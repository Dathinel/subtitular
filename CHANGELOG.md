# Historial de cambios de Subtitular

## [Sin publicar]

### Añadido
- **Cola de archivos**: se pueden soltar o elegir varios videos o audios a la vez; se procesan uno tras otro con «Archivo 2 de 5 · nombre · etapa» en la barra, cada uno se guarda al terminar y un archivo que falla no para a los demás. Cancelar corta el archivo en curso y no deja procesos de FFmpeg ni del módulo vivos.
- **Abrir la carpeta al terminar**: casilla (activada de fábrica) que abre el Explorador con el resultado seleccionado al guardar.
- **Atajos**: `Ctrl+O` abre «Elegir…» y `Ctrl+Entrar` pulsa «Generar subtítulos».
- **Aviso de versión nueva**: al arrancar, como mucho una vez cada 24 h, la ventana consulta la última release de `Dathinel/subtitular` en la API pública de GitHub (sin cuenta, `User-Agent` «Subtitular/<versión>», 5 s de límite, fuera del hilo de la interfaz); sin red o con una respuesta rara, no dice nada. Si hay una versión mayor, una franja discreta arriba dice «Subtitular 1.1.0 disponible» con «Ver novedades» (la release en el navegador), «Abrir el Lanzador» si está instalado (`%LOCALAPPDATA%\Dathinel\Lanzador` o `SUBTITULAR_LANZADOR`) y «Más tarde» (no vuelve a avisar de esa versión). En Ajustes, la casilla «Avisar cuando haya una versión nueva» (activada de fábrica) y «Buscar actualizaciones», que consulta al momento y también dice cuando no hay nada nuevo. La lógica (`UpdateCheck`, `ReleaseInfo`, `Launcher`) está en `Subtitular.Core` con pruebas sin red.

### Cambiado
- La ventana recuerda la última carpeta (la del archivo elegido, para el diálogo, y la de salida), el idioma del audio, el formato, «Traducir a…», «Leer en voz alta» y la casilla de abrir la carpeta (`%LOCALAPPDATA%\Dathinel\Subtitular\preferencias.json`). Ahí mismo van la casilla del aviso de versión nueva, la hora de la última consulta y la versión descartada con «Más tarde».

### Corregido
- La CI de `main` fallaba en `CatalogoEntradaTests`: `build\catalogo-entrada.ps1` dependía de `Get-FileHash`, que en Windows PowerShell 5.1 no existe si el proceso hereda un `PSModulePath` ajeno (el del host de pruebas lanzado desde pwsh). La huella se calcula ahora con las clases de .NET.

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
