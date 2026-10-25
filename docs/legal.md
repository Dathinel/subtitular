# Fondo legal de Subtitular

No es asesoría legal: resume las licencias y lo que hay que comprobar antes de entregar un ejecutable a terceros. Las licencias de cada pieza están en [LICENCIAS.md](../LICENCIAS.md).

## Licencia

GPL-3.0-only, con el texto oficial completo en [LICENSE](../LICENSE). La razón: Subtitular compila código de Rodaje (submódulo `externo/rodaje`), que es GPLv3, y trabaja con sus módulos de IA, también GPLv3. Rodaje y este programa tienen un solo titular (Dathinel), así que la elección de licencia es libre; GPLv3 es la coherente con el resto.

**Términos adicionales de Rodaje.** El `LICENSE` de Rodaje añade, por la sección 7, un aviso de atribución en la interfaz («Basado en el editor de video de código abierto de Dathinel / Rodaje», con enlace). Ese término acompaña al código de Rodaje que se compila aquí. Recomendación provisional: mostrar ese crédito en «Acerca de», que no cuesta nada y deja cerrada la duda. El titular puede decidir eximirlo.

**Marca.** El nombre «Rodaje», su logotipo y su icono no se licencian con el código (`TRADEMARK.md` de Rodaje). Subtitular usa nombre e icono propios.

## Lista de comprobación antes de publicar

- [x] `LICENSE` con la GPLv3 completa y la cabecera de copyright y URL (`Copyright (C) 2026 Dathinel`, `https://github.com/Dathinel/subtitular`).
- [x] Crédito de Rodaje: en la ventana («Acerca de» en la cabecera, 24-oct: texto literal como `HyperlinkButton` al repositorio, más versión y GPLv3 de Subtitular), `LICENCIAS.md`, `README.md` y notas de distribución («Basado en el editor de video de código abierto de Dathinel / Rodaje», https://github.com/Dathinel/rodaje) en cumplimiento de la Sección 7.
- [x] Nombre e icono propios: denominación propia «Subtitular», sin recursos protegidos por la marca de Rodaje.
- [x] `LICENCIAS.md` al día: dependencias, módulos de IA (`modulo-subtitulos`, `modulo-traducir`, `modulo-voz`) y modelos con versión, licencia (GPLv3, MIT, Apache-2.0) y huella.
- [x] Modelos: Whisper (MIT), Qwen3-4B-Instruct-2507 (Apache-2.0), Kokoro-82M (Apache-2.0); licencias que admiten redistribución y uso comercial, fijados por commit y verificados con SHA-256.
- [x] FFmpeg: solo la compilación LGPL de Rodaje (sin `--enable-gpl` ni `--enable-nonfree`), ejecutables sin renombrar y enlace a fuentes.
- [x] Sin código GPL ajeno copiado: solo referencias de proyecto al submódulo y consumo de módulos por protocolo v1.
- [x] Sin datos personales ni secretos en el historial (compatible con reglas de Rodaje).
- [x] La release se reconstruye desde su etiqueta con `build\preparar-entorno.ps1` y publica `SHA256SUMS`.
