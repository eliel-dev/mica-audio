# Handoff - Device.Client.Abstractions

## Objetivo

Extrair os contratos consumidos por clients para `Device.Client.Abstractions`, mantendo `Device.Server` como implementacao embedded e sem criar processo remoto.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `App.WinUI`, `Output` e futuros clients podem depender de `Device.Client.Abstractions` para `IDeviceServerClient`, `IDeviceFrameTransport` e `PanelsBatchRegistration`; `Device.Server.Abstractions` permanece como contrato do host embedded/lifecycle.

## Arquivos alterados

- `src/Device.Client.Abstractions/Device.Client.Abstractions.csproj`
- `src/Device.Client.Abstractions/IDeviceServerClient.cs`
- `src/Device.Client.Abstractions/IDeviceFrameTransport.cs`
- `src/Device.Client.Abstractions/PanelsBatchRegistration.cs`
- `src/Device.Server.Abstractions/Device.Server.Abstractions.csproj`
- `src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs`
- `src/Device.Server/Device.Server.csproj`
- `src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs`
- `src/Output/Output.csproj`
- `src/Output/Led/Esp32S3LedOutput.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Output.Tests/Esp32S3LedOutputTests.cs`
- `tests/Output.Tests/LedOutputLifecycleTests.cs`
- `tests/Output.Tests/DeviceIntegrationServiceLegacyWsSettingTests.cs`
- `tests/Integration.Smoke/Integration.Smoke.csproj`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Integration.Smoke/Hub75PriorityArbitrationTests.cs`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/modules/paineis.md`
- `docs/handoffs/2026-04-22-device-client-abstractions.md`

## Decisoes tomadas

1. Criar `Device.Client.Abstractions` como assembly de contratos, com referencia somente a `Device.Protocol`.
2. Mover `IDeviceServerClient`, `IDeviceFrameTransport` e `PanelsBatchRegistration` para namespace `Device.Client`.
3. Manter `IDeviceServerHost` em `Device.Server.Abstractions`, herdando o transporte de frames vindo do assembly client.
4. Manter `DeviceIntegrationService` como adaptador embedded do WinUI, sem introduzir client HTTP/WS remoto.
5. Fazer `Output` depender somente de `Device.Client.Abstractions`, preservando o wire `StreamFrameV2` e o runtime local.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~Esp32S3LedOutputTests|FullyQualifiedName~LedOutputLifecycleTests|FullyQualifiedName~DeviceIntegrationServiceLegacyWsSettingTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests|FullyQualifiedName~DeviceOperationsCoordinatorDeviceLogsTests" -> PASS (25/25)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj --filter "FullyQualifiedName~Hub75PriorityArbitrationTests|FullyQualifiedName~WinUiBootstrapSmokeTests" -> PASS (14/14)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> PASS
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> PASS
dotnet build MicaAudio.sln -c Debug -> PASS (0 warnings, 0 errors)
```

## Riscos e rollback

- Risco principal: algum consumidor ainda referenciar `Device.Server.Abstractions` apenas para contratos de client, recriando acoplamento ao host embedded.
- Como reverter: mover os tres contratos de volta para os assemblies anteriores, restaurar referencias de projeto de `Output`/`App.WinUI` e remover `Device.Client.Abstractions` da solucao.

## Proximos passos

1. Antes de criar `MicaAudio.Server.exe`, definir persistencia/configuracao propria do processo server e o boundary real de transporte remoto.
2. Quando existir client remoto, implementar um `Device.Client.*` concreto sem alterar os DTOs wire atuais.
