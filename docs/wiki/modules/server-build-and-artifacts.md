# Modulo Server Build And Artifacts

## Server standalone

- `MicaAudio.Server` e o primeiro executavel standalone do control plane, mantendo `Device.Server` como biblioteca de hosting/transportes.
- O projeto publica o dashboard estatico de `Device.Server/wwwroot/dashboard` para `wwwroot/dashboard` no output/publish do executavel.
- Configuracao operacional:
  - `PORT` sobrescreve a porta HTTP para Render;
  - `MICA_SERVER__*` configura o runtime standalone;
  - `MICA_SERVER__STORAGEROOT` define onde `StandaloneDeviceRegistryStore` grava `devices.json`.
- `src/MicaAudio.Server/Dockerfile` usa build multi-stage com imagens oficiais .NET 10 e `render.yaml` define Web Service Docker com health check em `/api/v1/health`.
- O smoke Render desta fase valida runtime HTTP/WS publico; operacao cloud completa de firmware e WinUI remoto ficam para fases posteriores.

Artefato oficial de firmware embarcado:
1. `esp32s3-devkitc1-128x64-dma_exp_merged.bin`
2. `esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`

O catalogo ativo nao expoe mais Matrix Portal S3, painel `64x32` nem o perfil `stable`.

## Refresh oficial em workspace/dev

- O release oficial consumido pelo dashboard, OTA e download manual continua sendo um pacote sidecar oficial do app, nao um `firmware.bin` arbitrario da pasta `.pio/build`.
- Rodar apenas `pio run` recompila o firmware bruto, mas nao atualiza sozinho o catalogo oficial local do app.
- A fonte oficial continua sendo o script:
  - `scripts/build-precompiled-firmware.ps1`
- Em workspace/dev, a `App.WinUI` agora faz duas coisas:
  - warm-up assincromo no startup para detectar se o release oficial local ficou stale em relacao aos fontes do firmware;
  - preflight obrigatorio antes de OTA e antes do download manual do BIN, regenerando o pacote oficial quando necessario.
- O frescor do release oficial e comparado contra os insumos reais do firmware:
  - `scripts/build-precompiled-firmware.ps1`
  - `firmware/esp32s3-devkitc1/platformio.ini`
  - `firmware/esp32s3-devkitc1/src/main.cpp`
  - `firmware/esp32s3-devkitc1/src/firmware_version.h`
  - `firmware/esp32s3-devkitc1/boards/`
  - `firmware/esp32s3-devkitc1/partitions/`
  - `firmware/esp32s3-devkitc1/scripts/`
- Se o app identificar release stale e a regeneracao oficial falhar, o catalogo deixa de anunciar `Ultimo release` em vez de continuar expondo o pacote velho como se estivesse atual.
- Os botoes `Baixar firmware` em `Dispositivos` e `Servidor` tambem falham de forma estrita quando o app nao consegue provar que o release oficial esta fresco.
- O download manual continua copiando o mesmo binario oficial interno, mas o nome sugerido ao usuario no `FileSavePicker` agora incorpora `firmwareVersion` do manifesto oficial.

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
- Em workspace/dev, o `builtAtUtc` do manifesto e a referencia canonica para decidir se o release oficial ainda representa o estado atual do firmware fonte.

## Referencias de codigo

- [MicaAudio.Server](../../../src/MicaAudio.Server/MicaAudio.Server.csproj#L1)
- [MicaAudioServerBootstrap](../../../src/MicaAudio.Server/MicaAudioServerBootstrap.cs#L1)
- [MicaAudioServerRuntime](../../../src/MicaAudio.Server/MicaAudioServerRuntime.cs#L1)
- [StandaloneDeviceRegistryStore](../../../src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs#L1)
- [MicaAudio.Server Dockerfile](../../../src/MicaAudio.Server/Dockerfile#L1)
- [Render Blueprint](../../../render.yaml#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [AppData Firmware](../../../src/App.WinUI/AppData/Firmware)
