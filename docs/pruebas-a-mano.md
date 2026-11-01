# Pruebas a mano (fase 2: la ventana)

Lo que solo Daniel puede juzgar. Antes: `build\publicar.cmd` deja `dist\Subtitular\Subtitular.exe` (ventana) y `Subtitular.Cli.exe`. Los módulos se buscan en `%LOCALAPPDATA%\Rodaje\complementos\modulos\modulo-*` (los que instala Rodaje) o en `SUBTITULAR_MODULOS`.

1. **Arranque.** Doble clic en `Subtitular.exe`. ¿La ventana aparece de golpe, sin pantalla en blanco ni parpadeo? (Medido: ver `informe-fase-2.md`.)
2. **Arrastrar y soltar.** Arrastra un `.mp4` desde el Explorador a cualquier punto de la ventana. Debe cambiar la cabecera al nombre del archivo y, debajo, la duración y «sin subtítulos al lado» o «ya tiene subtítulos: x.srt». Prueba también un `.txt`: debe salir una barra roja «no es un video ni un audio…», sin cuadro de error.
3. **Elegir… y Carpeta…** Abren los diálogos de Windows y rellenan la ruta. Cancelar no cambia nada. Defecto visto el 24-oct: con `FileOpenPicker`/`FolderPicker` de WinRT el selector salía colapsado (110×20 px, solo «Abrir» y la X) y fallaba si la app corría elevada; ahora son `IFileOpenDialog` nativos (`FileDialogs.cs`, copiado de Rastrear). Comprobar que el diálogo sale a tamaño normal y filtra por video y audio.
4. **Generar.** Con idioma «Detectar», pulsa «Generar subtítulos». La barra avanza por etapas («Decodificando», «Transcribiendo»…) y el texto de etapa cambia. ¿La ventana sigue respondiendo mientras tanto (se mueve, se redimensiona)?
5. **Cancelar.** Vuelve a generar y pulsa Cancelar a mitad: en menos de un segundo debe salir «Operación cancelada.» y los botones volver a su sitio.
6. **Vista previa.** Al terminar, la lista muestra un bloque por subtítulo con su tiempo a la izquierda y el texto editable a la derecha. Corrige una palabra, borra el texto de un bloque entero y pulsa Guardar: el `.srt` de la carpeta de salida debe traer la corrección y no el bloque vacío. Defecto visto el 24-oct con el ratón: añadir « (editado)» a un bloque y pulsar Guardar escribía el texto viejo; el `x:Bind` TwoWay del `TextBox` solo copiaba al modelo al perder el foco y el clic no llegaba a hacerlo. Ahora copia con cada tecla (`UpdateSourceTrigger=PropertyChanged`), también si el bloque sale de pantalla en una lista larga. Comprobar: editar el último bloque, bajar y subir la lista y Guardar. ¿Se lee bien la lista con 100 o más bloques? ¿La fuente del tiempo (Consolas) y la del texto son cómodas?
7. **Traducir a…** Elige «Inglés» y genera: la etapa «Traduciendo» aparece tras la transcripción y la vista previa sale en inglés; el archivo se llama `video.en.srt`.
8. **Leer en voz alta.** Marca la casilla, genera y guarda: aparece «Generando la voz» y queda `video.voz.wav` (o `video.en.voz.wav`) junto a los subtítulos. Defecto visto el 24-oct: con un `voz.wav` de entrada, la voz se guardaba como `voz.wav`, encima del audio original; ahora la voz lleva siempre `.voz.wav` y ninguna salida puede coincidir con la entrada (si pasara, se le añade `.subtitular`). Comprobar con un `.wav` de entrada que el original sigue intacto. Ábrelo: cada frase debe empezar más o menos cuando aparece su subtítulo.
9. **Errores.** Cambia de nombre la carpeta `modulo-subtitulos` y genera: la barra roja debe decir qué módulo falta y dónde se buscó, en español. Vuelve a ponerle el nombre.
10. **Tema.** Cambia Windows de claro a oscuro con la ventana abierta: colores y legibilidad.

## Identidad (pulido visual, 24-oct)

Lo que solo se juzga a ojo. Sin captura: la app no tiene `--captura`.

