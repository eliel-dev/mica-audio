# Handoff - Device OTA Update Detection

## Objetivo

Adicionar deteccao de firmware desatualizado no dashboard por device, expor CTA nativo para atualizacao e reintroduzir OTA autenticado por HTTP/MQTT para o ESP32-S3 usando o pacote oficial precompilado do app.

## Escopo classificado

- Tipo: estrutural + firmware/protocolo
- Criterio de aceite:
  - dashboard mostra `Firmware atual` e `Firmware oficial`;
  - CTA `Atualizar firmware` aparece apenas quando existir pacote oficial compativel e a versao atual estiver vazia ou diferente da oficial;
  - device online pode iniciar `update_firmware` por OTA;
  - device offline cai para o fluxo oficial de reflash por USB;
  - host expoe endpoints autenticados de metadata/download de firmware;
  - firmware valida compatibilidade, tamanho e `sha256` antes do reboot.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `scripts/build-precompiled-firmware.ps1`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- `src/App.WinUI/Services/Devices/DeviceCommandDispatcher.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsText.cs`
- `src/App.WinUI/Services/Firmware/FirmwareArtifactManifest.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareCatalogAdapter.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
- `src/App.WinUI/Views/DevicesPage.FirmwareUpdate.cs`
- `src/App.WinUI/Views/DevicesPage.Onboarding.cs`
- `src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs`
- `src/Device.Protocol/Models/DeviceCommandType.cs`
- `src/Device.Protocol/Models/DeviceFirmwareReleaseInfo.cs`
- `src/Device.Server/Hosting/DeviceOfficialFirmwareCatalog.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Firmware.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/wwwroot/dashboard/index.html`
- `src/Device.Server/wwwroot/dashboard/dashboard.css`
- `src/Device.Server/wwwroot/dashboard/dashboard.js`
- `tests/Integration.Smoke/DashboardAssetSmokeTests.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `tests/Integration.Smoke/FirmwareCatalogSmokeTests.cs`
- `tests/Output.Tests/DeviceOperationsCoordinatorBrightnessTests.cs`
- `tests/Output.Tests/DeviceServerHostDashboardTests.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/PrecompiledFirmwareServiceTests.cs`
- `docs/wiki/reference/device-observability-dashboard.md`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/modules/server-build-and-artifacts.md`

## Decisoes tomadas

1. O versionamento canonico permaneceu `vYYYY.MM.DD-tag-sha`; a comparacao de update e por igualdade exata com o manifesto oficial do pacote correspondente.
2. O host recebeu um catalogo neutro (`IDeviceOfficialFirmwareCatalog`) para resolver firmware oficial sem acoplar `Device.Server` ao `PrecompiledFirmwareService`.
3. O CTA de firmware continua simples na UI: online usa OTA como acao principal com USB como fallback; offline oferece apenas USB.
4. O firmware usa `GET /api/v1/device/firmware/latest` + `download` autenticados com as mesmas credenciais do device e valida `sha256/fileSizeBytes` localmente antes do reboot.
5. O pacote oficial embarcado foi regenerado ao final para a nova versao OTA-capable `v2026.03.12-untagged-8fc3a7e`.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~PrecompiledFirmwareServiceTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests|FullyQualifiedName~DeviceServerHostDashboardTests|FullyQualifiedName~DeviceServerHostMqttTests|FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~DeviceSessionTests" -> OK (32 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-build --filter "FullyQualifiedName~DashboardAssetSmokeTests|FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~FirmwareCatalogSmokeTests" -> OK (9 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -> OK
C:\Users\eliels\AppData\Local\Programs\Python\Python313\Scripts\pio.exe run -e esp32s3_devkitc1_dma_exp -> OK
```

## Riscos e rollback

- Risco principal: a OTA depende de conectividade HTTP estavel durante o download; falhas de rede em campo vao manter o CTA visivel e cair para USB.
- Como reverter:
  - remover `update_firmware` do mapeamento wire;
  - esconder o CTA no dashboard;
  - manter apenas o fluxo oficial de USB + firmware precompilado;
  - apontar o catalogo oficial novamente para manifesto sem OTA.

## Proximos passos

1. Testar em hardware real o caminho completo `CTA -> OTA -> reboot -> dashboard sem CTA`.
2. Se o fluxo USB for usado com frequencia para recovery, considerar um wizard dedicado de `reflash de dispositivo existente` em vez de reutilizar o onboarding.
3. Se o warning recorrente de `WEBSOCKETS_MAX_DATA_SIZE` continuar incomodando, alinhar o define do projeto com o valor da dependencia para remover o ruido de build.
