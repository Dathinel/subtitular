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
