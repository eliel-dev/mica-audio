# Network Poll Budget no loop do ESP32-S3

## Objetivo

Reduzir spikes de latencia no `loop()` do firmware `visualizer-hub75` introduzindo budget cooperativo para o trabalho de rede executado por iteracao, sem alterar o pipeline oficial de render do HUB75.

## Escopo classificado

- Classificacao: `firmware/protocolo`
- Escopo efetivo:
  - `firmware/esp32s3-devkitc1/src/main.cpp`
  - `docs/wiki/reference/device-telemetry-v2-fields.md`
  - `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

- O `loop()` continua em modo cooperativo unico, sem `xTaskCreate`.
- O budget por iteracao foi fixado em `kNetworkPollBudgetUs = 8000`.
- O budget cobre a secao de rede do `loop()`:
  - `gMqtt.loop()`
  - `gWs.loop()`
  - diagnostico/reconnect/telemetria executados no `loop()`
- O render continua fora do budget e permanece na cauda da iteracao:
  - `shouldPresentMatrixFrame(nowUs)`
  - `drawBars()` ou `drawFrame128x64()`
  - `commitMatrixFrame()`
- O contador `networkPollDeferCount` fica restrito ao JSON bruto de telemetria do firmware nesta entrega.
- O caminho principal do `loop()` nao usa mais `delay()` no ramo de Wi-Fi desconectado.

## Validacoes executadas

- Consulta de referencia oficial Espressif:
  - `https://docs.espressif.com/projects/esp-idf/en/v5.5.3/esp32s3/index.html`
  - `https://github.com/espressif/esp-idf/blob/v5.5.3/docs/en/index.rst`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `dotnet build MicaAudio.sln -c Debug -m:1`
- `pio run -e esp32s3_devkitc1_dma_exp`

## Riscos e rollback

- Se o budget de `8000 us` ficar agressivo demais, a conectividade pode perder cadencia e aumentar `networkPollDeferCount`.
- O fallback de provisioning continua potencialmente bloqueante quando acionado, pois o fluxo funcional do portal nao foi redesenhado nesta entrega.
- Rollback: reverter a reestruturacao do `loop()` e remover `networkPollDeferCount` do payload MQTT.

## Proximos passos

- Medir em hardware real a variacao de `loopHealthyPercent` antes/depois com stream ativo.
- Observar `networkPollDeferCount` para calibrar se `8000 us` e suficiente ou precisa ajuste.
- Se o contador se mostrar util, propagar o campo para `Device.Protocol` e dashboard em entrega posterior.
