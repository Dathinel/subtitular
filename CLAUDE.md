# Subtitular

Herramienta de IA aparte de Rodaje, con interfaz propia. Repositorio público `Dathinel/subtitular`, GPL-3.0-only. El plan común de las tres herramientas está en `..\PLAN.md` (carpeta `Herramientas-IA`, que no se versiona).

## Cómo se trabaja aquí

- **.NET:** siempre con `build\dotnet.cmd <args>`; `build\preparar-entorno.ps1` deja el SDK y FFmpeg en `.entorno\`. Los dos llaman a los scripts de Rodaje del submódulo `externo/rodaje`: no se copian. `build\publicar.cmd` publica App y Cli con AOT en `dist\Subtitular\`.
- **Código de Rodaje:** por `ProjectReference` a `externo/rodaje/src/...`, nunca copiado. Mover el commit fijado del submódulo es un commit propio. De `Rodaje.App` solo se tomó la receta del csproj (WinUI 3 sin empaquetar + AOT) y el arranque con `Main` propio.
- **Worktrees** en `.claude\worktrees\` (ignorado). Un commit por tarea y push.
- **Módulos:** `Modules.Find` los busca, en orden, en `SUBTITULAR_MODULOS`, la carpeta del Lanzador (`%LOCALAPPDATA%\Dathinel\Comun\modulos\modulo-*\<versión>`, la más nueva), los de Rodaje (`%LOCALAPPDATA%\Rodaje\complementos\modulos\modulo-*`, `RODAJE_MODULOS`) y `modulos\` junto al exe (`ModulesFindTests`). Las pruebas largas (`RODAJE_PRUEBAS_LARGAS=1`) necesitan los tres módulos con sus modelos y el corpus FLEURS en `RODAJE_ENTORNO`.

## Estado (1-nov)

- Fases 0-3 cerradas y verificadas (`docs/informe-fase-1.md` a `-3.md`): Core, CLI, ventana WinUI 3 con AOT, `build\distribuir.ps1` (zip AOT, instalador Inno Setup, `catalogo-subtitular.json`, `notas-version.md`, `SHA256SUMS`), `ci.yml` y `release.yml` (`workflow_dispatch`, `--draft`). Pendiente legal: términos del Windows App SDK (`docs/legal.md`).
- Submódulo `externo/rodaje` fijado a PR #445 (`0436ad75`, `IndexEntryKind.App`).
- **1.0.0 publicada** el 24-oct: release `v1.0.0` en GitHub (zip, instalador y `SHA256SUMS`), `-warnaserror` sin avisos, 34 pruebas en verde con la larga (WER 2,13 %).
- **PR #1 unido en `main`** el 31-oct: cola de archivos, abrir la carpeta al terminar, atajos, preferencias y el arreglo de la CI (`Get-FileHash`). Todo está en «[Sin publicar]» del `CHANGELOG.md`; `Directory.Build.props` sigue en **1.0.0** a propósito: se sube a 1.0.1 y se cierra «[Sin publicar]» en el mismo commit que publique la 1.0.1.
- **Unidos también el 31-oct:** #2 (aviso de versión nueva: consulta diaria a GitHub, franja en la ventana, preferencia y «Buscar actualizaciones») y #3 (revisión de bloque: sin repetidos en la cola y sufijo «-2» en salidas iguales, cancelar conserva lo hecho, preferencias guardadas al cambiar, `Resolve-Path` en `catalogo-entrada.ps1`). AOT publicado en `dist\Subtitular\` (31-oct, 21:02) con todo lo anterior.
- **#4 unido el 1-nov:** soltar una carpeta encola sus videos y audios (sin subcarpetas; `MediaInfo.Select`, `MediaSelection.Warning`), «Guardar sin revisar» (casilla en Ajustes, `SaveWithoutReview`, un solo archivo pasa por la cola), Guardar sin regenerar el WAV si la voz leería lo mismo (`SpeechSynthesizer.SameSpeech`), JSON de estado atómicos (`JsonFile`), `ResultFolder` en la App y `-Salida` relativa en `catalogo-entrada.ps1`. 75 pruebas (5 solo en Windows). AOT republicado el 1-nov en `dist\Subtitular\` y zip `dist\Subtitular-1.0.0-win-x64.zip` (27,4 MB). Resultado del repaso con ratón del 1-nov en `docs/pruebas-a-mano.md`; sin PR abiertos.
- **Siguiente:** Daniel firma `dist\catalogo-subtitular.json` (URL y huella de la release) en `Dathinel/rodaje-catalogo`; lo que salga de `docs/pruebas-a-mano.md` va a la 1.0.1; revisión de bloque de #4 (Cuadrilla, lectura).
