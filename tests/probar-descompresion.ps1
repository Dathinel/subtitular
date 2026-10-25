# Prueba que el zip descomprimido en una carpeta limpia arranca la App y la Cli (--help)
param(
    [string]$Zip = ''
)

$ErrorActionPreference = 'Stop'
$Raiz = Split-Path -Parent $PSScriptRoot

if (-not $Zip) {
    $Zip = (Resolve-Path (Join-Path $Raiz 'dist\Subtitular-*-win-x64.zip')).Path
}

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("subtitular-test-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
try {
    Write-Host "Descomprimiendo en carpeta limpia: $tempDir"
    Expand-Archive -LiteralPath $Zip -DestinationPath $tempDir

    $cliExe = Join-Path $tempDir 'Subtitular.Cli.exe'
    if (-not (Test-Path -LiteralPath $cliExe)) { throw "No se encontró $cliExe en el zip" }

    Write-Host "Ejecutando Subtitular.Cli.exe --help..."
    $salidaCli = & $cliExe --help
    if ($LASTEXITCODE -ne 0) { throw "Subtitular.Cli.exe --help falló con código $LASTEXITCODE" }
    Write-Host "Subtitular.Cli.exe --help ejecutó correctamente:" -ForegroundColor Green
    $salidaCli | ForEach-Object { Write-Host "  $_" }

    $appExe = Join-Path $tempDir 'Subtitular.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw "No se encontró $appExe en el zip" }

    Write-Host "Ejecutando Subtitular.exe --medir..."
    $medida = Join-Path $tempDir 'medida.txt'
    $proc = Start-Process -FilePath $appExe -ArgumentList @('--medir', $medida) -PassThru -Wait
    if ($proc.ExitCode -ne 0) { throw "Subtitular.exe salió con código $($proc.ExitCode)" }

    if (Test-Path -LiteralPath $medida) {
        $ms = (Get-Content -LiteralPath $medida -Raw).Trim()
        Write-Host "Subtitular.exe arrancó y pintó su primer cuadro en $ms ms." -ForegroundColor Green
    } else {
        throw "No se generó el archivo de medida de primer cuadro."
    }

    Write-Host "Prueba de descompresión limpia superada con éxito." -ForegroundColor Green
} finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
