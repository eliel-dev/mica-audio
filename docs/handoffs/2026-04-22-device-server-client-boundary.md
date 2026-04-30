# Handoff - Device server client boundary

## Objetivo

Introduzir uma fronteira de client app-level entre WinUI/Output/Paineis e o `IDeviceServerHost`, mantendo o server embutido no processo WinUI.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: consumidores ativos usam `IDeviceServerClient` e/ou `IDeviceFrameTransport`; `DeviceServerHost` permanece registrado apenas no composition root, sem mudanca de wire protocol.

## Arquivos alterados

- `src/Device.Server.Abstractions/Hosting/IDeviceFrameTransport.cs`
- `src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs`
- `src/App.WinUI/Services/Devices/IDeviceServerClient.cs`
- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/Output/Led/Esp32S3LedOutput.cs`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Output.Tests/Esp32S3LedOutputTests.cs`
- `tests/Output.Tests/LedOutputLifecycleTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Integration.Smoke/Hub75PriorityArbitrationTests.cs`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/output-led.md`
- `docs/handoffs/2026-04-22-device-server-client-boundary.md`

## Decisoes tomadas

1. Separar `IDeviceFrameTransport` de `IDeviceServerHost` para manter o hot path visual pequeno e independente de lifecycle, registry e comandos.
2. Manter `IDeviceServerClient` interno ao WinUI nesta entrega, porque ainda nao existe processo remoto nem client HTTP/WS real.
3. Fazer `DeviceIntegrationService` implementar o client app-level para concentrar a adaptacao embedded sem mover persistencia, portas ou `ServerConfig`.
4. Remover o uso ativo de `DeviceIntegrationService.Host`; consumidores passam por `IDeviceServerClient` ou `IDeviceFrameTransport`.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~Esp32S3LedOutputTests|FullyQualifiedName~LedOutputLifecycleTests|FullyQualifiedName~DeviceIntegrationServiceLegacyWsSettingTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests|FullyQualifiedName~DeviceOperationsCoordinatorDeviceLogsTests" --no-restore -> PASS (25/25)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj --filter "FullyQualifiedName~Hub75PriorityArbitrationTests|FullyQualifiedName~WinUiBootstrapSmokeTests" --no-restore -> PASS (14/14)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> PASS
dotnet build MicaAudio.sln -c Debug -> PASS (0 warnings, 0 errors)
```

## Riscos e rollback

- Risco principal: algum consumidor excluido do build voltar a ser compilado e precisar de DI atualizado para `IDeviceFrameTransport`.
- Como reverter: restaurar `Esp32S3LedOutput` e `PanelsPlaybackService` para `IDeviceServerHost`, remover `IDeviceFrameTransport`/`IDeviceServerClient` e registrar novamente os consumidores direto no host.

## Proximos passos

1. Em entrega futura, extrair `Device.Client.*` com implementacao HTTP/WS remota usando a mesma superficie app-level.
2. Antes de criar `MicaAudio.Server.exe`, decidir persistencia/configuracao propria do processo server e estrategia de discovery.
