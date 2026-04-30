# Modulo Server Build And Artifacts

## Direcao oficial

- `MicaAudio.Server` e o artefato oficial do control plane.
- O host standalone existe para pairing, assets, ownership metadata, catalogo, telemetria e administracao.
- Ele volta a ser o hot path oficial do visualizador remoto: cliente envia frames ao server e o server entrega ao ESP.

## Server standalone

- `MicaAudio.Server` e o primeiro executavel standalone do control plane, mantendo `Device.Server` como biblioteca de hosting/transportes.
- O projeto publica o dashboard estatico de `Device.Server/wwwroot/dashboard` para `wwwroot/dashboard` no output/publish do executavel.
- Configuracao operacional:
  - `PORT` sobrescreve a porta HTTP para Render;
  - `MICA_SERVER__*` configura o runtime standalone;
  - `MICA_SERVER__PUBLICHTTPBASEADDRESS` define a base HTTP anunciada para firmware/clients quando o bind interno difere da porta publica;
  - `MICA_SERVER__PUBLICHOST` define o host MQTT anunciado para uso local/legado;
  - `MICA_SERVER__VISUALUDPPORT` define a porta UDP LAN anunciada para visual opt-in (`5274` por default);
  - `MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=true` permite o host preferir UDP LAN servidor->ESP para `Bins128` quando o device anunciar suporte; no Docker local fica ligado por default;
  - `MICA_SERVER__TRUSTEDLANAUTOREGISTRATION=true` habilita auto-registro LAN por UDP discovery;
  - `MICA_SERVER__DISCOVERYUDPPORT` define a porta UDP discovery (`5275` por default);
  - `MICA_SERVER__MAXMEDIAUPLOADBYTES` define o limite de upload de midia da biblioteca (`20971520` por default);
  - `MICA_SERVER__PANELSAUTORUNTIMEENABLED=true` liga o runtime autonomo server-owned de paineis (`true` por default);
  - `MICA_SERVER__STORAGEROOT` define onde o standalone grava `devices.json`, `panels/panels.json` e `media/*`.
- `src/MicaAudio.Server/Dockerfile` usa build multi-stage com imagens oficiais .NET 10 e `render.yaml` define Web Service Docker com health check em `/api/v1/health`.
- Docker local em workspace/dev deve usar o helper oficial, que rebuilda a imagem, para/remove apenas o container `mica-audio-server`, sobe a nova versao com volume persistente e anuncia automaticamente o IP LAN do PC:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1
```

- Defaults do helper: imagem `micaaudio-server:dev`, container `mica-audio-server`, HTTP externo `5272`, MQTT `5273`, transporte visual UDP servidor->ESP em `5274/udp`, UDP discovery `5275/udp`, runtime server-owned de paineis ligado, volume Docker `mica-audio-server-data` montado em `/data` e `MICA_SERVER__STORAGEROOT=/data`.
- Para forcar um IP especifico, usar `-PublicHost <IP_DO_PC>`. Para acompanhar logs apos subir, usar `-FollowLogs`. Para ver os comandos sem executar, usar `-DryRun`. Para troubleshooting especifico por WS, usar `-DisableVisualUdp`, que define `MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=false` e nao publica `5274/udp`.
- No fluxo normal, o firmware precisa apenas de Wi-Fi; `Servidor` no portal AP fica opcional como fallback tecnico. Se usado manualmente, informe `http://<IP_DO_PC>:5272`; nao use `localhost` nem `127.0.0.1` para um ESP fisico.
- UDP visual e LAN-only; no helper Docker local ele e o default oficial no trecho servidor->ESP, e `5274/udp` deve estar liberado no firewall/host Docker.
- UDP discovery e LAN-only; sem `MICA_SERVER__TRUSTEDLANAUTOREGISTRATION=true` ou sem `-p 5275:5275/udp`, o firmware nao aparece automaticamente no client e o pareamento legado fica como compatibilidade.
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
- O `firmwareVersion` do pacote oficial agora usa carimbo `UTC timestamp + tag/ou untagged + short commit`:
  - formato: `vyyyy.MM.dd-HHmmssZ-<tag-or-untagged>-<sha>`
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
- [ServerOwnedPanelsRuntimeService](../../../src/MicaAudio.Server/ServerOwnedPanelsRuntimeService.cs#L1)
- [MicaAudioServerOptions](../../../src/MicaAudio.Server/MicaAudioServerOptions.cs#L1)
- [StandaloneDeviceRegistryStore](../../../src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs#L1)
- [MicaAudio.Server Dockerfile](../../../src/MicaAudio.Server/Dockerfile#L1)
- [Docker server redeploy](../../../scripts/docker-server-redeploy.ps1#L1)
- [Render Blueprint](../../../render.yaml#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [AppData Firmware](../../../src/App.WinUI/AppData/Firmware)
