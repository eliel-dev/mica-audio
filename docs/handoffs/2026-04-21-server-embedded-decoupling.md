# Handoff - Server embutido desacoplado

## Objetivo

Separar os contratos do servidor de dispositivos em um assembly de abstracoes, mantendo o runtime embutido no WinUI e preservando o protocolo HTTP/WS/MQTT atual.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `Output` e consumidores WinUI dependem de `IDeviceServerHost` via `Device.Server.Abstractions`, enquanto `Device.ServerHost` permanece como implementacao embutida sem mudanca de wire protocol.

## Arquivos alterados

- `src/Device.Server.Abstractions/Device.Server.Abstractions.csproj`
- `src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceOfficialFirmwareCatalog.cs`
- `src/Device.Server.Abstractions/Hosting/PanelsBatchRegistration.cs`
- `MicaAudio.sln`
- `src/Device.Server/Device.Server.csproj`
- `src/Device.Server/Hosting/IDeviceServerHost.cs` (movido para `Device.Server.Abstractions`)
- `src/Device.Server/Hosting/DeviceOfficialFirmwareCatalog.cs` (movido para `Device.Server.Abstractions`)
- `src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs`
- `src/Output/Output.csproj`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Output.Tests/DeviceIntegrationServiceLegacyWsSettingTests.cs`
- `tests/Output.Tests/Esp32S3LedOutputTests.cs`
- `tests/Output.Tests/LedOutputLifecycleTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `src/Device.Server.Abstractions/packages.lock.json`
- `src/Device.Server/packages.lock.json`
- `src/Output/packages.lock.json`
- `src/App.WinUI/packages.lock.json`
- `tests/Output.Tests/packages.lock.json`
- `tests/Integration.Smoke/packages.lock.json`
- `BenchmarkSuite1/packages.lock.json`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/reference/code-index.md`
- `docs/handoffs/2026-04-21-server-embedded-decoupling.md`

## Decisoes tomadas

1. Manter o namespace `Device.Server.Hosting` nos contratos movidos para reduzir churn nos consumidores e preservar imports existentes.
2. Criar `Device.Server.Abstractions` como `net10.0` com referencia apenas a `Device.Protocol`, evitando dependencias de ASP.NET Core, MQTTnet ou WinUI.
3. Expor `RegisterPanelsBatch` e `ClearPanelsBatches` em `IDeviceServerHost` porque `PanelsPlaybackService` ja usa esse contrato operacional no modo embutido.
4. Manter `DeviceServerHost` registrado somente no composition root do WinUI; os servicos de output e paineis recebem `IDeviceServerHost`.

## Validacoes executadas

```text
dotnet test tests\Output.Tests\Output.Tests.csproj --filter ServerAbstractionBoundaryTests -> PASS
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj --filter PanelsPlaybackService_ShouldDependOnDeviceServerAbstraction -> PASS
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj --filter WinUiBootstrapSmokeTests -> PASS
dotnet test tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~DeviceServerHostPanelsBatchTests|FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~DeviceServerHostTargetedFrameTests|FullyQualifiedName~Esp32S3LedOutputTests|FullyQualifiedName~ServerAbstractionBoundaryTests" -> PASS (35/35)
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj --filter "FullyQualifiedName~Hub75PriorityArbitrationTests|FullyQualifiedName~WinUiBootstrapSmokeTests" -> PASS (13/13)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> PASS
dotnet build MicaAudio.sln -c Debug -> PASS (0 warnings, 0 errors)
```

## Riscos e rollback

- Risco principal: algum consumidor indireto depender do assembly concreto `Device.Server` para usar apenas contratos.
- Como reverter: restaurar `IDeviceServerHost` e `DeviceOfficialFirmwareCatalog` para `src/Device.Server/Hosting`, devolver a referencia de `Output` para `Device.Server` e remover `Device.Server.Abstractions` da solucao.

## Proximos passos

1. Em uma proxima entrega, introduzir um client HTTP/WS para permitir server-process sem trocar os consumidores.
2. Avaliar persistencia/configuracao propria do servidor antes de criar `MicaAudio.Server.exe`.
