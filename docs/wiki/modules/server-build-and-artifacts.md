# Modulo Server Build And Artifacts

Artefato oficial de firmware embarcado:
1. `esp32s3-devkitc1-128x64-dma_exp_merged.bin`
2. `esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`

O catalogo ativo nao expoe mais Matrix Portal S3, painel `64x32` nem o perfil `stable`.

## Manifesto oficial

- O manifesto embarcado no app agora usa `schemaVersion = 2`.
- Campos minimos relevantes:
  - `firmwareVersion`
  - `sha256`
  - `fileSizeBytes`
  - `boardModel`
  - `panelType`
  - `profile`
  - `controlPlane`
- O `firmwareVersion` do pacote oficial agora usa carimbo `UTC timestamp + tag + short commit`:
  - formato: `vyyyy.MM.dd-HHmmssZ-<tag>-<sha>`
  - duas geracoes no mesmo dia passam a produzir IDs distintos.
- O host local reutiliza esse manifesto para:
  - informar `Ultimo release` no dashboard;
  - decidir `firmwareUpdateAvailable`;
  - servir metadata/download OTA autenticados para o ESP32.

## Referencias de codigo

- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [AppData Firmware](../../../src/App.WinUI/AppData/Firmware)
