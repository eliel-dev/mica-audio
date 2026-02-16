# Glossario

- `PcmFrame`: frame de audio normalizado (mono float) com timestamp QPC.
- `SpectrumFrame`: frame de espectro com bandas de display, `bands64` e `level`.
- `BandsDisplay`: bandas usadas pela renderizacao principal.
- `Bands64`: bandas de output para HUB75/device stream.
- `QPC`: QueryPerformanceCounter (clock de alta resolucao no Windows).
- `Mode0`: layout de barras inspirado em audioMotion para distribuicao por largura.
- `ILedOutput`: contrato unico de saida para simulador/servidor.
- `StreamFrameV1`: payload binario enviado para firmware via servidor.
- `Tracked command`: comando com `commandId`, progresso e timeout.
- `OTA pull`: firmware baixa binario do servidor via HTTP.
- `HUB75 preview`: simulacao local de matriz 64x32 no app.
- `DOCS:`: marcador no codigo apontando para pagina da wiki.
