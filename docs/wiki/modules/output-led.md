# Modulo Output LED

## Fluxo de execucao

1. `LedPayloadFactory` cria o payload canonico do pipeline (`Bins128` ou `Frame128x64`)
2. `AudioPipelineOutputRouter` envia o payload para ESP32, simulador ou null output
3. `Esp32S3LedOutput` serializa `StreamFrameV2`
4. `LedFrameDeduplicator` codifica RGB565 e decide se o frame precisa ser reenviado
5. `SimulatorLedOutput` mantem snapshot local nativo `128x64`

## Atualizacao 2026-03 - shipping mode HUB75 em Bins128

- O caminho shipping/default do desktop voltou a priorizar `Bins128`.
- `AudioPipelineFrameProcessor` continua tratando `Frame128x64` como infraestrutura preservada, mas no runtime normal do app o fluxo ativo e:
  - `PcmFrame -> SpectrumFrame -> LedPayloadFactory.CreateSpectrumPayload() -> Type 1`.
- `Frame128x64` continua existindo para cenarios explicitamente forcados e para a infraestrutura local do preview/simulador, sem sair do contrato wire atual.
- A consequencia intencional e que o preview WinUI pode divergir do HUB75 fisico em renderers artisticos, em troca de throughput maior no device.

## Atualizacao 2026-03 - identidade visual `Bins128` por `flags`

- O payload canonico do pipeline ganhou `BinsFlags` explicito em `LedPayload`.
- `LedPayloadFactory` agora preenche esse byte no caminho `Bins128`, em vez de deixar `Esp32S3LedOutput` recalcular estilo no limite do wire.
- `Esp32S3LedOutput` apenas repassa o valor resolvido para `StreamFrameV2.CreateBins128(...)`.
- O contrato operacional fica:
  - host resolve `presetId + rendererId -> styleId + paletteFamilyId`;
  - `flags = 0` continua reservado ao visual legado do firmware;
  - `Frame128x64` nao muda.

## Atualizacao 2026-03 - preview HUB75 local fiel ao `Bins128` do device

- O preview HUB75 local do `Visualizador` continua vindo de `SimulatorLedOutput`, sem trocar a fonte do painel inferior da tela.
- O simulador deixou de desenhar `Bins128` com um unico visual espelhado legado.
- O novo `Bins128PreviewRenderer` replica localmente as familias fisicas do firmware:
  - `wave-mirror`
  - `mirror-lines`
  - `mirror-blocks`
  - `classic-bars`
  - `flow-line`
  - `history-scan`
  - `radial-orbit`
  - `atmosphere`
  - `launchpad-grid`
- O contrato do preview local passa a ser o proprio payload ja emitido para o HUB75:
  - `bins[128]`
  - `level`
  - `brightness`
  - `BinsFlags`
- `Frame128x64` continua igual, e `flags = 0` preserva o fallback legado para emissores antigos.

## Atualizacao 2026-03 - Factory de payload e roteamento do pipeline

- O app deixou de montar `LedPayload` manualmente em varios pontos:
  - `LedPayloadFactory.CreateSpectrumPayload()` e o caminho canonico para `SpectrumFrame -> Bins128`;
  - `LedPayloadFactory.CreateFramePayload()` preserva o caminho `Frame128x64` para GIF e renderers 2D;
  - o remapeamento para `128` bins fica encapsulado no proprio `LedPayloadFactory`, sem virar superficie publica do pipeline.
- O runtime do app passou a separar composicao de payload de roteamento:
  - `AudioPipelineFrameProcessor` decide quando enviar `Bins128` vs `Frame128x64`;
  - `AudioPipelineOutputRouter` aplica brilho e decide separadamente se o simulador e o device HUB75 recebem o frame;
  - `Esp32S3LedOutput` continua sem mudar o wire e recebe um `LedPayload` ja normalizado.

## Atualizacao 2026-03 - Toggle HUB75 como gate do device output

- O envio para `Esp32S3LedOutput` deixou de acontecer por default ao entrar no `Visualizador`.
- `AudioPipelineOutputRouter` agora trata preview local e output remoto como estados independentes:
  - `enableSimulator` controla apenas o preview local do HUB75;
  - `enableHub75DeviceOutput` controla apenas o stream para o ESP32.
- Com isso:
  - o toggle `Modo HUB75` passou a governar exclusivamente o device output;
  - `forceSimulator` continua existindo apenas para cenarios locais de preview;
  - quando ambos os outputs estao desligados, o payload cai no `NullLedOutput`.
- No shipping mode atual:
  - `Audio` usa `Bins128` por default no device;
  - `GIF` do visualizador principal continua local/simulador;
  - `Paineis` preservam um path dedicado `Frame128x64` com envio direcionado por `deviceId`.

## Atualizacao 2026-03 - Deduplicacao RGB565 extraida

- O caminho ESP32 passou a usar `LedFrameDeduplicator` como helper puro para:
  - codificar `RgbaColor -> RGB565`;
  - detectar frame repetido com o mesmo brilho;
  - forcar reenvio quando o brilho muda mesmo com pixels iguais.
- `Esp32S3LedOutput` ficou reduzido a adaptador entre `LedPayload` e `IDeviceFrameTransport`.
- O contrato wire continua inalterado:
  - `StreamFrameV2`
  - `messageType = bins128`
  - `messageType = frame128x64Rgb565`

## Atualizacao 2026-04 - Transporte De Frames Desacoplado

- `Esp32S3LedOutput` depende de `Device.Client.IDeviceFrameTransport`, nao do host completo do server.
- `Output` referencia `Device.Client.Abstractions` e nao referencia `Device.Server` nem `Device.Server.Abstractions`.
- A implementacao embedded do transporte fica fora de `Output`, em `Device.Client.Embedded` + composition root WinUI.
- O output continua emitindo os mesmos payloads `StreamFrameV2`; a mudanca e apenas de fronteira interna para preparar um futuro client remoto.

## Referencias de codigo

- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [LedPayloadFactory](../../../src/MicaAudio.Core/Led/LedPayloadFactory.cs#L1)
- [AudioPipelineFrameProcessor](../../../src/App.WinUI/Services/AudioPipelineFrameProcessor.cs#L1)
- [AudioPipelineOutputRouter](../../../src/App.WinUI/Services/AudioPipelineOutputRouter.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [IDeviceFrameTransport](../../../src/Device.Client.Abstractions/IDeviceFrameTransport.cs#L1)
- [LedFrameDeduplicator](../../../src/Output/Led/LedFrameDeduplicator.cs#L1)
- [Bins128PreviewRenderer](../../../src/Output/Led/Bins128PreviewRenderer.cs#L1)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L1)
- [StreamFrameV2](../reference/ws-protocol-v2.md)
