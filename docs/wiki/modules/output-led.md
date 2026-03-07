# Modulo Output LED

## Fluxo de execucao

1. `LedPayloadFactory` cria o payload canonico do pipeline (`Bins128` ou `Frame128x64`)
2. `AudioPipelineOutputRouter` envia o payload para ESP32, simulador ou null output
3. `Esp32S3LedOutput` serializa `StreamFrameV2`
4. `LedFrameDeduplicator` codifica RGB565 e decide se o frame precisa ser reenviado
5. `SimulatorLedOutput` mantem snapshot local nativo `128x64`

## Atualizacao 2026-03 - visualizer HUB75 por frame

- O app pode suprimir o envio continuo de `Bins128` e usar `Frame128x64` como caminho autoritativo para renderers 2D mais artisticos.
- Esse caminho preserva o firmware atual: o ESP32 ja suporta `messageType = 2` e apenas desenha o frame recebido.
- `Bins128` continua disponivel como caminho de menor custo para renderers que preferem throughput.

## Atualizacao 2026-03 - Factory de payload e roteamento do pipeline

- O app deixou de montar `LedPayload` manualmente em varios pontos:
  - `LedPayloadFactory.CreateSpectrumPayload()` e o caminho canonico para `SpectrumFrame -> Bins128`;
  - `LedPayloadFactory.CreateFramePayload()` preserva o caminho `Frame128x64` para GIF e renderers 2D;
  - `LedPayloadFactory.ResampleSpectrumBins()` centraliza o remapeamento para `128` bins usado no pipeline e nos smoke tests.
- O runtime do app passou a separar composicao de payload de roteamento:
  - `AudioPipelineFrameProcessor` decide quando enviar `Bins128` vs `Frame128x64`;
  - `AudioPipelineOutputRouter` aplica brilho e decide se o simulador recebe o frame;
  - `Esp32S3LedOutput` continua sem mudar o wire e recebe um `LedPayload` ja normalizado.

## Atualizacao 2026-03 - Deduplicacao RGB565 extraida

- O caminho ESP32 passou a usar `LedFrameDeduplicator` como helper puro para:
  - codificar `RgbaColor -> RGB565`;
  - detectar frame repetido com o mesmo brilho;
  - forcar reenvio quando o brilho muda mesmo com pixels iguais.
- `Esp32S3LedOutput` ficou reduzido a adaptador entre `LedPayload` e `IDeviceServerHost.BroadcastFrame`.
- O contrato wire continua inalterado:
  - `StreamFrameV2`
  - `messageType = bins128`
  - `messageType = frame128x64Rgb565`

## Referencias de codigo

- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [LedPayloadFactory](../../../src/MicaAudio.Core/Led/LedPayloadFactory.cs#L1)
- [AudioPipelineFrameProcessor](../../../src/App.WinUI/Services/AudioPipelineFrameProcessor.cs#L1)
- [AudioPipelineOutputRouter](../../../src/App.WinUI/Services/AudioPipelineOutputRouter.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [LedFrameDeduplicator](../../../src/Output/Led/LedFrameDeduplicator.cs#L1)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L1)
- [StreamFrameV2](../reference/ws-protocol-v2.md)
- [StreamFrameV1 legado](../reference/ws-protocol-v1.md)
