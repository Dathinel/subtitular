# Pulido visual e identidad (24-oct-2026)

Hecho por Claude (Fable 5.1) en el worktree `pulido`, rama `pulido-visual`, sobre main `11aee41` (fase 3).

- **Identidad:** terracota `#A3471A` sobre marfil `#F7F2E8` en claro; ámbar `#E39A4C` sobre carbón cálido `#1F1B17` en oscuro (sigue el tema de Windows; contraste AA en los dos: 5,3:1 y 7,3:1). Título, lema («Lo que se dice, escrito») y cabeceras de sección en **Sitka Display** (fuente del sistema, sin OFL que incluir). Icono propio: burbuja de diálogo con una línea de texto y una onda de voz (`assets/marca/subtitular.svg` y `-pequeno.svg`; `build/generar-iconos.ps1` genera el `.ico` de 16 a 256 px con el ffmpeg del entorno). Va en el exe, en la barra de título y en la barra de tareas (`AppWindow.SetIcon`).
- **Ventana:** título «Subtitular», mínimo 800×700, recuerda tamaño y posición en `%LOCALAPPDATA%\Dathinel\Subtitular\ventana.json` (`WindowPlacement` en Core; si la pantalla guardada ya no existe, vuelve al centro). Comprobado con el exe publicado: cierra en 150,90 1100×760 y reabre ahí; no baja de 800×700.
- **Estado vacío:** la burbuja dibujada en XAML y la frase de qué hacer; desaparece al generar.
- **Progreso:** barra indeterminada mientras carga el modelo («Preparando · 0:03»), después avanza con animación de 300 ms y el texto «Transcribiendo · 45 % · 0:12 · quedan ~0:15» (`JobProgress.Status` y `FormatClock` en Core); al terminar aparece un tic ámbar con rebote. Tres tarjetas con sombra (`ThemeShadow`), rejilla de 8 px, Guardar como botón de acento.
- **Acerca de** (`HyperlinkButton` en la cabecera, término 1 de la licencia de Rodaje): nombre, versión del ensamblado (la de `Directory.Build.props`), GPLv3 con enlace al repositorio y el crédito literal «Basado en el editor de video de código abierto de Dathinel / Rodaje (https://github.com/Dathinel/rodaje)» como enlace que abre el repositorio. Casilla marcada en `docs/legal.md`.
- **Defecto encontrado y arreglado:** con AOT, `(OverlappedPresenter)AppWindow.Presenter` lanza `InvalidCastException` (con JIT funciona) y la app moría al arrancar; hay que usar `AppWindow.Presenter.As<OverlappedPresenter>()`.

## Cifras

- `build\dotnet.cmd build src\Subtitular.slnx -c Release -warnaserror`: 0 advertencias. `build\publicar.cmd`: sin avisos de recorte ni de CsWinRT.
- Pruebas rápidas: **31 de 31** (28 anteriores + ventana, reloj y estado del progreso). La 32.ª, `CatalogoEntradaTests` de la fase 3, pide el zip de `build\distribuir.ps1`, que no se generó en este worktree.
- `Subtitular.exe`: 7.771.648 → **7.969.280 bytes** (+193 KB: icono, estilos, animaciones y Acerca de); `Subtitular.Cli.exe` 4.222.976.
- Arranque hasta el primer cuadro (`--medir`, exe publicado, tres veces): **266 / 256 / 286 ms** (antes, en la misma sesión: 327 ms). Límite: 1 s.

## Queda

- Juzgar a ojo (`docs/pruebas-a-mano.md`, bloque «Identidad»). Sin captura automática: la app no tiene `--captura`.
- La barra de título sigue siendo la del sistema (sin teñir); si Daniel quiere la cabecera dentro de la barra, es `ExtendsContentIntoTitleBar` y regiones de arrastre.