1. **Tres segundos.** Abre `Subtitular.exe`: cabecera con la burbuja terracota, «Subtitular» en Sitka Display y el lema «Lo que se dice, escrito». ¿Se nota que no es Rodaje ni una app genérica de WinUI? ¿El icono de la barra de tareas y del exe se reconoce a 16 px?
2. **Claro y oscuro.** Cambia el tema de Windows con la ventana abierta: marfil con terracota en claro, carbón cálido con ámbar en oscuro. ¿Se lee bien el texto de la etapa, el tiempo de cada bloque (naranja oscuro/claro) y los botones?
3. **Estado vacío.** Sin generar nada, la tarjeta «Subtítulos» muestra la burbuja dibujada y la frase de qué hacer. Al generar desaparece; al soltar otro archivo vuelve.
4. **Progreso.** Pulsa Generar: la barra late indeterminada mientras carga el modelo («Preparando · 0:03»), luego avanza suave con «Transcribiendo · 45 % · 0:12 · quedan ~0:15». Al terminar aparece un tic naranja a la derecha, que se queda hasta la siguiente acción. ¿La estimación se acerca al tiempo real?
5. **Ventana.** Mueve y redimensiona la ventana, ciérrala y ábrela: vuelve al mismo sitio y tamaño (`%LOCALAPPDATA%\Dathinel\Subtitular\ventana.json`). No se deja bajar de 800×700. Si la cierras maximizada, se queda el último tamaño normal.
6. **Acerca de.** Arriba a la derecha: versión, licencia y el crédito de Rodaje; los dos enlaces abren GitHub en el navegador.
7. **Ritmo.** ¿Las tres tarjetas (archivo, ajustes, subtítulos) con sombra suave se ven ordenadas o recargadas? Con Tab, ¿se ve el foco en cada control?

## Calidad de vida (1.0.1)

Lo que la CI no puede comprobar (la lógica tiene pruebas en `BatchTests` y `PreferencesTests`).

1. **Recordar.** Elige un archivo de una carpeta nueva, pon «Inglés», VTT, «Traducir a… Francés», marca la voz y una carpeta de salida; cierra y abre: todo debe seguir igual, y «Elegir…» debe abrirse en la carpeta del último archivo (`%LOCALAPPDATA%\Dathinel\Subtitular\preferencias.json`). Borra la carpeta de salida y vuelve a abrir: el campo queda vacío (junto al video).
2. **Varios archivos.** Arrastra tres videos a la vez (o elígelos con Ctrl en el diálogo): la cabecera dice «3 archivos en cola» y debajo sus nombres. Generar: la barra dice «Archivo 1 de 3 · nombre · Transcribiendo · 45 %…», late indeterminada al empezar cada archivo, y al final «3 archivos listos» con la vista previa del último; en la carpeta deben estar los tres `.srt`. Mezcla un `.txt` en lo que sueltas: barra roja con su nombre y los videos siguen cargados. Renombra el segundo video antes de generar: «2 archivos listos, 1 con error: …» y los otros dos guardados.
3. **Cancelar en cola.** Con tres archivos, cancela durante el segundo: «Operación cancelada.» en menos de un segundo, el primero ya guardado, y en el Administrador de tareas no queda ningún `ffmpeg.exe` ni `Rodaje.Module.*.exe`.
4. **Abrir la carpeta.** Con la casilla marcada, al guardar un archivo se abre el Explorador con el `.srt` seleccionado; con varios, se abre la carpeta. Desmarcada, no se abre nada.
5. **Atajos.** `Ctrl+O` abre el diálogo de elegir; `Ctrl+Entrar` genera (y no hace nada mientras ya está generando o sin archivo). Con el foco dentro de un bloque de la vista previa, `Ctrl+Entrar` también debe generar y no meter un salto de línea.

## Revisión de bloque (carpetas, «Guardar sin revisar», voz)

La lógica tiene pruebas en `MediaSelectionTests`, `PreferencesTests` y `SpeechTests`; esto es lo que solo se ve con la ventana.

