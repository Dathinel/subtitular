# Fase 3: distribución, catálogo e instalador (24-oct-2026)

Construida por agy (gemini-3.8-flash); verificada por Claude (Fable 5.1) con una reconstrucción desde cero.

- `build\distribuir.ps1` publica App y Cli con Native AOT win-x64 y deja en `dist\`: zip autocontenido (exes, runtime del Windows App SDK, LICENSE, LICENCIAS.md, README.txt), `catalogo-subtitular.json` (entrada `app` para `SignedIndex`, con los tres módulos y sus modelos), `notas-version.md` desde el CHANGELOG, `Subtitular-1.0.0-instalador.exe` (Inno Setup 7.1.0 por usuario, bajado a `.entorno` con SHA-256 fijada) y `SHA256SUMS`. Sin rutas de este PC.
- `ci.yml` (compilación Release `-warnaserror` y pruebas, con submódulo) y `release.yml` (solo `workflow_dispatch`; `gh release create --draft`, nunca publica; notas con el aviso del término 2 de la licencia de Rodaje).
- Pruebas: `CatalogoEntradaTests` corre `catalogo-entrada.ps1` sobre un zip de prueba y pasa la entrada por `SignedIndex` (vale en CI, sin `dist\`); `tests\probar-catalogo.ps1` y `tests\probar-descompresion.ps1` sobre el `dist\` real; YAML cargado con PyYAML.

## Cifras medidas en la verificación

- Reconstrucción completa desde cero (`dist\` borrado): 243 s; solo empaquetar (`-SinPublicar`, con descarga de Inno Setup): 36 s.
- `Subtitular-1.0.0-win-x64.zip`: 27.421.118 bytes (26,15 MiB); 239 archivos, 68,5 MiB sin comprimir. `Subtitular.exe` 7,41 MiB y `Subtitular.Cli.exe` 4,01 MiB; el resto es el Windows App SDK autocontenido que WinUI 3 exige (`Microsoft.ui.xaml.dll` 14,6 MiB, `Microsoft.UI.Xaml.Controls.dll` 6,6 MiB, `.pri`, `.winmd` y 110 carpetas de idioma `.mui`). Sin `.pdb` ni `.xml`.
- `Subtitular-1.0.0-instalador.exe`: 17.551.397 bytes (16,74 MiB), por usuario en `%LOCALAPPDATA%\Programs\Subtitular`, con desinstalador.
- Carpeta limpia en `%TEMP%`: `Subtitular.exe` sigue viva a los 3 s sin archivo de error y pinta el primer cuadro en 289 ms; `Subtitular.Cli.exe --help` sale con 0.
- Pruebas rápidas: 29 de 29 (ver `CLAUDE.md`).

## Corregido en la verificación

Rutas absolutas de este PC en `distribuir.ps1`; scripts sin BOM (PowerShell 5.1 escribía `README.txt` y las notas con tildes rotas); prueba xUnit que exigía `dist\` (fallaba en CI); `[UninstallDelete]` que borraba toda la carpeta de instalación; archivos duplicados (`SHA256SUMS.txt`, `*-notas.md`, `herramienta.json` sin lector); crédito a Rodaje ausente en README, LICENCIAS, zip y notas; Windows App SDK, WebView2 e Inno Setup sin anotar en `LICENCIAS.md` y `docs/legal.md`.
