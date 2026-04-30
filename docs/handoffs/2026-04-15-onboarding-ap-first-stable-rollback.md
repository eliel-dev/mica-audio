# Handoff - 2026-04-15 - onboarding-ap-first-stable-rollback

## Objetivo

Restaurar o onboarding oficial do ESP32-S3 para o baseline estavel `COM -> flash -> pair code -> AP imediato`, revertendo a janela `serial-first` no boot limpo e endurecendo o perfil oficial do firmware/artifacts apos o split em modulos.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui: firmware ESP32-S3 (`setup/provisioning/network`), wizard WinUI, service de onboarding, frescor do release oficial, smoke/observability tests e documentacao operacional.
- Nao inclui: remocao definitiva de `mica.serial.v1`, mudanca de contrato wire WS/HTTP ou redesign do portal AP.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `firmware/esp32s3-devkitc1/boards/mica_esp32_s3_devkitc1_n16r8.json`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceOnboardingModels.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/Views/DevicesPage.Onboarding.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `tests/Integration.Smoke/DeviceUsbOnboardingServiceTests.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `tests/Output.Tests/OnboardingObservabilityTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. O firmware volta a abrir o portal AP imediatamente quando faltar `host/porta/deviceId/token` no boot.
2. O caminho oficial do wizard deixa de depender de `SerialProvisioningClient`; o sucesso do onboarding desktop volta a ser “flash concluido + pair code exibido”.
3. `mica.serial.v1` permanece no codigo apenas como compatibilidade/diagnostico, fora do fluxo feliz.
4. O perfil oficial da placa passa a incluir `ARDUINO_USB_CDC_ON_BOOT=1` junto de `ARDUINO_USB_MODE=1`.
5. O preflight de frescor do release oficial passa a observar toda a arvore `firmware/esp32s3-devkitc1/src`, nao apenas `main.cpp`.
6. A tentativa `serial-first` documentada em `2026-04-14-serial-first-onboarding.md` deve ser tratada como experimento revertido; o baseline oficial volta a ser AP-first.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK.
- `dotnet build MicaAudio.sln -c Debug` -> OK.
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DeviceUsbOnboardingServiceTests|FullyQualifiedName~DevicesPageSmokeTests"` -> OK (12/12).
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~OnboardingObservabilityTests"` -> OK (2/2).
- `powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1` -> OK.
- `platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1` -> OK.

## Riscos e rollback

- Risco: quem tentou usar o provisioning serial como fluxo principal perde esse caminho no wizard.
  - Mitigacao: `mica.serial.v1` continua disponivel para diagnostico e compatibilidade futura.
- Risco: o usuario nao copiar o `pair code` apos o flash.
  - Mitigacao: o app mostra instrucoes explicitas no footer com `pair code` + AP + `Servidor`.
- Rollback:
  1. restaurar a janela `serial-first` em `main.cpp` e `mica_network.cpp`;
  2. recolocar `SSID/senha/deviceName` no wizard e a dependencia de `SerialProvisioningClient`;
  3. revalidar release oficial e documentacao.

## Proximos passos

1. Fazer smoke manual em bancada com `erase-all + flash` para confirmar que o SSID `MicaAudio-Setup-xxxx` aparece imediatamente no celular.
2. Confirmar em hardware que o HUB75 entra em `SETUP WIFI` enquanto o portal estiver aberto.
3. Validar monitor serial USB a `115200` com o perfil oficial novo (`USB CDC on boot`).
4. Se a trilha serial continuar relevante, reintroduzi-la apenas como fluxo secundario explicitamente smokeado, nunca mais como baseline sem bancada.
