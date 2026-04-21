# Handoff - 2026-04-20 - remove-usb-flash-flow

## Objetivo

Remover completamente o fluxo de flash USB do desktop e consolidar o caminho oficial em download manual do firmware + OTA apenas para devices online.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `DevicesPage` sem wizard USB nem `Novo dispositivo`, dashboard exibindo firmware offline mas oferecendo CTA de update apenas para OTA online, servicos USB/esptool removidos do app e testes/build passando.

## Arquivos alterados

- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.Onboarding.cs`
- `src/App.WinUI/Views/DevicesPage.FirmwareUpdate.cs`
- `src/App.WinUI/Views/DevicesPage.ListState.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs`
- `src/Device.Server/wwwroot/dashboard/dashboard.js`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `tests/Integration.Smoke/DashboardAssetSmokeTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/device-observability-dashboard.md`
- `docs/wiki/guides/build-export-firmware.md`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/guides/debug-ota-http-failure.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

Arquivos removidos:

- `src/App.WinUI/Views/DevicesPage.WizardSerial.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialProvisioningClient.cs`
- `src/App.WinUI/Infrastructure/Serial/WizardSerialMonitorPolicy.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceOnboardingModels.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs`
- `src/App.WinUI/Services/Devices/Onboarding/IEspToolFlashService.cs`
- `tests/Integration.Smoke/DeviceUsbOnboardingServiceTests.cs`
- `tests/Integration.Smoke/EspToolFlashServiceTests.cs`
- `tests/Output.Tests/OnboardingObservabilityTests.cs`
- `tests/Output.Tests/WizardSerialMonitorPolicyTests.cs`

## Decisoes tomadas

1. O corte foi minimo e estrutural: removemos apenas o caminho USB/esptool do desktop e preservamos OTA, download manual, `Parear`, `Copiar host` e `Copiar link do dashboard`.
2. `PrecompiledFirmwareService` foi mantido como fonte unica do "ultimo firmware"; nao foi adicionada consulta remota de release nem manifesto externo.
3. `FirmwareUpdateAvailable` passou a significar "ha upgrade e o device esta online para OTA agora", tanto no DTO do servidor quanto no gating do dashboard web.
4. `SerialProvisioningClient` e `WizardSerialMonitorPolicy` foram removidos junto com o onboarding porque ficaram sem consumidores reais depois do corte.
5. Os testes de onboarding USB e das policies removidas foram deletados em vez de adaptados, porque o contrato correspondente deixou de existir.

## Validacoes executadas

```text
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~DashboardAssetSmokeTests|FullyQualifiedName~FirmwareCatalogSmokeTests" -> OK (14/14)
```

## Riscos e rollback

- Risco principal: bancada que dependia do wizard USB interno agora precisa de ferramenta externa para gravacao e captura serial.
- Como reverter: restaurar os arquivos removidos de onboarding USB, religar o DI em `App.xaml.cs`, reintroduzir o overlay na `DevicesPage` e restaurar os testes deletados.

## Proximos passos

1. Rodar a bateria completa obrigatoria (`docs-validate`, `ai-governance-check`, `mvvm-validate`, `dotnet build` e o filtro oficial de smoke).
2. Validar manualmente em bancada o fluxo real `Baixar firmware -> flash externo -> Parear -> Copiar host -> AP MicaAudio-Setup-xxxx`.
