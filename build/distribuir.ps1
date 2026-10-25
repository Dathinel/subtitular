# Distribución de Subtitular (publicación AOT, zip, catálogo, notas e instalador).
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File build\distribuir.ps1 [-Version 1.0.0] [-Salida dist] [-SinPublicar] [-SinInstalador]
[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$Salida = '',
    [switch]$SinPublicar,
    [switch]$SinInstalador
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$Raiz = Split-Path -Parent $PSScriptRoot

if (-not $Version) {
    $propsPath = Join-Path $Raiz 'Directory.Build.props'
    if (Test-Path -LiteralPath $propsPath) {
        $props = [xml](Get-Content -LiteralPath $propsPath -Raw -Encoding UTF8)
        $Version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
    }
    if (-not $Version) { throw 'No se pudo determinar la versión desde Directory.Build.props.' }
}

if (-not $Salida) {
    $Salida = Join-Path $Raiz 'dist'
}

$CarpetaApp = Join-Path $Salida 'Subtitular'
New-Item -ItemType Directory -Force -Path $Salida, $CarpetaApp | Out-Null

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Distribución de Subtitular $Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Entorno propio (SDK, Inno Setup): el mismo que build\dotnet.cmd si no viene RODAJE_ENTORNO.
if (-not $env:RODAJE_ENTORNO) { $env:RODAJE_ENTORNO = Join-Path $Raiz '.entorno' }

# 1. Compilación y publicación Native AOT (App y Cli)
if (-not $SinPublicar) {
    Write-Host "`n[1/5] Publicando Subtitular.App y Subtitular.Cli con Native AOT..." -ForegroundColor Yellow
    $cmdPublicar = Join-Path $PSScriptRoot 'publicar.cmd'
    & cmd /c "call `"$cmdPublicar`""
    if ($LASTEXITCODE -ne 0) {
        throw "Falló la publicación con Native AOT (código $LASTEXITCODE)."
    }
}

# Verificar que existen los ejecutables publicados
$exeApp = Join-Path $CarpetaApp 'Subtitular.exe'
$exeCli = Join-Path $CarpetaApp 'Subtitular.Cli.exe'
foreach ($exe in $exeApp, $exeCli) {
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "No se encontró el ejecutable requerido: $exe"
    }
}

# 2. Documentos y manifiesto de uso dentro de la carpeta
Write-Host "`n[2/5] Copiando licencias, manifiesto y README de uso..." -ForegroundColor Yellow
Copy-Item -LiteralPath (Join-Path $Raiz 'LICENSE') -Destination (Join-Path $CarpetaApp 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $Raiz 'LICENCIAS.md') -Destination (Join-Path $CarpetaApp 'LICENCIAS.md') -Force

$readmeUso = @"
Subtitular $Version
==================

Subtítulos automáticos para Windows, en tu PC y sin conexión.

Basado en el editor de video de código abierto de Dathinel / Rodaje (https://github.com/Dathinel/rodaje),
un proyecto libre y gratuito.

Contenido de esta distribución:
- Subtitular.exe: Aplicación gráfica en WinUI 3 (haz doble clic para iniciar).
- Subtitular.Cli.exe: Herramienta de línea de comandos para scripts y automatización.
- LICENSE / LICENCIAS.md: Licencia GPL-3.0-only y avisos de componentes de terceros.

Uso rápido (App):
1. Ejecuta Subtitular.exe.
2. Arrastra un video o audio a la ventana (o pulsa «Elegir archivo…»).
3. Elige el idioma de transcripción, traducción opcional y/o lectura en voz alta.
4. Pulsa «Generar subtítulos». Al finalizar puedes editar los bloques y pulsar «Guardar».

Uso rápido (CLI):
  Subtitular.Cli.exe video.mp4 --idioma es
  Subtitular.Cli.exe video.mp4 --traducir en --voz --salida ./subtitulos/
  Subtitular.Cli.exe --help

Módulos de IA de Rodaje:
Subtitular requiere los módulos de IA:
- modulo-subtitulos (Whisper)
- modulo-traducir (Qwen3)
- modulo-voz (Kokoro)
Se buscan en la carpeta de la app, en la variable SUBTITULAR_MODULOS o en
%LOCALAPPDATA%\Rodaje\complementos\modulos\modulo-*.

Licencia: GNU GPL versión 3 (GPL-3.0-only).
Repositorio: https://github.com/Dathinel/subtitular
"@
[System.IO.File]::WriteAllText((Join-Path $CarpetaApp 'README.txt'), $readmeUso, (New-Object System.Text.UTF8Encoding($false)))

# Fuera símbolos y documentación XML de los proyectos referenciados: no hacen falta para ejecutar.
Get-ChildItem -LiteralPath $CarpetaApp -Recurse -File | Where-Object { $_.Extension -in '.pdb', '.xml' } | Remove-Item -Force

# 3. Empaquetar zip autocontenido
Write-Host "`n[3/5] Creando zip autocontenido..." -ForegroundColor Yellow
$zipNombre = "Subtitular-$Version-win-x64.zip"
$zipSalida = Join-Path $Salida $zipNombre
if (Test-Path -LiteralPath $zipSalida) {
    Remove-Item -LiteralPath $zipSalida -Force
}

$archivoZip = [System.IO.Compression.ZipFile]::Open($zipSalida, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $padre = (Resolve-Path -LiteralPath $CarpetaApp).Path.TrimEnd('\') + '\'
    foreach ($f in Get-ChildItem -LiteralPath $CarpetaApp -Recurse -File) {
        $nombreRelativo = $f.FullName.Substring($padre.Length).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archivoZip, $f.FullName, $nombreRelativo, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $archivoZip.Dispose()
}

$tamZip = (Get-Item -LiteralPath $zipSalida).Length
Write-Host "  Zip creado: $zipSalida ($([math]::Round($tamZip / 1MB, 2)) MB)" -ForegroundColor Green

# 4. Generar notas de la versión desde CHANGELOG.md y entrada de catálogo
Write-Host "`n[4/5] Extrayendo notas y generando entrada de catálogo..." -ForegroundColor Yellow
$changelogPath = Join-Path $Raiz 'CHANGELOG.md'
$notasTexto = ''
if (Test-Path -LiteralPath $changelogPath) {
    $lineas = Get-Content -LiteralPath $changelogPath -Encoding UTF8
    $capturando = $false
    $lineasNotas = New-Object System.Collections.Generic.List[string]
    foreach ($l in $lineas) {
        if ($l -match "^##\s+\[$([regex]::Escape($Version))\]") {
            $capturando = $true
            $lineasNotas.Add($l)
            continue
        }
        if ($capturando -and $l -match '^##\s+\[') {
            break
        }
        if ($capturando) {
            $lineasNotas.Add($l)
        }
    }
    if ($lineasNotas.Count -gt 0) {
        $notasTexto = ($lineasNotas -join "`n").Trim()
    }
}

if (-not $notasTexto) {
    $notasTexto = "# Subtitular $Version`n`nLanzamiento inicial de Subtitular $Version."
}
# Término 2 de la licencia de Rodaje: aviso prominente en la descripción de la release (release.yml usa estas notas).
$notasTexto = "**Basado en el editor de video de código abierto de Dathinel / Rodaje (https://github.com/Dathinel/rodaje)**, un proyecto libre y gratuito.`n`n$notasTexto"

$notasSalida = Join-Path $Salida 'notas-version.md'
[System.IO.File]::WriteAllText($notasSalida, $notasTexto, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "  Notas de versión: $notasSalida" -ForegroundColor Green

# Generar entrada de catálogo llamando a catalogo-entrada.ps1
$catalogoSalida = Join-Path $Salida 'catalogo-subtitular.json'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'catalogo-entrada.ps1') -Version $Version -Zip $zipSalida -Salida $catalogoSalida
if ($LASTEXITCODE -ne 0) {
    throw "Falló la generación de catalogo-subtitular.json."
}

# 5. Instalador con Inno Setup portable en RODAJE_ENTORNO; si falta se baja con SHA-256 fijada (misma versión y
#    huella que build\instalador.ps1 de Rodaje), así release.yml también lo arma en GitHub Actions.
$instaladorSalida = Join-Path $Salida "Subtitular-$Version-instalador.exe"
if (-not $SinInstalador) {
    Write-Host "`n[5/5] Compilando instalador Inno Setup..." -ForegroundColor Yellow
    $innoVersion = '7.1.0'
    $inno = Join-Path $env:RODAJE_ENTORNO "innosetup-$innoVersion"
    $iscc = Join-Path $inno 'ISCC.exe'
    if (-not (Test-Path -LiteralPath $iscc)) {
        Write-Host "  Bajando Inno Setup $innoVersion a $inno..."
        $bajado = Join-Path ([System.IO.Path]::GetTempPath()) "innosetup-$innoVersion-x64.exe"
        Invoke-WebRequest -Uri "https://github.com/jrsoftware/issrc/releases/download/is-$($innoVersion -replace '\.', '_')/innosetup-$innoVersion-x64.exe" -OutFile $bajado -UseBasicParsing
        $sha = (Get-FileHash -LiteralPath $bajado -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sha -ne '0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f') {
            Remove-Item -LiteralPath $bajado -Force
            throw "Inno Setup bajado no coincide con su SHA-256 ($sha)."
        }
        $p = Start-Process -FilePath $bajado -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/PORTABLE=1', '/NOICONS', "/DIR=`"$inno`"" -PassThru -Wait
        Remove-Item -LiteralPath $bajado -Force
        if ($p.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $iscc)) { throw "No se pudo desempacar Inno Setup (código $($p.ExitCode))." }
    }

    & $iscc '/Q' "/DVersion=$Version" "/DOrigen=$CarpetaApp" "/DLicencia=$(Join-Path $Raiz 'LICENSE')" "/O$Salida" (Join-Path $PSScriptRoot 'instalador\Subtitular.iss')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $instaladorSalida)) {
        throw "ISCC terminó con código $LASTEXITCODE; no se creó el instalador."
    }
    Write-Host "  Instalador creado: $instaladorSalida ($([math]::Round((Get-Item -LiteralPath $instaladorSalida).Length / 1MB, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "`n[5/5] Instalador omitido (-SinInstalador)." -ForegroundColor Gray
}

# 6. Generar SHA256SUMS
$artefactos = New-Object System.Collections.Generic.List[string]
if (Test-Path -LiteralPath $zipSalida) { $artefactos.Add($zipSalida) }
if (Test-Path -LiteralPath $instaladorSalida) { $artefactos.Add($instaladorSalida) }
if (Test-Path -LiteralPath $catalogoSalida) { $artefactos.Add($catalogoSalida) }

$sumsLineas = @($artefactos | ForEach-Object {
    $sha = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    $nombre = Split-Path -Leaf $_
    "$sha  $nombre"
})

$sumsSalida = Join-Path $Salida 'SHA256SUMS'
[System.IO.File]::WriteAllLines($sumsSalida, [string[]]$sumsLineas, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "`n========================================" -ForegroundColor Green
Write-Host " Distribución completada con éxito" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
foreach ($linea in $sumsLineas) {
    Write-Host "  $linea"
}
