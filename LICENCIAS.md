# Licencias

Qué lleva Subtitular y con qué licencia. Ningún binario ni modelo de terceros va en este repositorio: los modelos se descargan con huella fijada.

## Código

| Pieza | Licencia | Cómo entra |
|---|---|---|
| Código propio (`src/`, `build/`) | GPL-3.0-only | Este repositorio |
| Rodaje (`externo/rodaje`, submódulo fijado a un commit) | GPL-3.0-only con los términos adicionales de la sección 7 de su `LICENSE` | Se compilan sus proyectos; el código no se copia |
| SDK de .NET 10 y runtime de Native AOT | MIT | `.entorno\`, con huella SHA-512 |
| FFmpeg, compilación LGPL de Rodaje | LGPL v3 | `.entorno\ffmpeg`, con huella SHA-256; se distribuye como DLL y ejecutables sin renombrar |
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
