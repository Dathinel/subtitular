using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class SubtitleWriterTests
{
    [Fact]
    public void Format_timestamps_srt_and_vtt()
    {
        TimeSpan t1 = TimeSpan.Zero;
        TimeSpan t2 = TimeSpan.FromSeconds(1.234);
        TimeSpan t3 = TimeSpan.FromSeconds(3665.078);

        Assert.Equal("00:00:00,000", SubtitleWriter.FormatSrtTimestamp(t1));
        Assert.Equal("00:00:01,234", SubtitleWriter.FormatSrtTimestamp(t2));
        Assert.Equal("01:01:05,078", SubtitleWriter.FormatSrtTimestamp(t3));

        Assert.Equal("00:00:00.000", SubtitleWriter.FormatVttTimestamp(t1));
        Assert.Equal("00:00:01.234", SubtitleWriter.FormatVttTimestamp(t2));
        Assert.Equal("01:01:05.078", SubtitleWriter.FormatVttTimestamp(t3));
    }

    [Fact]
    public void Escape_vtt_special_characters()
    {
        string raw = "Tom & Jerry <en acción> dijeron \"¡hola!\"";
        string escaped = SubtitleWriter.EscapeVtt(raw);

        Assert.Equal("Tom &amp; Jerry &lt;en acción&gt; dijeron \"¡hola!\"", escaped);
    }

    [Fact]
    public void Cues_strictly_respect_42_chars_and_max_2_lines()
    {
        string longSentence = "Esta es una frase de prueba diseñada para verificar que los subtítulos se cortan a cuarenta y dos caracteres y no más de dos líneas por bloque de subtítulo.";
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 8.0, longSentence)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count is >= 1 and <= SubtitleWriter.MaxLinesPerCue,
                $"El bloque {cue.Index} tiene {cue.Lines.Count} líneas (máximo 2).");

            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= SubtitleWriter.MaxLineLength,
                    $"La línea «{line}» supera los {SubtitleWriter.MaxLineLength} caracteres (longitud {line.Length}).");
            }
        }
    }

    [Fact]
    public void Ultra_long_single_word_is_split_into_chunks_under_42_chars()
    {
        string ultraLongWord = new('A', 100);
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 5.0, ultraLongWord)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count <= 2);
            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= 42, $"Línea de largo {line.Length}: {line}");
            }
        }
    }

    [Fact]
    public void Lines_are_balanced_when_possible()
    {
        // 54 caracteres en total: si fuera desbalanceado, podría ser 42 + 11.
        // Con balanceo, debe ser dos líneas de largo similar (ej. ~27 caracteres).
        string text = "Esta es una frase moderada que debería ser dividida.";
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 4.0, text)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);
        Assert.Single(cues);
        Assert.Equal(2, cues[0].Lines.Count);

        int len1 = cues[0].Lines[0].Length;
        int len2 = cues[0].Lines[1].Length;
        Assert.True(len1 <= 42);
        Assert.True(len2 <= 42);
        Assert.True(Math.Abs(len1 - len2) <= 15, $"Diferencia entre líneas excesiva: {len1} vs {len2}");
    }

    [Fact]
    public void Srt_output_structure_and_numbering()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(1.0, 3.5, "Hola mundo.", [
                new TranscriptWord(1.0, 2.0, "Hola"),
                new TranscriptWord(2.0, 3.5, "mundo.")
            ]),
            new TranscriptSegment(4.0, 7.0, "Segunda frase.", [
                new TranscriptWord(4.0, 5.5, "Segunda"),
                new TranscriptWord(5.5, 7.0, "frase.")
            ])
        ]);

        string srt = SubtitleWriter.ToSrt(transcript);

        Assert.Contains("1\r\n00:00:01,000 --> 00:00:03,500\r\nHola mundo.\r\n", srt.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
        Assert.Contains("2\r\n00:00:04,000 --> 00:00:07,000\r\nSegunda frase.\r\n", srt.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
    }

    [Fact]
    public void Vtt_output_has_header_and_correct_format()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.5, 2.5, "Prueba <VTT> & subtítulos.")
        ]);

        string vtt = SubtitleWriter.ToVtt(transcript);

        Assert.StartsWith("WEBVTT", vtt.TrimStart());
        Assert.Contains("00:00:00.500 --> 00:00:02.500", vtt);
        Assert.Contains("Prueba &lt;VTT&gt; &amp; subtítulos.", vtt);
    }

    [Theory]
    [InlineData(0.0, "00:00:00,000", "00:00:00.000")]
    [InlineData(59.999, "00:00:59,999", "00:00:59.999")]
    [InlineData(60.0, "00:01:00,000", "00:01:00.000")]
    [InlineData(3599.999, "00:59:59,999", "00:59:59.999")]
    [InlineData(3600.0, "01:00:00,000", "01:00:00.000")]
    [InlineData(359999.0, "99:59:59,000", "99:59:59.000")]
    [InlineData(360000.0, "100:00:00,000", "100:00:00.000")]
    [InlineData(900000.123, "250:00:00,123", "250:00:00.123")]
    [InlineData(-5.0, "00:00:00,000", "00:00:00.000")]
    public void Format_timestamp_edge_values_srt_and_vtt(double seconds, string expectedSrt, string expectedVtt)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        Assert.Equal(expectedSrt, SubtitleWriter.FormatSrtTimestamp(time));
        Assert.Equal(expectedVtt, SubtitleWriter.FormatVttTimestamp(time));
    }

    [Theory]
    [InlineData("es-CO")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Decimal_separator_is_invariant_under_different_cultures(string cultureName)
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            TimeSpan time = TimeSpan.FromSeconds(12.345);

            string srt = SubtitleWriter.FormatSrtTimestamp(time);
            string vtt = SubtitleWriter.FormatVttTimestamp(time);

            Assert.Equal("00:00:12,345", srt);
            Assert.Equal("00:00:12.345", vtt);

            Transcript transcript = new("es", "cpu", [new TranscriptSegment(1.5, 2.25, "Coma decimal.")]);
            Assert.Contains("00:00:01,500 --> 00:00:02,250", SubtitleWriter.ToSrt(transcript), StringComparison.Ordinal);
            Assert.Contains("00:00:01.500 --> 00:00:02.250", SubtitleWriter.ToVtt(transcript), StringComparison.Ordinal);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Cues_with_zero_or_negative_duration_produce_valid_monotonic_cues()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(5.0, 5.0, "Duración cero."),
            new TranscriptSegment(8.0, 4.0, "Duración negativa.")
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.Equal(2, cues.Count);
        Assert.Equal(TimeSpan.FromSeconds(5.0), cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(5.1), cues[0].End);
        Assert.True(cues[0].End > cues[0].Start);

        Assert.Equal(TimeSpan.FromSeconds(8.0), cues[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(8.1), cues[1].End);
        Assert.True(cues[1].End > cues[1].Start);
    }

    [Fact]
    public void Overlapping_and_unordered_segments_are_clamped_and_monotonic()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 5.0, "Primer segmento."),
            new TranscriptSegment(3.0, 6.0, "Segundo segmento solapado."),
            new TranscriptSegment(2.0, 4.0, "Tercer segmento desordenado.")
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.True(cues.Count >= 3);
        for (int i = 0; i < cues.Count; i++)
        {
            Assert.True(cues[i].End > cues[i].Start, $"Cue {i} debe tener duración positiva.");
            if (i > 0)
            {
                Assert.True(cues[i].Start >= cues[i - 1].End, $"Cue {i} debe empezar en o después del fin del cue {i - 1}.");
            }
        }
    }

    [Fact]
    public void Empty_and_whitespace_segments_are_omitted_without_breaking_consecutive_numbering()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 2.0, "Primer bloque."),
            new TranscriptSegment(2.0, 3.0, ""),
            new TranscriptSegment(3.0, 4.0, "   \t\r\n   "),
            new TranscriptSegment(4.0, 6.0, "Segundo bloque.")
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.Equal(2, cues.Count);
        Assert.Equal(1, cues[0].Index);
        Assert.Equal(2, cues[1].Index);
        Assert.Equal(["Primer bloque."], cues[0].Lines);
        Assert.Equal(["Segundo bloque."], cues[1].Lines);
    }

    [Fact]
    public void Special_characters_handling_arrows_html_tags_and_emojis()
    {
        string text = "Flecha --> y etiqueta <i>cursiva</i> & más emojis: 👋 🌍 𝄞";
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(1.0, 4.0, text)
        ]);

        string srt = SubtitleWriter.ToSrt(transcript);
        string vtt = SubtitleWriter.ToVtt(transcript);

        // SRT no tiene escapes: el texto va tal cual, y la flecha dentro del texto no añade otra línea de tiempos.
        Assert.Contains("Flecha --> y", srt);
        Assert.Single(srt.Split('\n'), l => l.Contains(" --> ", StringComparison.Ordinal) && char.IsAsciiDigit(l[0]));
        Assert.Contains("<i>cursiva</i>", srt);
        Assert.Contains("👋 🌍 𝄞", srt);

        // WebVTT prohíbe «-->» dentro del texto: solo queda la de la línea de tiempos.
        Assert.Contains("Flecha --&gt; y", vtt);
        Assert.Single(vtt.Split('\n'), l => l.Contains("-->", StringComparison.Ordinal));
        Assert.Contains("&lt;i&gt;cursiva&lt;/i&gt;", vtt);
        Assert.Contains("&amp; más", vtt);
        Assert.Contains("👋 🌍 𝄞", vtt);
    }

    [Fact]
    public async Task SaveAsync_writes_utf8_without_bom_and_crlf_line_endings()
    {
        string dir = Directory.CreateTempSubdirectory("subtitular-bom-").FullName;
        try
        {
            Transcript transcript = new("es", "cpu",
            [
                new TranscriptSegment(1.0, 3.0, "Prueba codificación.")
            ]);

            JobOptions srtOpt = new(Path.Combine(dir, "video.mp4")) { Format = "srt" };
            (string srtPath, _) = await SubtitleJob.SaveAsync(transcript, srtOpt);

            byte[] srtBytes = await File.ReadAllBytesAsync(srtPath);
            Assert.False(srtBytes.Length >= 3 && srtBytes[0] == 0xEF && srtBytes[1] == 0xBB && srtBytes[2] == 0xBF,
                "El archivo SRT no debe contener BOM UTF-8.");
            string srtContent = Encoding.UTF8.GetString(srtBytes);
            Assert.Contains("\r\n", srtContent);

            JobOptions vttOpt = new(Path.Combine(dir, "video.mp4")) { Format = "vtt" };
            (string vttPath, _) = await SubtitleJob.SaveAsync(transcript, vttOpt);

            byte[] vttBytes = await File.ReadAllBytesAsync(vttPath);
            Assert.False(vttBytes.Length >= 3 && vttBytes[0] == 0xEF && vttBytes[1] == 0xBB && vttBytes[2] == 0xBF,
                "El archivo VTT no debe contener BOM UTF-8.");
            string vttContent = Encoding.UTF8.GetString(vttBytes);
            Assert.StartsWith("WEBVTT\r\n\r\n", vttContent);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Empty_transcript_produces_empty_cues_and_valid_outputs()
    {
        Transcript empty = new("es", "cpu", []);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(empty);
        Assert.Empty(cues);

        string srt = SubtitleWriter.ToSrt(empty);
        Assert.Equal("", srt);

        string vtt = SubtitleWriter.ToVtt(empty);
        Assert.Equal("WEBVTT" + Environment.NewLine + Environment.NewLine, vtt);
    }

    [Fact]
    public void Single_giant_word_over_84_chars_splits_across_multiple_cues_under_limits()
    {
        string giantWord = new('Z', 130);
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 10.0, giantWord)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        int totalChars = 0;
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count is >= 1 and <= SubtitleWriter.MaxLinesPerCue);
            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= SubtitleWriter.MaxLineLength);
                totalChars += line.Length;
            }
        }
        Assert.Equal(130, totalChars);
    }

    [Fact]
    public void ExpandLongWords_splits_word_tokens_exceeding_max_line_length_proportionally()
    {
        string word90 = new('W', 90);
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(1.0, 10.0, word90,
            [
                new TranscriptWord(1.0, 10.0, word90)
            ])
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count <= 2);
            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= SubtitleWriter.MaxLineLength);
            }
        }
        // No se pierde ni una letra y los trozos ocupan el tiempo de la palabra, sin salirse.
        Assert.Equal(word90, string.Concat(cues.SelectMany(c => c.Lines)));
        Assert.Equal(TimeSpan.FromSeconds(1.0), cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(10.0), cues[^1].End);
    }

    [Fact]
    public void Right_to_left_text_splits_cleanly_respecting_limits()
    {
        string arabic = "هذا نص تجريبي طويل باللغة العربية للتحقق من تقسيم الأسطر والكتل الزمنية بشكل سليم ودقيق للغاية دون أي خطأ";
        Transcript transcript = new("ar", "cpu",
        [
            new TranscriptSegment(0.0, 6.0, arabic)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count is >= 1 and <= SubtitleWriter.MaxLinesPerCue);
            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= SubtitleWriter.MaxLineLength);
            }
        }
    }

    [Fact]
    public void Cjk_text_without_spaces_is_split_into_chunks_under_limits()
    {
        string chinese = "这是一个非常长的中文测试段落用来验证在完全没有任何空格的情况下字幕切分器是否能够正确按照四十二个字符的上限进行分割而不发生崩溃或者超出限制";
        Transcript transcript = new("zh", "cpu",
        [
            new TranscriptSegment(0.0, 8.0, chinese)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count is >= 1 and <= SubtitleWriter.MaxLinesPerCue);
            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= SubtitleWriter.MaxLineLength);
            }
        }
        Assert.Equal(chinese, string.Concat(cues.SelectMany(c => c.Lines)));
    }
    private static readonly Regex SrtTiming = new(@"^(\d{2,}):(\d{2}):(\d{2}),(\d{3}) --> (\d{2,}):(\d{2}):(\d{2}),(\d{3})$");

    /// <summary>Lee un SRT como un reproductor: bloques separados por una línea en blanco, con número, tiempos y una o dos líneas de texto.</summary>
    private static List<(TimeSpan Start, TimeSpan End, string[] Text)> ParseSrt(string srt)
    {
        List<(TimeSpan Start, TimeSpan End, string[] Text)> blocks = [];
        string[] raw = srt.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < raw.Length; i++)
        {
            string[] lines = raw[i].Split('\n');
            Assert.True(lines.Length is 3 or 4, $"Bloque {i + 1} mal formado: «{raw[i]}»");
            Assert.Equal((i + 1).ToString(CultureInfo.InvariantCulture), lines[0]);
            Match m = SrtTiming.Match(lines[1]);
            Assert.True(m.Success, $"Tiempos mal formados en el bloque {i + 1}: «{lines[1]}»");
            int[] n = [.. m.Groups.Values.Skip(1).Select(g => int.Parse(g.Value, CultureInfo.InvariantCulture))];
            Assert.All(lines[2..], l => Assert.False(string.IsNullOrWhiteSpace(l)));
            blocks.Add((new TimeSpan(0, n[0], n[1], n[2], n[3]), new TimeSpan(0, n[4], n[5], n[6], n[7]), lines[2..]));
        }
        return blocks;
    }

    [Fact]
    public void Hostile_transcript_still_reads_back_as_a_well_formed_srt()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(5.0, 5.0, "Duración cero."),
            new TranscriptSegment(8.0, 4.0, "Duración negativa."),
            new TranscriptSegment(-3.0, 1.0, "Empieza antes de cero."),
            new TranscriptSegment(6.0, 9.0, "Solapado con --> flecha."),
            new TranscriptSegment(2.0, 3.0, "Desordenado."),
            new TranscriptSegment(10.0, 11.0, "   "),
            new TranscriptSegment(11.0, 12.0, "2026"),
            new TranscriptSegment(12.0, 14.0, "Línea uno\r\n\r\nlínea dos"),
            new TranscriptSegment(14.0, 16.0, "con palabras", [new TranscriptWord(14.0, 15.0, "salto\n\ndentro"), new TranscriptWord(15.0, 16.0, " \t ")]),
            new TranscriptSegment(359999.5, 360001.0, "Más de 99 horas."),
        ]);

        List<(TimeSpan Start, TimeSpan End, string[] Text)> blocks = ParseSrt(SubtitleWriter.ToSrt(transcript));

        Assert.Equal(SubtitleWriter.BuildCues(transcript).Count, blocks.Count);
        Assert.Equal("Duración cero. Duración negativa. Empieza antes de cero. Solapado con --> flecha. Desordenado. 2026 Línea uno línea dos salto dentro Más de 99 horas.",
            string.Join(' ', blocks.SelectMany(b => b.Text)));
        for (int i = 0; i < blocks.Count; i++)
        {
            Assert.True(blocks[i].End > blocks[i].Start, $"El bloque {i + 1} no dura nada.");
            Assert.True(i == 0 || blocks[i].Start >= blocks[i - 1].End, $"El bloque {i + 1} empieza antes de que acabe el anterior.");
        }
        Assert.Equal(new TimeSpan(100, 0, 1), blocks[^1].End);

        // El mismo texto en WebVTT: cabecera, y ninguna línea en blanco ni «-->» fuera de las de tiempos.
        string[] vtt = SubtitleWriter.ToVtt(transcript).ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("WEBVTT", vtt[0]);
        Assert.Equal(blocks.Count, vtt.Length - 1);
        Assert.All(vtt[1..], block => Assert.Single(block.Split('\n'), l => l.Contains("-->", StringComparison.Ordinal)));
    }

    [Fact]
    public void Cues_shorter_than_a_millisecond_are_not_written_with_the_same_start_and_end()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(1.0001, 1.0004, "uno", [new TranscriptWord(1.0001, 1.0004, "uno")]),
            new TranscriptSegment(1.0005, 1.0009, "dos", [new TranscriptWord(1.0005, 1.0009, "dos")]),
        ]);

        List<(TimeSpan Start, TimeSpan End, string[] Text)> blocks = ParseSrt(SubtitleWriter.ToSrt(transcript));

        Assert.Equal([(1000, 1100), (1100, 1200)], blocks.Select(b => ((int)b.Start.TotalMilliseconds, (int)b.End.TotalMilliseconds)));
        Assert.Contains("00:00:01.000 --> 00:00:01.100", SubtitleWriter.ToVtt(transcript), StringComparison.Ordinal);
    }

    [Fact]
    public void Segment_whose_timed_words_are_all_blank_keeps_its_text()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(1.0, 3.0, "Hola mundo.", [new TranscriptWord(1.0, 2.0, " "), new TranscriptWord(2.0, 3.0, "")])
        ]);

        SubtitleCue cue = Assert.Single(SubtitleWriter.BuildCues(transcript));

        Assert.Equal(["Hola mundo."], cue.Lines);
        Assert.Equal(TimeSpan.FromSeconds(1.0), cue.Start);
        Assert.Equal(TimeSpan.FromSeconds(3.0), cue.End);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Long_token_is_never_cut_through_a_surrogate_pair(bool timedWords)
    {
        // Una letra y 30 emojis de dos unidades UTF-16: el corte a los 42 caería en mitad de un par y el archivo saldría con «�».
        string token = "a" + string.Concat(Enumerable.Repeat("😀", 30));
        TranscriptSegment segment = timedWords
            ? new(0.0, 6.0, token, [new TranscriptWord(0.0, 6.0, token)])
            : new(0.0, 6.0, token);

        List<string> lines = [.. SubtitleWriter.BuildCues(new Transcript("es", "cpu", [segment])).SelectMany(c => c.Lines)];

        Assert.All(lines, line =>
        {
            Assert.True(line.Length <= SubtitleWriter.MaxLineLength);
            Assert.Equal(line, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(line)));
        });
        Assert.Equal(token, string.Concat(lines));
    }
}
