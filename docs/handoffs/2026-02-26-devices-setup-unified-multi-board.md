# Handoff — vNext multi-dispositivo + setup unificado

## Objetivo
Entregar gerenciamento simultaneo por dispositivo, unificar setup/download de firmware na aba Dispositivos e adicionar suporte de metadados/firmware para ESP32-S3 DevKitC-1 (WROOM-1 N8R2/N16R8).

## Escopo classificado
- estrutural
- firmware/protocolo

## Arquivos alterados
- src/App.WinUI/Services/Devices/DeviceCommandExecutionState.cs
- src/App.WinUI/Services/Devices/DeviceOperationsState.cs
- src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs
- src/App.WinUI/Views/DevicesPage.Ui.cs
- src/App.WinUI/Views/DevicesPage.xaml.cs
- src/App.WinUI/Views/AppsPage.xaml.cs
- src/App.WinUI/Views/ShellPage.xaml
- src/App.WinUI/Views/ShellPage.xaml.cs
- src/App.WinUI/App.xaml.cs
- src/App.WinUI/Services/Firmware/PrecompiledFirmwareOption.cs
- src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs
- src/Device.Protocol/Models/PairDeviceRequest.cs
- src/Device.Protocol/Models/DeviceTelemetryMessage.cs
- src/Device.Protocol/Models/DeviceRecord.cs
- src/Device.Protocol/Models/DeviceSnapshot.cs
- src/Device.Server/Hosting/DeviceServerHost.cs
- src/Device.Server/Hosting/DeviceServerHost.Advanced.cs
- src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs
- firmware/matrixportal-s3/platformio.ini
- firmware/matrixportal-s3/src/main.cpp
- tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs
- tests/Integration.Smoke/FirmwareCatalogSmokeTests.cs
- tests/Output.Tests/DeviceServerHostSecurityTests.cs
- docs/wiki/README.md
- docs/wiki/modules/app-winui.md
- docs/wiki/modules/device-operations-coordinator.md
- docs/wiki/modules/device-server-protocol.md
- docs/wiki/modules/server-build-and-artifacts.md
- docs/wiki/modules/firmware-matrixportal-s3.md
- docs/wiki/guides/setup-new-device.md
- docs/wiki/guides/build-export-firmware.md
- docs/wiki/guides/debug-ota-http-failure.md
- docs/wiki/reference/code-index.md
- docs/wiki/reference/http-api-v1.md
- docs/wiki/reference/ws-protocol-v1.md
- docs/wiki/reference/troubleshooting-matrix.md

## Decisoes tomadas
1. Concorrencia de comandos agora e por dispositivo: 1 comando simultaneo por device, sem bloqueio global entre devices.
2. A aba Servidor saiu do menu principal do Shell; funcoes de setup/download foram movidas para Dispositivos.
3. Wizard de Novo dispositivo implementado com placa, painel e perfil de firmware.
4. Pinagem manual no wizard mantida apenas como referencia local (nao altera binario).
5. Catalogo de firmware expandido para placa/painel/perfil, mantendo compatibilidade com ids antigos `stable` e `dma_exp`.
6. Protocolo/persistencia ganharam `BoardModel` e `PanelType` com fallback para payloads legados.
7. Firmware ganhou variantes de placa por macro e metadata de board/panel em pareamento e telemetria.

## Validacoes executadas
1. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` (sucesso).
2. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` (sucesso).
3. `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug` (sucesso).
4. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug` (sucesso; 43 aprovados).
5. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug` (falha local esperada por APPX3217, sem SDK UAP instalado).

## Riscos e rollback
1. Os BINs DevKitC-1 ainda nao estao embarcados nesta entrega; o wizard informa erro claro quando o arquivo nao existe.
2. Alteracao no Shell remove acesso direto a ServerPage; rollback rapido e reintroduzir item `server` em `ShellPage` e registro DI correspondente.
3. Mudanca de estado por dispositivo pode impactar telas que dependiam do status global; fallback global foi preservado em `DeviceOperationsState`.

## Proximos passos
1. Gerar e embarcar `esp32s3-devkitc1-stable_merged.bin` e `esp32s3-devkitc1-dma_exp_merged.bin` para fechar fluxo sem erro de arquivo ausente.
2. Adicionar testes especificos de concorrencia por device no `DeviceOperationsCoordinator`.
3. Rodar gate completo no CI (`dotnet build MicaAudio.sln -c Debug`) para validar ambiente com SDK UAP.
