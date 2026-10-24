# Fase 2: ventana WinUI 3 con AOT, traducción y voz (24-oct-2026)

Hecha por Claude (Fable 5.1) en el worktree `fase-2-ui`; pendiente de verificación independiente.

- **`Subtitular.Core`** gana `SubtitleTranslator` (Rodaje.Module.Translate, `traducir`, búfer de salida de 8 MiB), `SpeechSynthesizer` (Rodaje.Module.Voice, `voz` + `entregar`, cada frase colocada en su tiempo, WAV PCM 16 bits), `SubtitleJob` (etapas, progreso ponderado, cancelación, guardado y mensajes de error en español), `SubtitlePreview` (bloques editables → transcripción), `MediaInfo` (duración por ffprobe, subtítulos al lado) y `Modules` (búsqueda común de los tres módulos, también en `%LOCALAPPDATA%\Rodaje\complementos\modulos\modulo-*`).
- **`Subtitular.Cli`** llama a `SubtitleJob`; nuevas `--traducir <idioma>`, `--voz`, `--modulo-traducir`, `--modulo-voz`. Con `--traducir en` la salida es `video.en.srt` (y `video.en.wav`).
- **`Subtitular.App`** (WinUI 3 sin empaquetar, Native AOT, receta de csproj de Rodaje.App y `Main` propio; interfaz propia): soltar archivo o «Elegir…», nombre + duración + «ya tiene subtítulos», idioma, SRT/VTT, carpeta, «Traducir a…», «Leer en voz alta», barra por etapa con Cancelar, lista editable (`x:Bind` TwoWay por bloque), Guardar e `InfoBar` para los errores.

## Cifras

- `build\dotnet.cmd build src\Subtitular.slnx -c Release -warnaserror`: 0 advertencias. `build\publicar.cmd` (AOT, App y Cli) sin avisos de recorte ni de CsWinRT (nivel 2).
- **Arranque en frío** hasta el primer cuadro (`Subtitular.exe --medir`, `Stopwatch` desde `Main` hasta `CompositionTarget.Rendering`): **695 ms**; en caliente 273-311 ms. Límite: 1 s.
- Pruebas rápidas: **27 de 27** (16 de la fase 1 + progreso, rutas de salida, vista previa, colocación de la voz, WAV, idiomas de voz, contrato del traductor contra `Rodaje.Module.Echo`).
- Prueba larga (`RODAJE_PRUEBAS_LARGAS=1`, clip FLEURS de 11 s, GPU Vulkan): decodificar 0,8 s, transcribir 3,0 s (`large-v3-turbo-q5_0`), traducir al inglés 14,9 s (incluye cargar Qwen3-4B, 2,5 GB), voz 5,2 s (Kokoro en CPU, con carga); **18,8 s en total**. Traducción: «We can start living a more planet-friendly life, join environmental movements, or even become activists…».
- Publicado en `dist\Subtitular\`: `Subtitular.exe` **7.784.960 bytes**, `Subtitular.Cli.exe` 4.208.128 bytes; carpeta completa 118 MB (Windows App SDK autocontenido, 68 archivos).

## Criterios

- Cumplidos: arranque < 1 s, 0 avisos, lógica nueva en Core con pruebas sin WinUI, prueba larga de punta a punta con tiempos, `docs/pruebas-a-mano.md`, tamaño anotado, `dist\Subtitular\` con App y Cli.
- Pendiente de Daniel: fluidez, arrastrar y soltar y legibilidad (`pruebas-a-mano.md`); la ventana se abrió y pintó, pero nadie la ha usado con el ratón.

## Pendiente

- Segmentos de más de 500 caracteres o más de 5000 segmentos: el traductor los rechaza (límite del módulo); habría que partirlos.
- La voz lee con la voz por defecto del idioma; no hay selector de voz ni de velocidad en la ventana.
- Fase 3: zip en `dist\`, entrada `app` en el catálogo firmado y release.

## Verificación (24-oct, otro agente Claude)

- Release `-warnaserror`: 0 advertencias; `build\publicar.cmd` sin avisos de recorte ni de CsWinRT. Rápidas: 27/27 en dos pasadas limpias (una tercera, con la CPU al 100 % por otros agentes, falló los 4 contratos con Eco por tiempo de espera de 30 s: no es del código). Larga: pasa, 24,96 s (decodificar 1,1 s, transcribir 3,7 s, traducir 20,1 s con carga de Qwen; la voz sale sumada a traducir porque el progreso de voz solo llega al terminar cada frase).
- Publicado: `Subtitular.exe` 7.793.664 bytes, `Subtitular.Cli.exe` 4.208.128; carpeta 123 MB. Arranque medido desde fuera (`MainWindowHandle` ≠ 0 cada 50 ms): 294 / 206 / 163 ms; hasta el primer cuadro (`--medir`): 318 / 275 / 397 ms. Vivo 3 s sin dejar archivos.
- Arreglado en este commit: tras cancelar o fallar «Generar», «Guardar» quedaba activo con la transcripción anterior y la lista vacía (escribía un archivo vacío); y las excepciones de `async void` tras un `await` (soltar, Elegir…, Carpeta…) no pasan por `Application.UnhandledException` en WinUI 3 (Rodaje lo salva con `GuardedSynchronizationContext`): ahora salen en la barra.
- Probado con el ratón (24-oct, `voz.wav` de 8 s, «Leer en voz alta», « (editado)» en el 2.º bloque, Guardar): dos defectos. (1) Pérdida de datos: la voz se escribía en `voz.wav`, encima de la entrada, porque `OutputPath` usaba el mismo nombre base y la extensión `.wav`; ahora es `<base>.voz.wav` y `OutputPath` renombra con `.subtitular` cualquier salida igual a la entrada (prueba `Output_path_never_overwrites_the_input`). (2) La edición no llegaba al SRT: el `TextBox` de la lista con `x:Bind TwoWay` solo copiaba al `CueItem` al perder el foco; ahora `UpdateSourceTrigger=PropertyChanged` (prueba de Core ampliada con el texto cambiado).
- Pendiente: `FileOpenPicker`/`FolderPicker` de WinRT fallan si la app corre como administrador; la barra lo explica y pide arrastrar el archivo. Pasar a `IFileOpenDialog` (como Rodaje) solo si Daniel lo necesita elevado. Si el usuario cambia «Traducir a…» o el formato entre Generar y Guardar, el nombre del archivo sigue a la selección nueva, no a la transcripción.

## Probado con el ratón (Claude, 24-oct)

Con `Subtitular.exe` publicado y `voz.wav` (8 s, FLEURS es): primer intento con los selectores WinRT, inutilizable (ventana colapsada); tras 4c1b6bb el diálogo nativo abre, acepta la ruta escrita y carga el audio (duración y «sin subtítulos al lado»). Generar con «Leer en voz alta»: 2 bloques correctos en <5 s. Antes de ed3ac05: la voz pisó `voz.wav` y la edición no llegó al SRT. Después: `voz.srt` con « (editado)», `voz.voz.wav` (8,5 s) y el `voz.wav` de entrada byte a byte intacto. No probado: Cancelar (acaba antes), arrastrar y soltar, «Traducir a…» desde la ventana (sí en la prueba larga).