1. **Soltar una carpeta.** Arrastra una carpeta con tres videos, un `.txt` y una subcarpeta con otro video: la cabecera dice «3 archivos en cola» (los de la carpeta, por nombre; el de la subcarpeta no entra) y no sale ningún aviso por el `.txt` (dentro de una carpeta se ignora en silencio). Suelta una carpeta sin medios: barra roja «La carpeta «x» no tiene videos ni audios.» y la cola no cambia. Suelta a la vez un video suelto y una carpeta: entran los dos. Arrastra un adjunto desde Outlook o un archivo desde dentro de un zip abierto en el Explorador: barra roja «Lo que has soltado no es un archivo ni una carpeta del disco…» (o el cursor de «no se puede soltar», según lo que ofrezca el origen).
2. **Guardar sin revisar.** Marca la casilla, suelta un solo video y genera: al terminar no hay que pulsar Guardar; sale «Guardado: …srt» en verde, la vista previa queda rellena y, con la casilla de la carpeta, se abre el Explorador. Cierra y abre: la casilla sigue marcada. Desmárcala: vuelve el flujo de siempre («Revisa el texto y pulsa Guardar»).
3. **Guardar sin regenerar la voz.** Con «Leer en voz alta» y «Guardar sin revisar» (o una cola de dos), genera; al terminar pulsa Guardar sin tocar nada: debe guardar al momento (sin la etapa «Generando la voz») y la fecha del `.voz.wav` no cambia. Corrige una palabra y vuelve a Guardar: ahora sí regenera la voz y el WAV cambia. Vuelve a Guardar sin tocar nada: otra vez al momento.

## Aviso de versión nueva

La lógica (tag, comparación, 24 h, «Más tarde», Lanzador) tiene pruebas sin red en `UpdateCheckTests` y `LauncherTests`; esto es lo que solo se ve con la ventana y GitHub de verdad. Mientras la última release sea la 1.0.0 y esta compilación sea la 1.0.x, la franja no sale: para verla, baja `<Version>` en `Directory.Build.props` a `0.9.0` y publica.

1. **Arranque sin franja.** Con la app al día, abre y cierra: no sale nada arriba, la ventana pinta igual de rápido y en `preferencias.json` queda `lastUpdateCheck` con la hora (UTC). Vuelve a abrir antes de 24 h: la hora no cambia (no consulta). Sin red (desconecta el wifi): arranca igual, sin aviso ni barra roja.
2. **Franja.** Con una compilación `0.9.0`, al abrir debe salir arriba, encima de la cabecera, la franja con el borde del acento: «Subtitular 1.0.0 disponible», «Ver novedades», «Más tarde» y, solo si `%LOCALAPPDATA%\Dathinel\Lanzador\Lanzador.exe` existe (o `SUBTITULAR_LANZADOR` apunta a él), «Abrir el Lanzador». ¿Se ve discreta y en claro y oscuro? ¿Empuja el resto hacia abajo sin solaparse con «Acerca de»?
3. **Ver novedades.** Abre en el navegador la página de la release (`github.com/Dathinel/subtitular/releases/tag/v1.0.0`). **Abrir el Lanzador** abre el Lanzador y la ventana sigue abierta.
4. **Más tarde.** La franja se va y en `preferencias.json` queda `skippedVersion: "1.0.0"`. Cierra y abre pasadas 24 h (o borra `lastUpdateCheck` del archivo): no vuelve a salir. Cambia a mano `skippedVersion` a `0.9.5`: vuelve a salir.
5. **Buscar actualizaciones.** En Ajustes, el enlace consulta al momento aunque no hayan pasado 24 h y aunque la versión esté descartada: con la `0.9.0`, sale la franja; con la app al día, la barra azul «No hay nada nuevo: Subtitular 1.0.0 es la última versión.»; sin red, la barra amarilla «No se pudo consultar…» (en menos de 6 s: el límite es de 5). Mientras consulta, el enlace queda apagado y la ventana responde.
6. **Casilla.** Desmarca «Avisar cuando haya una versión nueva», cierra y abre: la casilla sigue desmarcada y `lastUpdateCheck` no cambia aunque hayan pasado 24 h; «Buscar actualizaciones» sigue funcionando.

## Resultado del repaso del 1-nov (Claude con el ratón, `dist\Subtitular`)

Pasaron: arranque, «Buscar actualizaciones» («No hay nada nuevo: Subtitular 1.0.0 es la última versión»), `Ctrl+O` con ruta escrita, cabecera con duración y «sin subtítulos al lado», Generar con «Detectar» (dos bloques en «en» de un video de prueba con tono), Guardar deja el `.srt` junto al video y abre el Explorador con el archivo elegido. Sin probar: cancelar, traducir, voz, cola de varios, tema claro.
