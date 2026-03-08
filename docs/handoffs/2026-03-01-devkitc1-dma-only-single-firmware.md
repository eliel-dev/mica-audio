# Handoff - DevKitC-1 DMA-only Single Firmware

## Objetivo

Consolidar a base em um unico firmware oficial (`dma_exp`) para o painel HUB75 P2.5 128x64 e preparar o build real nesta maquina via `python`.

## Escopo classificado

Mudanca `firmware/protocolo`: remove `stable` do fluxo ativo em firmware, app, metadados e build script.

## Arquivos alterados

- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
- `src/App.WinUI/Views/ServerPage.xaml`
- `src/App.WinUI/Views/ServerPage.xaml.cs`
- `src/App.WinUI/Views/ServerPage.Ui.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `firmware/matrixportal-s3/platformio.ini`
- `firmware/matrixportal-s3/src/main.cpp`
- `firmware/matrixportal-s3/include/README.txt`
- `scripts/build-precompiled-firmware.ps1`
- `tests/Integration.Smoke/FirmwareCatalogSmokeTests.cs`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/firmware-matrixportal-s3.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. `stable` saiu do catalogo ativo, da UI de download e dos fallbacks de profile.
2. `dma_exp` passou a ser o unico firmware oficial da base.
3. O script oficial de build foi simplificado para `python` + target unico.
4. O firmware ficou com env unico `esp32s3_devkitc1_dma_exp`.

## Validacoes executadas

1. Build .NET e smoke do catalogo de firmware (apos install/build do toolchain nesta mesma entrega).
2. Validacoes de docs/governanca.
3. Validacao do artefato gerado em `src/App.WinUI/AppData/Firmware/`.

## Riscos e rollback

1. Registros legados com `stable` dependem da normalizacao para `dma_exp`.
2. Se o build de firmware falhar por dependencias externas, o app continua coerente mas sem o BIN novo.
3. Rollback: reintroduzir `stable` no catalogo, restaurar env antigo do firmware e o script multi-target.

## Proximos passos

1. Gerar o BIN real `esp32s3-devkitc1-128x64-dma_exp_merged.bin` nesta maquina.
2. Validar em hardware real no DevKitC-1.
