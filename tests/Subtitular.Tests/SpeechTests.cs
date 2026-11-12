using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class SpeechTests
{
    [Fact]
    public void Clips_start_at_their_time_and_wait_for_the_previous_one()
    {
        float[] a = [1, 1, 1, 1];
        float[] b = [2, 2];
        // b empieza en 0,2 s (= muestra 2) pero a dura hasta la 4: se corre a la 4.
        float[] timeline = SpeechSynthesizer.Lay([(0, a), (0.2, b)], 10);
        Assert.Equal(6, timeline.Length);
        Assert.Equal([1, 1, 1, 1, 2, 2], timeline);

        float[] spaced = SpeechSynthesizer.Lay([(0.5, b), (1.0, b)], 10);
        Assert.Equal(12, spaced.Length);
        Assert.Equal(0, spaced[4]);
        Assert.Equal(2, spaced[5]);
        Assert.Equal(2, spaced[10]);
    }

    [Fact]
    public void Wav_is_16_bit_pcm_mono()
    {
        string path = Path.Combine(Path.GetTempPath(), $"subtitular-{Guid.NewGuid():N}.wav");
        try
        {
            SpeechSynthesizer.WriteWav(path, [0f, 1f, -1f], 24000);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(44 + 6, bytes.Length);
            Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
            Assert.Equal(1, BitConverter.ToInt16(bytes, 20)); // PCM
            Assert.Equal(1, BitConverter.ToInt16(bytes, 22)); // mono
            Assert.Equal(24000, BitConverter.ToInt32(bytes, 24));
            Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
            Assert.Equal(6, BitConverter.ToInt32(bytes, 40));
            Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 46));
            Assert.Equal(-short.MaxValue, BitConverter.ToInt16(bytes, 48));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Same_speech_ignores_cue_splits_and_spacing_but_not_words_or_language()
    {
        Transcript spoken = new("es", "cpu", [new TranscriptSegment(0, 4, "Hola a todos. Segundo bloque."), new TranscriptSegment(4, 5, "  ")]);
        // La vista previa parte y recorta los bloques: mismas palabras, misma voz.
        Transcript reviewed = new("es", "cpu", [new TranscriptSegment(0, 2, "Hola a todos."), new TranscriptSegment(2, 4, " Segundo  bloque. ")]);
        Assert.True(SpeechSynthesizer.SameSpeech(spoken, reviewed));
        Assert.True(SpeechSynthesizer.SameSpeech(spoken, reviewed with { Language = "ES" }));
        Assert.False(SpeechSynthesizer.SameSpeech(spoken, reviewed with { Language = "en" }));
        Assert.False(SpeechSynthesizer.SameSpeech(spoken, new Transcript("es", "cpu", [new TranscriptSegment(0, 2, "Hola a todas."), reviewed.Segments[1]])));
        Assert.False(SpeechSynthesizer.SameSpeech(spoken, new Transcript("es", "cpu", [reviewed.Segments[0]])));

        // La puntuación cambia la entonación y las mayúsculas importan: «Hola.» vs «hola» genera voz distinta.
        Transcript punct = new("es", "cpu", [new TranscriptSegment(0, 2, "Hola.")]);
        Transcript noPunct = new("es", "cpu", [new TranscriptSegment(0, 2, "hola")]);
        Assert.False(SpeechSynthesizer.SameSpeech(punct, noPunct));
    }

    [Fact]
    public async Task Save_keeps_the_wav_when_the_text_did_not_change_and_regenerates_otherwise()
    {
        string dir = Directory.CreateTempSubdirectory("subtitular-voz-").FullName;
        try
        {
            Transcript spoken = new("es", "cpu", [new TranscriptSegment(0, 2, "Hola a todos.")]);
            // Con un módulo de voz que no existe, generar la voz falla: si Guardar devuelve el WAV, es que no lo tocó.
            JobOptions options = new(Path.Combine(dir, "clip.mp4")) { Speak = true, VoiceModule = Path.Combine(dir, "no-hay-modulo") };
            string wav = Path.Combine(dir, "clip.voz.wav");
            byte[] previous = [1, 2, 3];
            File.WriteAllBytes(wav, previous);
            List<JobProgress> reports = [];

            (string subtitles, string? audio) = await SubtitleJob.SaveAsync(spoken, options, new SyncProgress<JobProgress>(reports.Add), CancellationToken.None, spoken);
            Assert.Equal(Path.Combine(dir, "clip.srt"), subtitles);
            Assert.Equal(wav, audio);
            Assert.Equal(previous, File.ReadAllBytes(wav));
            Assert.Contains("Hola a todos.", File.ReadAllText(subtitles), StringComparison.Ordinal);
            Assert.Contains(reports, r => r is { Stage: JobStage.Speak, StageFraction: 1 });

            // Texto corregido: hay que volver a leerlo (y aquí el módulo no está).
            Transcript edited = new("es", "cpu", [new TranscriptSegment(0, 2, "Hola a todas.")]);
            await Assert.ThrowsAsync<SubtitularUserException>(() => SubtitleJob.SaveAsync(edited, options, null, CancellationToken.None, spoken));
            // Sin WAV previo, aunque el texto sea el mismo, se genera.
            File.Delete(wav);
            await Assert.ThrowsAsync<SubtitularUserException>(() => SubtitleJob.SaveAsync(spoken, options, null, CancellationToken.None, spoken));
            // Sin saber de qué texto era el WAV (la vista previa de un solo archivo), se genera siempre.
            File.WriteAllBytes(wav, previous);
            await Assert.ThrowsAsync<SubtitularUserException>(() => SubtitleJob.SaveAsync(spoken, options, null, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Voice_language_maps_whisper_codes_and_rejects_the_rest()
    {
        Assert.Equal("es", SpeechSynthesizer.VoiceLanguage("es"));
        Assert.Equal("en-US", SpeechSynthesizer.VoiceLanguage("en"));
        Assert.Equal("pt-BR", SpeechSynthesizer.VoiceLanguage("pt"));
        SubtitularUserException ex = Assert.Throws<SubtitularUserException>(() => SpeechSynthesizer.VoiceLanguage("fr"));
        Assert.Contains("fr", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Hola mundo", "Hola mundo", true)]
    [InlineData("Hola mundo", "Hola   mundo", true)]
    [InlineData("Hola mundo", "  Hola mundo  ", true)]
    [InlineData("Hola mundo", "Hola\t\r\nmundo", true)]
    [InlineData("Hola.", "Hola", false)]
    [InlineData("Hola!", "Hola?", false)]
    [InlineData("Hola, mundo", "Hola mundo", false)]
    [InlineData("Hola mundo", "hola mundo", false)]
    [InlineData("HOLA MUNDO", "Hola Mundo", false)]
    [InlineData("«Hola»", "Hola", false)]
    public void SameSpeech_table_of_pairs_matching_behavior(string textA, string textB, bool expected)
    {
        Transcript a = new("es", "cpu", [new TranscriptSegment(0, 2, textA)]);
        Transcript b = new("es", "cpu", [new TranscriptSegment(0, 2, textB)]);
        Assert.Equal(expected, SpeechSynthesizer.SameSpeech(a, b));
    }

    [Fact]
    public void SameSpeech_table_of_pairs_for_segment_structures()
    {
        Transcript single = new("es", "cpu", [new TranscriptSegment(0, 4, "Uno dos tres cuatro")]);
        Transcript two = new("es", "cpu", [new TranscriptSegment(0, 2, "Uno dos"), new TranscriptSegment(2, 4, "tres cuatro")]);
        Transcript three = new("es", "cpu", [new TranscriptSegment(0, 1, "Uno"), new TranscriptSegment(1, 2, "dos tres"), new TranscriptSegment(2, 4, "cuatro")]);
        Transcript withEmpties = new("es", "cpu", [new TranscriptSegment(0, 1, "Uno dos"), new TranscriptSegment(1, 2, "   "), new TranscriptSegment(2, 3, ""), new TranscriptSegment(3, 4, "tres cuatro")]);

        Assert.True(SpeechSynthesizer.SameSpeech(single, two));
        Assert.True(SpeechSynthesizer.SameSpeech(single, three));
        Assert.True(SpeechSynthesizer.SameSpeech(single, withEmpties));

        Transcript empty1 = new("es", "cpu", []);
        Transcript empty2 = new("es", "cpu", []);
        Transcript emptyWhitespace = new("es", "cpu", [new TranscriptSegment(0, 1, "   "), new TranscriptSegment(1, 2, "")]);

        Assert.True(SpeechSynthesizer.SameSpeech(empty1, empty2));
        Assert.True(SpeechSynthesizer.SameSpeech(empty1, emptyWhitespace));
        Assert.False(SpeechSynthesizer.SameSpeech(empty1, single));
    }

    [Theory]
    [InlineData("es", "es", true)]
    [InlineData("es", "ES", true)]
    [InlineData("ES", "es", true)]
    [InlineData("en-US", "en-us", true)]
    [InlineData("pt-BR", "pt-br", true)]
    [InlineData("es", "en", false)]
    [InlineData("es", "pt", false)]
    public void SameSpeech_language_comparison_rules(string langA, string langB, bool expected)
    {
        Transcript a = new(langA, "cpu", [new TranscriptSegment(0, 2, "Mismo texto")]);
        Transcript b = new(langB, "cpu", [new TranscriptSegment(0, 2, "Mismo texto")]);
        Assert.Equal(expected, SpeechSynthesizer.SameSpeech(a, b));
    }

    [Fact]
    public void SameSpeech_throws_on_null_arguments()
    {
        Transcript valid = new("es", "cpu", [new TranscriptSegment(0, 2, "Texto")]);
        Assert.Throws<ArgumentNullException>(() => SpeechSynthesizer.SameSpeech(null!, valid));
        Assert.Throws<ArgumentNullException>(() => SpeechSynthesizer.SameSpeech(valid, null!));
    }

    [Theory]
    [InlineData("es", "es")]
    [InlineData("es-419", "es")]
    [InlineData("spanish", "es")]
    [InlineData("SPANISH", "es")]
    [InlineData("pt", "pt-BR")]
    [InlineData("pt-br", "pt-BR")]
    [InlineData("pt-pt", "pt-BR")]
    [InlineData("portuguese", "pt-BR")]
    [InlineData("en", "en-US")]
    [InlineData("en-us", "en-US")]
    [InlineData("english", "en-US")]
    [InlineData("en-gb", "en-GB")]
    [InlineData("EN-GB", "en-GB")]
    public void VoiceLanguage_mappings_all_supported_variants(string inputLang, string expectedVoice)
    {
        Assert.Equal(expectedVoice, SpeechSynthesizer.VoiceLanguage(inputLang));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("ja")]
    [InlineData("zh")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void VoiceLanguage_rejects_unsupported_languages_with_clear_message(string? unsupported)
    {
        SubtitularUserException ex = Assert.Throws<SubtitularUserException>(() => SpeechSynthesizer.VoiceLanguage(unsupported));
        Assert.Contains("La voz solo lee español, portugués e inglés", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lay_edge_cases_empty_clips_negative_time_and_zero_samples()
    {
        Assert.Empty(SpeechSynthesizer.Lay([], 16000));

        // Inicio negativo se recorta a muestra 0
        float[] clip = [0.5f, 0.5f];
        float[] timeline = SpeechSynthesizer.Lay([(-2.0, clip)], 10);
        Assert.Equal(2, timeline.Length);
        Assert.Equal([0.5f, 0.5f], timeline);

        // Clip vacío
        float[] emptyTimeline = SpeechSynthesizer.Lay([(1.0, [])], 10);
        Assert.Equal(10, emptyTimeline.Length); // cursor en 1.0 s = 10 muestras de ceros
        Assert.All(emptyTimeline, s => Assert.Equal(0f, s));
    }
}
