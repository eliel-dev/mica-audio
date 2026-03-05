# Handoff - 2026-03-05 - onboarding-rollback-com-flash-ap

## Objetivo

Restaurar o fluxo de onboarding para o modo operacional estavel: wizard apenas com selecao de porta COM e flash, exibindo `pair code` ao final, enquanto o ESP32 volta a provisionar Wi-Fi via AP.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui: `App.WinUI` (wizard + onboarding service), firmware ESP32 (`setup/provisioning`), smoke/unit tests e documentacao operacional.
- Nao inclui: mudanca de contrato wire WS/HTTP, remocao definitiva de `mica.serial.v1`.

## Arquivos alterados

- `src/App.WinUI/Services/Devices/Onboarding/DeviceOnboardingModels.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `tests/Integration.Smoke/DeviceUsbOnboardingServiceTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

1. Onboarding oficial deixou de depender de `SerialProvisioningClient` no caminho principal.
2. Wizard foi reduzido para etapa unica (porta COM + flash).
3. `DeviceOnboardingResult` passou a carregar `PairCode` para exibicao imediata na UI.
4. Firmware abre provisioning AP imediatamente quando detectar config/credenciais incompletas no boot.
5. `mica.serial.v1` permanece no codigo para compatibilidade futura, mas fora do fluxo default.

## Validacoes executadas

- `dotnet build MicaAudio.sln -c Debug` -> OK (com warnings preexistentes de analise/Win2D).
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~EspToolFlashServiceTests|FullyQualifiedName~DeviceUsbOnboardingServiceTests"` -> OK (10/10).
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceOperationsCoordinator|FullyQualifiedName~DeviceServerHostSecurityTests"` -> OK (28/28).
- `platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK.

## Riscos e rollback

- Risco: usuario fechar modal sem copiar `pair code`.
  - Mitigacao: texto persistido no footer/log local.
- Risco: provisioning AP nao concluir em bancada por credencial incorreta.
  - Mitigacao: AP permanece ativo no portal sem timeout.
- Rollback:
  1. Reverter commit do wizard/service.
  2. Reverter `setup()` do firmware para comportamento anterior.

## Proximos passos

1. Validar smoke manual em bancada: flash, AP visivel, pareamento por codigo.
2. Reavaliar em fase separada se `mica.serial.v1` sera removido ou mantido como fallback oficial.
