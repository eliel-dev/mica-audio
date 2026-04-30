# Handoff - transporte WebP batch para `Paineis`

## Objetivo

Adicionar um caminho `monitor-first` para `Paineis` baseado em lotes `WebP` animados de `1 s / 30 frames`, com compositor autoritativo no host e playback efemero no ESP32-S3.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - `Paineis` gera batches `WebP` lossless em memoria quando o device anuncia suporte.
  - `Device.Server` expoe download HTTP autenticado para os batches e envia `queue_panels_batch`.
  - O firmware baixa, valida e toca o batch em task dedicada sem quebrar `Bins128` nem o fallback `Frame128x64`.

## Arquivos alterados

- `src/App.WinUI/Services/Panels/PanelsAnimatedWebpEncoder.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/Device.Protocol/Models/DeviceCommandType.cs`
- `src/Device.Protocol/Models/PanelsBatchCommandPayload.cs`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceSession.cs`
- `src/Device.Server/Hosting/DeviceRecordMutations.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsText.cs`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/lib/libwebp/library.json`
- `firmware/esp32s3-devkitc1/lib/libwebp/src/webp/config.h`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/reference/code-index.md`
- `tests/Integration.Smoke/PanelsFrameComposerTests.cs`
- `tests/Output.Tests/DeviceTelemetryMessageTests.cs`
- `tests/Output.Tests/DeviceServerHostPanelsBatchTests.cs`

## Decisoes tomadas

1. O compositor de widgets continua 100% no host. O firmware permanece cego para layout/janela e recebe apenas batches full-canvas `128x64`.
2. O transporte novo usa `queue_panels_batch` + HTTP pull autenticado, sem reintroduzir arquitetura de apps e sem inventar novo wire binario no WebSocket.
3. O batch v1 e `WebP` animado lossless, janela fixa de `1000 ms` e `30` frames, com politica `play-once queue`.
4. O cache de batches fica apenas em memoria:
   - host: `ativo + proximo` por `deviceId + panelsSessionId`;
   - firmware: `ativo + proximo` em RAM/PSRAM, sem `FFat`.
5. O fallback para o pipeline existente permanece automatico:
   - host usa batches apenas se `animatedWebpBatchSupported == true`;
   - qualquer falha de fila/download devolve `Paineis` ao `Frame128x64`.
6. Para reduzir risco e reaproveitar base testada, a decodificacao no ESP32-S3 foi implementada sobre `libwebp` vendorizada com API oficial `WebPAnimDecoder`, em vez de protocolo proprietario de delta.

## Validacoes executadas

```text
dotnet build src/Device.Server/Device.Server.csproj -c Debug -m:1 -> sucesso
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -m:1 -> sucesso
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --no-build --filter "FullyQualifiedName~PanelsFrameComposerTests" -> sucesso
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~DeviceServerHostPanelsBatchTests" -> sucesso
dotnet build MicaAudio.sln -c Debug -> sucesso
pio run -e esp32s3_devkitc1_dma_exp -> sucesso
```

## Riscos e rollback

- Risco principal:
  - batches `WebP` sao mais leves e estaveis que stream continuo, mas ainda introduzem latencia de lote (`1 s`) e dependencia de prefetch correto para evitar underrun.
- Como reverter:
  - remover o uso de `animatedWebpBatchSupported` no `PanelsPlaybackService`;
  - desabilitar o comando `queue_panels_batch`;
  - manter apenas o caminho existente `Frame128x64` no host e no firmware.

## Proximos passos

1. Validar em hardware real o pacing do batch, especialmente cenarios `GIF + relogio` com atualizacao por segundo.
2. Medir underrun/latencia em LAN real e decidir se o v2 precisa de janela maior, `loop-until-invalidated` ou compressao/qualidade diferente.
3. Se o modelo se provar estavel, adicionar testes end-to-end mais completos para `queue_panels_batch` via MQTT + download HTTP autenticado.
