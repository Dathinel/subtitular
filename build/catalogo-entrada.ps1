# Genera el JSON de la entrada «app» para el catálogo firmado de Rodaje.
# Deja el JSON en dist\catalogo-subtitular.json (sin firmar).
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File build\catalogo-entrada.ps1 [-Version 1.0.0] [-Zip dist\Subtitular-1.0.0-win-x64.zip] [-Salida dist\catalogo-subtitular.json]
[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$Zip = '',
    [string]$Salida = ''
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

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
    $Salida = Join-Path $Raiz 'dist\catalogo-subtitular.json'
}

$dirSalida = Split-Path -Parent $Salida
if (-not (Test-Path -LiteralPath $dirSalida)) {
    New-Item -ItemType Directory -Force -Path $dirSalida | Out-Null
}

if (-not $Zip) {
    $Zip = Join-Path $dirSalida "Subtitular-$Version-win-x64.zip"
}

if (-not (Test-Path -LiteralPath $Zip)) {
    throw "No se encontró el archivo zip de distribución: $Zip (corre antes build\distribuir.ps1)."
}

$sha256 = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item -LiteralPath $Zip).Length
$url = "https://github.com/Dathinel/subtitular/releases/download/v$Version/Subtitular-$Version-win-x64.zip"

# Módulos declarados por Subtitular y sus modelos desde sus paquete.json
$modulosDeclarados = @(
    @{ Carpeta = 'Rodaje.Module.Subtitles'; Id = 'modulo-subtitulos' },
    @{ Carpeta = 'Rodaje.Module.Translate'; Id = 'modulo-traducir' },
    @{ Carpeta = 'Rodaje.Module.Voice'; Id = 'modulo-voz' }
)

$modulosInfo = New-Object System.Collections.Generic.List[object]
foreach ($m in $modulosDeclarados) {
    $paqueteJson = Join-Path $Raiz "externo\rodaje\modules\$($m.Carpeta)\paquete.json"
    if (Test-Path -LiteralPath $paqueteJson) {
        $data = Get-Content -LiteralPath $paqueteJson -Raw -Encoding UTF8 | ConvertFrom-Json
        $modelos = @()
        if ($data.module.models) {
            $modelos = @($data.module.models | ForEach-Object {
                [ordered]@{
                    id = $_.id
                    file = $_.file
                    size = [long]$_.size
                    sha256 = $_.sha256.ToLowerInvariant()
                    license = $_.license
                    url = $_.url
                }
            })
        }
        $modulosInfo.Add([ordered]@{
            id = $data.id
            version = $data.version
            name = $data.name
            description = $data.description
            models = $modelos
        })
    }
}

$entrada = [ordered]@{
    id = 'subtitular'
    version = $Version
    kind = 'app'
    url = $url
    sha256 = $sha256
    size = $size
    modulos = @('modulo-subtitulos', 'modulo-traducir', 'modulo-voz')
    modules = $modulosInfo.ToArray()
}

$jsonTexto = $entrada | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($Salida, $jsonTexto, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Entrada de catálogo generada: $Salida" -ForegroundColor Green
Write-Host "  Id: $($entrada.id) $($entrada.version)"
Write-Host "  URL: $url"
Write-Host "  SHA256: $sha256"
Write-Host "  Tamaño: $([math]::Round($size / 1MB, 2)) MB"
