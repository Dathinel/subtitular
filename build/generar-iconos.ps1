# Genera assets\marca\subtitular.ico (16-256 px, PNG dentro del .ico) desde los SVG de assets\marca con el ffmpeg
# (librsvg) de RODAJE_ENTORNO o .entorno. Hasta 32 px se usa subtitular-pequeno.svg. El .ico se versiona: compilar no lo necesita.
#   powershell -NoProfile -ExecutionPolicy Bypass -File build\generar-iconos.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$marca = Join-Path $raiz 'assets\marca'
$entorno = if ($env:RODAJE_ENTORNO) { $env:RODAJE_ENTORNO } else { Join-Path $raiz '.entorno' }
$ffmpeg = Join-Path $entorno 'ffmpeg\bin\ffmpeg.exe'
if (-not (Test-Path -LiteralPath $ffmpeg)) { throw "Falta $ffmpeg (corre build\preparar-entorno.ps1)." }
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('subtitular-iconos-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $lados = 16, 20, 24, 32, 40, 48, 64, 128, 256
    $pngs = foreach ($l in $lados) {
        $svg = Join-Path $marca $(if ($l -le 32) { 'subtitular-pequeno.svg' } else { 'subtitular.svg' })
        $png = Join-Path $tmp "$l.png"
        & $ffmpeg -hide_banner -v error -y -width $l -height $l -i $svg -frames:v 1 -pix_fmt rgba $png
        if ($LASTEXITCODE -ne 0) { throw "ffmpeg no pudo rasterizar $svg a $l px." }
        , [IO.File]::ReadAllBytes($png)
    }
    # Cabecera ICONDIR, una entrada por tamaño (ancho/alto 0 = 256) y los PNG seguidos.
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter($ms)
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$lados.Count)
    $offset = 6 + 16 * $lados.Count
    for ($i = 0; $i -lt $lados.Count; $i++) {
        $l = [byte]($lados[$i] % 256)
        $w.Write($l); $w.Write($l); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($p in $pngs) { $w.Write($p) }
    $w.Flush()
    [IO.File]::WriteAllBytes((Join-Path $marca 'subtitular.ico'), $ms.ToArray())
    Copy-Item -LiteralPath (Join-Path $tmp '256.png') -Destination (Join-Path $marca 'subtitular-256.png') -Force
    Write-Host '  subtitular.ico, subtitular-256.png'
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
