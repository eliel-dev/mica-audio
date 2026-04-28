# Handoff - Direct LAN Visual + Stable Device Identity

## Objetivo

Corrigir o fluxo de desenvolvimento para que o mesmo ESP32-S3 nao vire outro device apos reflash, mesmo com NVS apagada, e fazer o visualizador remoto voltar a renderizar no HUB75 sem depender do Docker repassar frames visuais.

## Escopo classificado

- `firmware_protocolo`
- `estrutural`

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
- `src/Device.Protocol/Models/MicaDiscoveryRequestV1.cs`
- `src/Device.Protocol/Models/AdminVisualEndpointsResponse.cs`
- `src/Device.Protocol/Models/DeviceVisualEndpointInfo.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceSessionState.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Admin.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Client.Remote/RemoteDeviceFrameTransport.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Services/Devices/RemoteDeviceServerConnectionTester.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `tests/Output.Tests/DeviceServerHostTrustedLanRegistrationTests.cs`
- `tests/Output.Tests/DeviceServerHostAdminApiTests.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/RemoteDeviceServerClientTests.cs`
- `tests/Output.Tests/RemoteDeviceServerConnectionTesterTests.cs`
- `tests/Output.Tests/DeviceServerTestHarness.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/ws-protocol-v2.md`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `DeviceMac` e a identidade primaria de re-registro LAN. Reflash com NVS preservada continua usando `deviceId/token`; reflash limpo usa discovery por MAC e recebe o mesmo registro/token.
- `/api/v1/pair` permanece legado, mas grava/reusa `DeviceMac` para nao duplicar devices durante desenvolvimento.
- `LanIpAddress` foi separado de `LastKnownIp`; o primeiro vem do firmware (`deviceIp` no discovery ou `ipAddress` na telemetria) e o segundo continua sendo o IP observado da conexao, que pode ser Docker/bridge.
- Registros antigos offline sem MAC nao sao mesclados por IP automaticamente, porque IP local pode mudar ou representar NAT/bridge.
- `GET /api/v1/admin/visual-endpoints` expoe devices online, UDP-capable e com LAN IP valido para o WinUI remoto.
- `RemoteDeviceFrameTransport` envia `Bins128` direto para `LanIpAddress:visualUdpPort` usando `VisualUdpFrameV1` e HMAC com o token do device; payloads grandes e endpoints ausentes continuam no fallback WS admin.
- Docker local permanece com UDP visual server->ESP desligado por default; o caminho oficial remoto passa fora do container.
- Firmware continua no modelo ESP-IDF v5.5.4/Arduino atual e apenas acrescenta campos JSON cooperativos, sem trocar a stack de rede.

## Validacoes executadas

- `git diff --check` - passou; avisos apenas de normalizacao LF/CRLF em arquivos ja tocados.
- `dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --filter "TrustedLanRegistration|PairingLegacy_ShouldReuseExistingDeviceByMac|AdminVisualEndpoints|MqttStatusPublish_ShouldBackfillDeviceMacAndPreserveLanIpFromTelemetry|RemoteDeviceFrameTransport_ShouldSendBins128DirectlyToVisualUdpEndpoint|RemoteDeviceFrameTransport_ShouldFallbackToAdminWebSocketForFrame128x64|RemoteDeviceServerConnectionTester"` - passou, 10 testes.
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` - passou.
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` - passou.
- `dotnet build MicaAudio.sln -c Debug` - passou com avisos NU1902 ja existentes em OpenTelemetry.
- `python -m platformio run -d firmware\esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp` - passou; RAM 39.2%, Flash 49.7%.

## Riscos e rollback

- O caminho UDP direto exige que o PC com WinUI e o ESP estejam na mesma LAN e que firewall local permita envio UDP para o ESP.
- Se o endpoint visual estiver ausente, invalido ou bloqueado, o client remoto cai para `/ws/v1/admin/frames`; para diagnostico, testar primeiro o botao `Testar servidor remoto`.
- Devices antigos offline sem MAC permanecem como orfaos tecnicos e devem ser removidos por acao admin quando confirmado.
- Rollback de baixo risco: voltar o WinUI remoto para fallback WS removendo a preferencia UDP direta no `RemoteDeviceFrameTransport`; o protocolo de device continua compativel.

## Proximos passos

- Testar fisicamente com Docker default, WinUI em Remote e HUB75 ativo, conferindo aumento de `streamFramesReceived/Applied` no ESP.
- Se o firewall do Windows bloquear UDP LAN, criar uma regra documentada especifica para o app/porta visual.
- Expor os counters de diagnostico remoto em UI se o teste fisico mostrar que isso ajuda no suporte diario.

## Referencias

- ESP-IDF v5.5.4 ESP32-S3: https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/index.html
- ESP-IDF v5.5.4 index source: https://github.com/espressif/esp-idf/blob/v5.5.4/docs/en/index.rst
- ESP-IDF v5.5.4 lwIP/BSD sockets: https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/api-guides/lwip.html
- Docker port publishing: https://docs.docker.com/engine/network/port-publishing/
