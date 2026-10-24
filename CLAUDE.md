# Subtitular

Herramienta de IA aparte de Rodaje, con interfaz propia. Repositorio público `Dathinel/subtitular`, GPL-3.0-only. El plan común de las tres herramientas está en `..\PLAN.md` (carpeta `Herramientas-IA`, que no se versiona).

## Cómo se trabaja aquí

- **.NET:** siempre con `build\dotnet.cmd <args>`; `build\preparar-entorno.ps1` deja el SDK y FFmpeg en `.entorno\`. Los dos llaman a los scripts de Rodaje del submódulo `externo/rodaje`: no se copian.
- **Código de Rodaje:** por `ProjectReference` a `externo/rodaje/src/...`, nunca copiado. Mover el commit fijado del submódulo es un commit propio.
- **Worktrees** en `.claude\worktrees\` (ignorado). Un commit por tarea y push.

## Estado (24-oct)

- Fase 0 cerrada: esqueleto, licencia y `src\Subtitular.slnx`.
- Fase 1 cerrada: `Subtitular.Core` (decodificar con FFmpeg, llamar a `Rodaje.Module.Subtitles`, escribir SRT/VTT) y `Subtitular.Cli` con AOT (4,0 MB); 16 pruebas y WER 2,13 % en el corpus. Cifras en `docs/informe-fase-1.md`. El módulo se publica desde `externo/rodaje/modules/Rodaje.Module.Subtitles` y se pasa con `--modulo` o `SUBTITULAR_MODULOS`.
- **Siguiente:** fase 2, interfaz WinUI 3 sobre `Subtitular.Core`; aparte, `--traducir` y `--voz` (`..\PLAN.md` y `..\encargos\`).
