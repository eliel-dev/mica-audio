# Handoff de mudanca estrutural

## Objetivo

Migrar o control plane de devices para MQTT, preservando o WebSocket binario atual exclusivamente para stream visual.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - host embute broker MQTT e autentica por `deviceId/token`;
  - comandos tracked passam por MQTT;
  - `status` e `presence` MQTT governam online/offline;
  - firmware preserva `WStype_BIN` para frames;
  - docs e testes refletem o novo contrato.

## Arquivos alterados

- `src/Device.Server/Device.Server.csproj`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs`
- `src/Device.Server/Hosting/DeviceMqttTopics.cs`
- `src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs`
- `src/Device.Server/Hosting/DeviceSession.cs`
- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/Device.Protocol/Models/PairDeviceResponse.cs`
- `src/Device.Protocol/Models/ServerInfoResponse.cs`
- `src/Device.Protocol/Models/DevicePresenceMessage.cs`
- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `tests/Output.Tests/DeviceSessionTests.cs`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/DeviceServerTestHarness.cs`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. O broker MQTT foi embutido no `DeviceServerHost` para manter rollout simples, reutilizar `deviceId/token` e evitar dependencia operacional externa.
2. O WS binario foi mantido intacto no hot path visual; apenas o control plane foi movido para MQTT para nao introduzir risco no streaming.
3. O host continua aceitando WS-texto e `/api/v1/device/command-ack` como rollback passivo, mas o firmware oficial novo deixou de usa-los.
4. O snapshot `Online` passou a significar disponibilidade do control plane MQTT, com grace curto para reconexao e sem depender do socket de stream.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> sucesso
dotnet build src/Device.Server/Device.Server.csproj -c Debug -nologo -> sucesso
dotnet build tests/Output.Tests/Output.Tests.csproj -c Debug -nologo -> sucesso
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug -nologo -> sucesso
dotnet build MicaAudio.sln -c Debug -nologo -> sucesso
platformio run -e esp32s3_devkitc1_dma_exp -> sucesso
```

## Riscos e rollback

- Risco principal: firmware legado que so sobe WS continua transmitindo frames, mas deixa de aparecer como online para comandos ate reflash.
- Como reverter:
  - rollback de firmware para a imagem anterior que usava WS-texto;
  - rollback do host removendo `DeviceServerHost.Mqtt.cs` do caminho de execucao e restaurando envio de comando por WS;
  - manter `/api/v1/device/command-ack` e WS-texto passivos ajuda a reduzir janela de retorno.

## Proximos passos

1. Fazer smoke manual em hardware para `test_led`, `set_brightness` e `activate_app` via MQTT.
2. Decidir se o cliente MQTT do firmware precisa ser elevado para QoS 1 end-to-end em publish, caso a telemetria de campo mostre perda relevante.
3. Planejar remocao do rollback passivo WS-texto quando a base em campo estiver regravada.
