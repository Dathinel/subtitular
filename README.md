# Subtitular

Subtítulos automáticos para Windows, en tu PC y sin conexión. Le das un video o un audio y te devuelve el `.srt`, si quieres traducido a otro idioma o leído en voz alta.

> Fase 3 completada: distribución AOT autocontenida (zip e instalador Inno Setup), entrada de catálogo firmado de Rodaje y flujos de GitHub Actions.

## Qué hay

- **`Subtitular.exe`** (ventana): suelta un video o audio, elige idioma, formato (SRT/VTT), carpeta, «Traducir a…» y «Leer en voz alta»; barra de progreso por etapa, Cancelar, vista previa editable bloque a bloque y Guardar. Los errores salen en español dentro de la ventana.
- **`Subtitular.Cli.exe`**: `Subtitular.Cli video.mp4 [--idioma es] [--formato vtt] [--traducir en] [--voz] [--salida carpeta]`. Deja `video.srt` (o `video.en.srt`) y, con `--voz`, `video.wav` con cada frase en su tiempo.
- Toda la lógica vive en `Subtitular.Core`; la ventana y la CLI solo la llaman. Cifras en `docs/informe-fase-1.md` y `docs/informe-fase-2.md`.

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
build\publicar.cmd
```

`build\publicar.cmd` deja la ventana y la CLI publicadas con Native AOT en `dist\Subtitular\`. Los módulos de IA (`Rodaje.Module.Subtitles.exe`, `.Translate.exe`, `.Voice.exe`) se buscan en `SUBTITULAR_MODULOS`, junto al exe (`modulos\`) o donde los instala Rodaje (`%LOCALAPPDATA%\Rodaje\complementos\modulos\modulo-*`).

El SDK de .NET y FFmpeg se instalan en `.entorno\` con huella fijada, sin tocar nada global. Los scripts son los de Rodaje (submódulo `externo/rodaje`, fijado a un commit). Para no bajar el SDK otra vez se puede apuntar `RODAJE_ENTORNO` al `.entorno` de Rodaje.

## Distribución y catálogo

Para empaquetar la distribución:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build\distribuir.ps1
```
Genera en `dist\`:
- `Subtitular-<versión>-win-x64.zip`: autocontenido (App, Cli, licencias, README corto y `herramienta.json`).
- `Subtitular-<versión>-instalador.exe`: instalador por usuario con Inno Setup (sin permisos de administrador).
- `SHA256SUMS` / `SHA256SUMS.txt`: huellas de los artefactos.
- `notas-version.md`: notas extraídas desde `CHANGELOG.md`.
- `catalogo-subtitular.json`: entrada `app` para el catálogo firmado de Rodaje.

**Firma del catálogo:** `build\catalogo-entrada.ps1` deja el JSON en `dist\catalogo-subtitular.json` sin firmar; Daniel lo firma con la clave privada del catálogo usando `firmar-indice.ps1` del repositorio `Dathinel/rodaje-catalogo`.

## Licencia

GPLv3, solo la versión 3 (`GPL-3.0-only`). Va con GPLv3 porque reutiliza código de Rodaje (el protocolo de módulos, entre otras piezas) y sus módulos de IA, que son GPLv3. Ver [LICENSE](LICENSE) y [LICENCIAS.md](LICENCIAS.md).
