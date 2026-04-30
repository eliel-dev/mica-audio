# Handoff - Device.Client.Embedded Adapter

## Objetivo

Extrair a adaptacao embedded do device server para `Device.Client.Embedded`, mantendo o server embutido no WinUI e preservando protocolo, firmware, portas, MQTT, WS e auth.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: App/Output/Paineis continuam consumindo contratos de client; o runtime embedded fica em assembly proprio; `DeviceServerHost` permanece no composition root WinUI.

## Arquivos alterados

- `src/Device.Client.Embedded/Device.Client.Embedded.csproj`
- `src/Device.Client.Embedded/EmbeddedDeviceServerClient.cs`
- `src/Device.Client.Embedded/EmbeddedDeviceServerClientOptions.cs`
- `src/Device.Client.Embedded/EmbeddedDeviceServerSettings.cs`
- `src/Device.Client.Embedded/IEmbeddedDeviceServerClientRuntime.cs`
- `src/Device.Client.Embedded/IEmbeddedDeviceRegistryStore.cs`
- `src/Device.Client.Embedded/IEmbeddedDeviceServerSettingsProvider.cs`
- `src/Device.Client.Embedded/IEmbeddedDevicePublicHostResolver.cs`
- `src/Device.Client.Embedded/NetworkInterfaceEmbeddedDevicePublicHostResolver.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/Services/Devices/AppEmbeddedDeviceServerSettingsProvider.cs`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `tests/Output.Tests/EmbeddedDeviceServerClientTests.cs`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/app-winui.md`

## Decisoes tomadas

1. `Device.Client.Embedded` referencia `Device.Client.Abstractions`, `Device.Server.Abstractions` e `Device.Protocol`, mas nao referencia `App.WinUI`.
2. `EmbeddedDeviceServerClient` concentra start/stop/dispose, seed/save do registry, resolucao de host LAN, montagem de `ServerConfig` e forwarding para `IDeviceServerHost`.
3. `App.WinUI` reteve persistencia/settings locais por meio de `JsonDeviceRegistryStore` e `AppEmbeddedDeviceServerSettingsProvider`.
4. `IDeviceFrameTransport` continua resolvendo para `IDeviceServerHost`, preservando o hot path de frames sem client remoto.
5. `DeviceIntegrationService` e o antigo `IDeviceRegistryStore` app-local foram removidos porque a implementacao ativa agora vive no adapter embedded.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~EmbeddedDeviceServerClientTests|FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~Esp32S3LedOutputTests|FullyQualifiedName~LedOutputLifecycleTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests|FullyQualifiedName~DeviceOperationsCoordinatorDeviceLogsTests" -> PASS (30/30)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj --filter "FullyQualifiedName~Hub75PriorityArbitrationTests|FullyQualifiedName~WinUiBootstrapSmokeTests" -> PASS (14/14)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> PASS
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> PASS
dotnet build .\MicaAudio.sln -c Debug -> PASS (0 warnings, 0 errors)
git diff --check -> PASS
```

## Riscos e rollback

- Risco principal: divergencia futura entre settings/registry do WinUI e defaults do adapter embedded.
- Como reverter: restaurar `DeviceIntegrationService` como implementacao app-local de `IDeviceServerClient` e remover o registro de `EmbeddedDeviceServerClient` no composition root.

## Proximos passos

1. Criar um adapter remoto HTTP/WS apenas quando houver decisao explicita de server process separado.
2. Manter testes de arquitetura impedindo `Output` de voltar a depender de assemblies de server.
