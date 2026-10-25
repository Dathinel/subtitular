# Historial de cambios de Subtitular

## [1.0.0] - 2026-10-24

### Añadido
- **Subtitular.App**: interfaz de usuario moderna en WinUI 3 compilada de antemano con Native AOT.
  - Selección de video o audio mediante diálogo de sistema o arrastrar y soltar.
  - Generación de subtítulos automáticos en formato SRT y VTT.
  - Traducción opcional conservando las marcas de tiempo (Qwen3-4B-Instruct-2507).
  - Lectura en voz alta sincronizada con cada frase (Kokoro-82M).
  - Vista previa interactiva editable bloque a bloque con sincronización en tiempo real.
  - Barra de progreso por etapas y cancelación inmediata.
  - Manejo de incidencias con mensajes en español dentro de la ventana.
- **Subtitular.Cli**: herramienta de línea de comandos para automatización y scripts (`--idioma`, `--formato`, `--traducir`, `--voz`, `--salida`).
- **Subtitular.Core**: biblioteca base con extracción de audio por FFmpeg (LGPL) y conexión por tubería con nombre a los módulos de Rodaje (`modulo-subtitulos`, `modulo-traducir`, `modulo-voz`).
- **Distribución**: paquete zip autocontenido AOT win-x64, entrada de catálogo firmado e instalador Inno Setup por usuario.
- **Módulos compartidos**: se localizan donde los instala el Lanzador (`%LOCALAPPDATA%\Dathinel\Comun\modulos`, versión más nueva), donde los instala Rodaje, en `SUBTITULAR_MODULOS` o junto al exe; si faltan, el error lista las rutas probadas.
