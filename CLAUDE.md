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
- Fase 2 hecha, pendiente de verificación independiente: `Subtitular.App` (WinUI 3, AOT, sin empaquetar) sobre `SubtitleJob`; `SubtitleTranslator` y `SpeechSynthesizer` en Core; CLI con `--traducir` y `--voz`; 27 pruebas rápidas + 2 largas. Cifras en `docs/informe-fase-2.md`; lo que juzga Daniel, en `docs/pruebas-a-mano.md`.
- **Siguiente:** verificación de la fase 2 por otro agente; luego fase 3 (zip en `dist\`, entrada `app` en el catálogo firmado, release).
