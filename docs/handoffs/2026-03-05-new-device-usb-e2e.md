# Handoff - Novo Dispositivo USB End-to-End (Fase 2)

## Objetivo

Implementar onboarding completo via USB no WinUI: flash do firmware, provisionamento serial `mica.serial.v1`, pareamento automatico e verificacao de device online.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui:
  - novos servicos de onboarding no app;
  - auto-detect de portas COM com heuristica VID/PID;
  - flasher com `esptool`;
  - protocolo serial entre app e firmware;
  - ajuste de boot/fallback para portal no firmware.

## Arquivos alterados

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/Infrastructure/Serial/SerialPortDescriptor.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialPortCatalogService.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialProvisioningClient.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceOnboardingModels.cs`
- `src/App.WinUI/Services/Devices/Onboarding/IEspToolFlashService.cs`
- `src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `tools/esptool/win-x64/esptool.cmd`
- `tools/esptool/win-x64/README.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. Credencial Wi-Fi continua efemera no app (nao persistida em settings/store/logs).
2. Pairing code permanece oculto e automatico.
3. Porta COM usa auto-detect por VID/PID (`303A`, `10C4`, `1A86`, `0403`) com fallback manual.
4. Firmware passou a publicar `hello` serial periodico e aceitar `provision` JSONL.
5. Boot sem configuracao valida aguarda onboarding serial e cai em portal por fallback temporal.

## Validacoes executadas

1. `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug` -> OK.
2. `platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1` -> OK.
3. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceOperationsCoordinator|FullyQualifiedName~DeviceServerHostSecurityTests"` -> OK.
4. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"` -> OK.
5. `dotnet test MicaAudio.sln -c Debug --no-build` -> OK.
6. `dotnet build MicaAudio.sln -c Debug` -> OK.
7. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK.
8. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK.

## Riscos e rollback

- Risco: ambiente final sem `esptool.exe` local pode depender do fallback `python -m esptool`.
- Risco: firmware antigo sem `mica.serial.v1` nao participa do onboarding USB novo.
- Rollback app: ocultar fluxo novo e voltar para onboarding/manual via portal AP.
- Rollback firmware: reflash do BIN anterior estavel.

## Proximos passos

1. Empacotar `esptool.exe` em `tools/esptool/win-x64` no pipeline de release.
2. Teste manual em bancada com 3 cenarios:
   - sem credenciais;
   - credencial invalida;
   - credencial valida + verificacao online.
3. Adicionar testes unitarios dedicados para `SerialProvisioningClient` e `DeviceUsbOnboardingService`.
