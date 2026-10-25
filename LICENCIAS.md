# Licencias

Qué lleva Subtitular y con qué licencia. Ningún binario ni modelo de terceros va en este repositorio: los modelos se descargan con huella fijada.

## Código

| Pieza | Licencia | Cómo entra |
|---|---|---|
| Código propio (`src/`, `build/`) | GPL-3.0-only | Este repositorio |
| Rodaje (`externo/rodaje`, submódulo fijado a un commit) | GPL-3.0-only con los términos adicionales de la sección 7 de su `LICENSE` | Se compilan sus proyectos; el código no se copia |
| SDK de .NET 10 y runtime de Native AOT | MIT | `.entorno\`, con huella SHA-512 |
| FFmpeg, compilación LGPL de Rodaje | LGPL v3 | `.entorno\ffmpeg`, con huella SHA-256; se distribuye como DLL y ejecutables sin renombrar |
| Windows App SDK (`Microsoft.WindowsAppSDK.WinUI` 2.3.9, `.Foundation` 2.3.12, `.InteractiveExperiences` 2.1.9) | Términos de licencia de software de Microsoft, no MIT (detalle, cláusulas y consulta pendiente al abogado en el `LICENCIAS.md` de Rodaje) | Publicación autocontenida: sus DLL, `.pri`, `.winmd` y carpetas de idioma van en el zip junto a `Subtitular.exe` (45 MB sin comprimir, la mayor parte de los 26 MB del zip); conservan los términos de Microsoft |
| Microsoft.Web.WebView2 (dependencia de WinUI) | Licencia propia de Microsoft de tipo BSD | `Microsoft.Web.WebView2.Core.dll` y `WebView2Loader.dll` en el zip; el control no se usa |
| Proyección de C# del Windows SDK y CsWinRT | Términos del Windows SDK; CsWinRT MIT | Compiladas dentro de los dos exe por Native AOT |
| Inno Setup 7.1.0 (portable en `.entorno`, bajado con huella fijada) | Licencia propia de Inno Setup: uso y redistribución permitidos | Arma `Subtitular-<v>-instalador.exe`, que lleva dentro su programa de instalación |
| Whisper.net y whisper.cpp | MIT | Dentro de modulo-subtitulos |
| LLamaSharp y llama.cpp | MIT | Dentro de modulo-traducir |
| ONNX Runtime | MIT | Dentro de modulo-voz |

## Módulos y modelos (se descargan aparte)

| Módulo | Modelo | Licencia del modelo |
|---|---|---|
| modulo-subtitulos (GPLv3) | Whisper large-v3-turbo q5_0, small q5_1, base q5_1 | MIT |
| modulo-traducir (GPLv3) | Qwen3-4B-Instruct-2507 Q4_K_M | Apache-2.0 |
| modulo-voz (GPLv3) | Kokoro-82M y sus voces | Apache-2.0 |

El detalle (URL fijada a un commit, tamaño y SHA-256) está en el `paquete.json` y el `LICENCIAS.md` de cada módulo, en `externo/rodaje/modules/`.

## Crédito

Subtitular está basado en el editor de video de código abierto de Dathinel / Rodaje (https://github.com/Dathinel/rodaje), como pide la sección 7 de su licencia. El nombre, el logotipo y el icono de Rodaje no se usan.
