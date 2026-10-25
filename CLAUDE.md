# Subtitular

Herramienta de IA aparte de Rodaje, con interfaz propia. Repositorio público `Dathinel/subtitular`, GPL-3.0-only. El plan común de las tres herramientas está en `..\PLAN.md` (carpeta `Herramientas-IA`, que no se versiona).

## Cómo se trabaja aquí

- **.NET:** siempre con `build\dotnet.cmd <args>`; `build\preparar-entorno.ps1` deja el SDK y FFmpeg en `.entorno\`. Los dos llaman a los scripts de Rodaje del submódulo `externo/rodaje`: no se copian. `build\publicar.cmd` publica App y Cli con AOT en `dist\Subtitular\`.
- **Código de Rodaje:** por `ProjectReference` a `externo/rodaje/src/...`, nunca copiado. Mover el commit fijado del submódulo es un commit propio. De `Rodaje.App` solo se tomó la receta del csproj (WinUI 3 sin empaquetar + AOT) y el arranque con `Main` propio.
- **Worktrees** en `.claude\worktrees\` (ignorado). Un commit por tarea y push.
- **Módulos:** `Modules.Find` los busca en `SUBTITULAR_MODULOS`, junto al exe y en `%LOCALAPPDATA%\Rodaje\complementos\modulos\modulo-*`. Las pruebas largas (`RODAJE_PRUEBAS_LARGAS=1`) necesitan los tres módulos con sus modelos y el corpus FLEURS en `RODAJE_ENTORNO`.

## Estado (24-oct)

- Fase 0 cerrada: esqueleto, licencia y `src\Subtitular.slnx`.
- Fase 1 cerrada: `Subtitular.Core` (decodificar, `Rodaje.Module.Subtitles`, SRT/VTT) y `Subtitular.Cli`; WER 2,13 % (`docs/informe-fase-1.md`).
- Fase 2 cerrada: ventana WinUI 3 con AOT, traducción y voz en Core y CLI (`docs/informe-fase-2.md`).
- Fase 3 cerrada y verificada (`docs/informe-fase-3.md`): `build\distribuir.ps1` deja en `dist\` el zip AOT autocontenido (26,2 MB), el instalador Inno Setup por usuario (16,8 MB), `catalogo-subtitular.json` (entrada `app`), `notas-version.md` y `SHA256SUMS`; `ci.yml` y `release.yml` (solo `workflow_dispatch`, `--draft`). Pendiente legal: términos del Windows App SDK (`docs/legal.md`).
- Submódulo `externo/rodaje` fijado a PR #445 (`0436ad75`, `IndexEntryKind.App`).
- **Siguiente:** Daniel firma la entrada de catálogo en `Dathinel/rodaje-catalogo` y publica la release de GitHub tras revisar el borrador.
