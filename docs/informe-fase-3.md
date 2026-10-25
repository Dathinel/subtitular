# Fase 3: distribución, catálogo e instalador (24-oct-2026)

Hecha por agy (gemini-3.8-flash-medium).

- **`build\distribuir.ps1`**: publica App y Cli con Native AOT win-x64, empaqueta zip autocontenido con licencias, README corto y `herramienta.json`, genera `SHA256SUMS`, extrae `notas-version.md` desde `CHANGELOG.md` e invoca `catalogo-entrada.ps1`.
- **`build\catalogo-entrada.ps1`**: genera `dist\catalogo-subtitular.json` (tipo `app`, PR #445) con SHA-256 y tamaño exactos del zip, URL de release y los 3 módulos declarados (`modulo-subtitulos`, `modulo-traducir`, `modulo-voz`) con sus modelos.
- **Instalador Inno Setup**: `build\instalador\Subtitular.iss` compilado con Inno Setup 7.1.0 de Rodaje; salida `dist\Subtitular-1.0.0-instalador.exe`.
- **CI / CD**: `.github/workflows/ci.yml` (compilación Release `-warnaserror` y pruebas) y `release.yml` (disparado a mano con `workflow_dispatch`, crea release en borrador con `gh release create --draft`). Validados con PyYAML.

## Cifras
- `Subtitular-1.0.0-win-x64.zip`: **26,16 MB** (27.433.570 bytes).
- `Subtitular-1.0.0-instalador.exe`: **16,75 MB** (17.568.140 bytes).
- Arranque en carpeta limpia (`%TEMP%`): `Subtitular.Cli.exe --help` inmediato; `Subtitular.exe` primer cuadro en **339 ms**.
- Pruebas rápidas: **29 de 29** superadas (incluye validación de catálogo contra `SignedIndex`).
- Submódulo `externo/rodaje`: actualizado a commit `0436ad75` (PR #445, `IndexEntryKind.App`).
