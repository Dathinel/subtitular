using Rodaje.Modules.Protocol;
using Subtitular.Core;

namespace Subtitular.Tests;

/// <summary>El contrato de «traducir» (segments con id y text, mismos tiempos) contra el módulo Eco, que devuelve los parámetros.</summary>
public sealed class TranslatorEchoTests
{
    [Fact]
    public async Task Translator_keeps_times_and_takes_texts_by_id()
    {
        Transcript original = new("es", "cpu",
        [
            new TranscriptSegment(0, 1.5, " Hola. ", [new TranscriptWord(0, 1, "Hola.")]),
            new TranscriptSegment(1.5, 3, "Adiós."),
        ]);

        await using ModuleProcess module = await ModuleProcess.StartAsync(ModuleEchoTests.EchoExe);
        Transcript result = await SubtitleTranslator.InvokeAsync(module.Client, "eco", original, "en", "auto", null, CancellationToken.None);

        Assert.Equal("en", result.Language);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(1.5, result.Segments[0].End);
        Assert.Equal("Hola.", result.Segments[0].Text);
        Assert.Null(result.Segments[0].Words);
        Assert.Equal("Adiós.", result.Segments[1].Text);
    }

    [Fact]
    public async Task Unknown_target_language_is_a_user_error_before_starting_the_module()
    {
        Transcript t = new("es", "cpu", [new TranscriptSegment(0, 1, "Hola")]);
        await Assert.ThrowsAsync<SubtitularUserException>(() => SubtitleTranslator.TranslateAsync(t, "xx"));
    }
}
