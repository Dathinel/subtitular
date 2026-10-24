@echo off
rem Publica la ventana y la CLI con Native AOT en dist\Subtitular\ (App y Cli juntos). Uso: build\publicar.cmd
setlocal
set "RAIZ=%~dp0.."
call "%RAIZ%\build\dotnet.cmd" publish "%RAIZ%\src\Subtitular.App\Subtitular.App.csproj" -c Release -r win-x64 -o "%RAIZ%\dist\Subtitular" -warnaserror || exit /b 1
call "%RAIZ%\build\dotnet.cmd" publish "%RAIZ%\src\Subtitular.Cli\Subtitular.Cli.csproj" -c Release -r win-x64 -o "%RAIZ%\dist\Subtitular" -warnaserror || exit /b 1
exit /b 0
