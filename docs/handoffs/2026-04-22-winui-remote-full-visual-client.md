# WinUI Remote Full Visual Client

## Objetivo

Criar o primeiro modo remoto real do WinUI contra `MicaAudio.Server` standalone/Docker/Render, mantendo `Embedded` como default seguro e sem alterar firmware, endpoints de device existentes, MQTT topics atuais ou o fluxo embedded.

## Escopo classificado

- Tipo: estrutural + protocolo app/server
- Criterio de aceite: WinUI pode alternar para `Remote`, usar token admin, listar/remover devices, gerar pair code, enviar comandos tracked, registrar batches WebP e enviar frames HUB75 via server standalone.

## Arquivos alterados

- `src/Device.Client.Abstractions/IDeviceServerClient.cs`
- `src/Device.Client.Abstractions/IDeviceServerClientRuntime.cs`
- `src/Device.Client.Remote/*`
- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/Device.Protocol/Models/Admin*.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Admin.cs`
- `src/Device.Server/Hosting/AdminEventConnection.cs`
- `src/Device.Server/Hosting/DeviceServerHost*.cs`
- `src/MicaAudio.Server/MicaAudioServerOptions.cs`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Services/Devices/RemoteDeviceServerSecretStore.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `src/MicaAudio.Core/Presets/AppSettings.cs`
- `src/MicaAudio.Core/Presets/DeviceServerMode.cs`
- `render.yaml`
- `MicaAudio.sln`

## Decisoes tomadas

1. `Embedded` permanece default; `Remote` e opt-in por `AppSettings.DeviceServerMode`.
2. O admin token fica fora de `settings.json`, protegido por DPAPI em `remote-server-secrets.json`.
3. `IDeviceServerClientRuntime` virou contrato comum de lifecycle; o remote usa `RemoteDeviceServerRuntime` para iniciar events WS e frames WS juntos.
4. A Admin API e habilitada apenas com `ServerConfig.AdminToken`; token vazio retorna `admin_api_disabled`.
5. `RemoteDeviceFrameTransport` preserva `IDeviceFrameTransport` sincronico, mas enfileira frames em `Channel` bounded com `DropOldest` e envia em background.
6. `WS /ws/v1/admin/frames` usa envelope binario simples (`mode`, `deviceIdLength`, `deviceId`, payload) e chama `BroadcastFrame`/`SendFrame` no server.

## Validacoes executadas

```text
dotnet test tests\Output.Tests\Output.Tests.csproj --no-restore --filter "DeviceServerHostAdminApiTests|RemoteDeviceServerClientTests" -> aprovado (8 testes)
dotnet test tests\Output.Tests\Output.Tests.csproj --no-restore --filter "DeviceOperationsCoordinatorBrightnessTests|DeviceOperationsCoordinatorDeviceLogsTests|Hub75VisualizerSessionServiceTests|RemoteDeviceServerClientTests|DeviceServerHostAdminApiTests" -> aprovado (21 testes)
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj --no-restore --filter "WinUiBootstrapSmokeTests" -> aprovado (10 testes)
dotnet test tests\Output.Tests\Output.Tests.csproj --no-restore --filter "ServerAbstractionBoundaryTests|MicaAudioServerStandaloneTests|DeviceServerHostAdminApiTests|RemoteDeviceServerClientTests|DeviceOperationsCoordinatorBrightnessTests|DeviceOperationsCoordinatorDeviceLogsTests|Hub75VisualizerSessionServiceTests" -> aprovado (39 testes)
dotnet test tests\Output.Tests\Output.Tests.csproj --no-restore --filter "DeviceServerHostAdminApiTests|RemoteDeviceServerClientTests|ServerAbstractionBoundaryTests|Esp32S3LedOutputTests|LedOutputLifecycleTests|DeviceIntegrationServiceLegacyWsSettingTests|DeviceOperationsCoordinatorBrightnessTests|DeviceOperationsCoordinatorDeviceLogsTests|DeviceServerHostPanelsBatchTests|Hub75VisualizerSessionServiceTests" -> aprovado (41 testes)
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj --no-restore --filter "Hub75PriorityArbitrationTests|WinUiBootstrapSmokeTests" -> aprovado (15 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> falhou antes de atualizar lock files, detectando corretamente a nova referencia Device.Client.Remote
dotnet restore .\MicaAudio.sln --force-evaluate -> aprovado, lock files atualizados
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> aprovado
dotnet build MicaAudio.sln -c Debug --no-restore -> aprovado (0 warnings, 0 errors)
dotnet build .\MicaAudio.sln -c Debug -> aprovado (0 warnings, 0 errors)
docker build -f src\MicaAudio.Server\Dockerfile -t mica-audio-server:remote-dev . -> aprovado
docker run -e PORT=8080 -e MICA_SERVER__ADMINTOKEN=dev-token -e MICA_SERVER__RESTRICTTOPRIVATENETWORKS=false -p 5372:8080 mica-audio-server:remote-dev -> /api/v1/health ok, /api/v1/server/info ok, /api/v1/admin/pairing-codes ok
```

## Riscos e rollback

- Risco principal: usar Remote contra Render como operacao firmware completa; nesta entrega Render ainda e smoke publico de Admin API/WSS e o firmware atual permanece local/MQTT.
- Como reverter: manter `DeviceServerMode=Embedded` em `settings.json`; remover `Device.Client.Remote`, Admin API e referencias associadas se necessario. O fluxo embedded continua isolado.

## Proximos passos

1. Rodar smoke Docker local com `MICA_SERVER__ADMINTOKEN=dev-token`.
2. Validar WinUI remoto manualmente contra `http://127.0.0.1:5272`.
3. Projetar WSS/device cloud v2 para reduzir dependencia de MQTT publico em Render.
