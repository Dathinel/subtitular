# Comprueba que el JSON de catálogo tiene la huella real del zip y los campos que SignedIndex exige.
param(
    [string]$CatalogoJson = '',
    [string]$Zip = ''
)

$ErrorActionPreference = 'Stop'
$Raiz = Split-Path -Parent $PSScriptRoot

if (-not $CatalogoJson) {
    $CatalogoJson = Join-Path $Raiz 'dist\catalogo-subtitular.json'
}
if (-not $Zip) {
    $Zip = (Resolve-Path (Join-Path $Raiz 'dist\Subtitular-*-win-x64.zip')).Path
}

if (-not (Test-Path -LiteralPath $CatalogoJson)) { throw "Falta $CatalogoJson" }
if (-not (Test-Path -LiteralPath $Zip)) { throw "Falta $Zip" }

$json = Get-Content -LiteralPath $CatalogoJson -Raw -Encoding UTF8 | ConvertFrom-Json
$zipHash = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
$zipSize = (Get-Item -LiteralPath $Zip).Length

# Validar campos requeridos por SignedIndex
if ($json.id -ne 'subtitular') { throw "id esperado 'subtitular', pero fue '$($json.id)'" }
if (-not $json.version) { throw "Falta el campo 'version'" }
if ($json.kind -ne 'app') { throw "kind esperado 'app', pero fue '$($json.kind)'" }
if ($json.url -notmatch '^https://github\.com/Dathinel/subtitular/releases/download/v[^/]+/[^/]+\.zip$') {
    throw "URL no cumple con la política de descarga de Dathinel: $($json.url)"
}
if ($json.sha256 -ne $zipHash) {
    throw "La huella sha256 del catálogo ($($json.sha256)) no coincide con la del zip ($zipHash)"
}
if ($json.size -ne $zipSize) {
    throw "El tamaño del catálogo ($($json.size)) no coincide con el del zip ($zipSize)"
}

# Validar módulos declarados
$modulosRequeridos = @('modulo-subtitulos', 'modulo-traducir', 'modulo-voz')
foreach ($m in $modulosRequeridos) {
    if ($json.modulos -notcontains $m) {
        throw "El catálogo no declara el módulo '$m'"
    }
}

Write-Host "Prueba de catálogo superada:" -ForegroundColor Green
Write-Host "  Id: $($json.id)"
Write-Host "  Versión: $($json.version)"
Write-Host "  Kind: $($json.kind)"
Write-Host "  SHA256: $($json.sha256) (coincide con zip)"
Write-Host "  Tamaño: $($json.size) bytes (coincide con zip)"
Write-Host "  Módulos: $($json.modulos -join ', ')"
