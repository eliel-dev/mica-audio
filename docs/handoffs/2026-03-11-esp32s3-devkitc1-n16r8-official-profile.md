# Handoff - Perfil oficial N16R8 do ESP32-S3-DevKitC-1

## Objetivo

Oficializar no repositorio o perfil N16R8 validado em hardware para o firmware `dma_exp` do DevKitC-1, eliminando a dependencia do board padrao `esp32-s3-devkitc-1` do PlatformIO, que estava configurado localmente como `8MB sem PSRAM`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - o env oficial `esp32s3_devkitc1_dma_exp` usa board local N16R8;
  - o build fixa `QIO 80MHz`, `16MB flash`, `qio_opi` e particao local `3MB APP / 9.9MB FATFS`;
  - o pacote precompilado oficial continua com os mesmos nomes de artefato;
  - o manifesto empacotado passa a ser gerado junto com o `merged.bin`;
  - wiki e code index registram o novo baseline e o requisito de erase total na primeira gravacao apos a migracao.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/boards/mica_esp32_s3_devkitc1_n16r8.json`
- `firmware/esp32s3-devkitc1/partitions/mica_app3M_fat9M_16MB.csv`
- `scripts/build-precompiled-firmware.ps1`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. O env oficial foi mantido como `esp32s3_devkitc1_dma_exp` para preservar onboarding, scripts e identidade logica do device.
2. O board local usa `variant = esp32s3`, nao `esp32_s3r8n16`, porque o pinout correto do DevKitC-1 do projeto continua sendo o do variant `esp32s3`.
3. A particao oficial foi versionada no repositorio como CSV local para evitar dependencia implicita de aliases do framework instalado.
4. O script de build precompilado passou a gerar tambem o manifesto JSON, reduzindo drift entre `merged.bin` e `manifest.json`.

## Validacoes executadas

```text
pio run -e esp32s3_devkitc1_dma_exp -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
```

## Riscos e rollback

- Risco principal:
  - algum lote diferente de DevKitC-1 depender de outro layout fisico de flash/PSRAM e nao ser compativel com o baseline N16R8.
- Como reverter:
  - apontar o env oficial de volta para o board anterior;
  - remover o board local e a particao local;
  - regenerar o pacote precompilado anterior.

## Proximos passos

1. Fazer smoke manual em hardware com erase total na primeira gravacao apos a migracao.
2. Confirmar em telemetria `psramAvailable = true` e totais reais coerentes apos boot e reconexao MQTT.
3. Se surgir um segundo lote de hardware com configuracao distinta, separar em env dedicado em vez de relaxar o baseline oficial.
