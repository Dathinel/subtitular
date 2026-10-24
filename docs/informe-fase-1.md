# Fase 1: CLI de subtítulos (24-oct-2026)

Hecha por agy (gemini-3.8-flash); verificada por Claude en un equipo con GPU Vulkan.

- `Subtitular.Core` (`IsAotCompatible`): `AudioDecoder` (ffmpeg → f32le mono 16 kHz en memoria), `SubtitleTranscriber` (`ModuleProcess` + `ModuleClient` contra `Rodaje.Module.Subtitles`, salida por búfer compartido de 8 MiB) y `SubtitleWriter` (SRT y WebVTT, 42 caracteres por línea, 2 líneas por bloque, escape VTT). JSON con generadores de código fuente.
- `Subtitular.Cli`: `<archivo> [--idioma] [--formato srt|vtt] [--salida] [--modulo] [--ffmpeg] [--device] [--model] [--threads]`; progreso en stderr, Ctrl+C cancela. Salida 0 bien, 1 error del usuario, 2 error del módulo.
- No se copió código de `Rodaje.App` (`ModuleMedia`, `ModuleManager`): el protocolo va por `ProjectReference` al submódulo.

## Cifras

- `build\dotnet.cmd build src\Subtitular.slnx -c Release -warnaserror`: 0 advertencias.
- Pruebas rápidas: 16 de 16 (escritor, JSON, Echo: llamada, cancelación < 1 s, módulo que muere).
- Prueba larga (`RODAJE_PRUEBAS_LARGAS=1`, 66,9 s de FLEURS es_419): 5,996 s con `ggml-large-v3-turbo-q5_0` en GPU, RTF 0,09; WER 2,13 % (límite 15 %).
- CLI publicado con Native AOT sin advertencias: `Subtitular.Cli.exe` 4.017.664 bytes. Clip real de 17 s en SRT: 3,2 s de pared, módulo y modelo incluidos.

## Pendiente

- Fase 2: interfaz WinUI 3 sobre `Subtitular.Core`.
- `--traducir` (`Rodaje.Module.Translate`) y `--voz` (`Rodaje.Module.Voice`), encargo aparte.
