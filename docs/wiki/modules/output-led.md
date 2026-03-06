# Modulo Output LED

## Fluxo de execucao

1. receber `LedPayload.Frame128x64` para frame completo nativo
2. ou receber `LedPayload.Bins128` para barras
3. `Esp32S3LedOutput` serializa `StreamFrameV2`
4. `SimulatorLedOutput` mantem snapshot local nativo `128x64`

## Atualizacao 2026-03 - visualizer HUB75 por frame

- O app pode suprimir o envio continuo de `Bins128` e usar `Frame128x64` como caminho autoritativo para renderers 2D mais artisticos.
- Esse caminho preserva o firmware atual: o ESP32 ja suporta `messageType = 2` e apenas desenha o frame recebido.
- `Bins128` continua disponivel como caminho de menor custo para renderers que preferem throughput.

## Referencias de codigo

- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L1)
- [StreamFrameV2](../reference/ws-protocol-v2.md)
- [StreamFrameV1 legado](../reference/ws-protocol-v1.md)
