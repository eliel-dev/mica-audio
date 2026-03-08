# Referencia - WS Protocol v1 (legado)

`StreamFrameV1` permanece apenas como contrato historico para devices antigos 64x32.

## Tipos de mensagem

1. tipo `1`: `bins64`
2. tipo `2`: `frame 64x32 RGB565`

## Estrutura StreamFrameV1

O contrato legado de barras usa `bins64` e brilho em payload fixo.

## Estrutura StreamFrameV1 RGB565

O contrato legado de frame usa `64x32` RGB565.

Fluxo ativo do produto:
1. usar `StreamFrameV2`
2. usar painel `hub75_p2_5_128x64_smd2121_scan32`
3. usar frame RGB565 nativo `128x64`

Referencias:
- [WS protocol v2](ws-protocol-v2.md)
- [StreamFrameV1](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L1)
