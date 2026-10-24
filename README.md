# Subtitular

Subtítulos automáticos para Windows, en tu PC y sin conexión. Le das un video o un audio y te devuelve el `.srt`, si quieres traducido a otro idioma o leído en voz alta.

> En construcción: fase 0 (esqueleto). Todavía no hace nada.

## Cómo funciona

- Saca el audio del archivo con FFmpeg (LGPL).
- Lo pasa a los módulos de IA de Rodaje, que corren como procesos aparte y hablan con la app por una tubería con nombre (protocolo de módulos v1):
  - **modulo-subtitulos**: Whisper (whisper.cpp) en la GPU por Vulkan, o en la CPU.
  - **modulo-traducir**: Qwen3-4B-Instruct en llama.cpp.
  - **modulo-voz**: Kokoro-82M en ONNX Runtime, con fonemizador propio para español.
- Los modelos se bajan una sola vez desde el catálogo firmado de Rodaje y se comprueba su SHA-256.

## Cómo se ejecuta (desarrollo)

```
git clone --recursive https://github.com/Dathinel/subtitular
cd subtitular
powershell -NoProfile -ExecutionPolicy Bypass -File build\preparar-entorno.ps1
build\dotnet.cmd build src\Subtitular.slnx
```

El SDK de .NET y FFmpeg se instalan en `.entorno\` con huella fijada, sin tocar nada global. Los scripts son los de Rodaje (submódulo `externo/rodaje`, fijado a un commit). Para no bajar el SDK otra vez se puede apuntar `RODAJE_ENTORNO` al `.entorno` de Rodaje.

## Licencia

GPLv3, solo la versión 3 (`GPL-3.0-only`). Va con GPLv3 porque reutiliza código de Rodaje (el protocolo de módulos, entre otras piezas) y sus módulos de IA, que son GPLv3. Ver [LICENSE](LICENSE) y [LICENCIAS.md](LICENCIAS.md).
