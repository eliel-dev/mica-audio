# ADR 0010 - Remote-only device server, STA-hardcoded firmware e widgets autonomos no servidor

## Contexto

A baseline `Funcionando100` (commit `d7699be`) ainda carregava tres acoplamentos
herdados:

1. O `App.WinUI` hospedava o `DeviceServer` como adapter embedded
   (`Device.Client.Embedded`), oferecendo um modo dual (embedded vs remoto).
   Em produc&atilde;o solo so usavamos remoto, mas o codigo do embedded carregava
   metade da arquitetura: `JsonDeviceRegistryStore`, `EmbeddedDeviceServerSettingsProvider`,
   `DeviceServerHost`, stores in-memory, registro condicional do composition root.
2. O firmware ESP32-S3 carregava `WiFiManager` e abria um portal AP
   (`MicaAudio-Setup-XXXX`) sempre que faltavam credenciais. O portal exigia um
   pair code digitado pelo celular para chamar `POST /api/v1/pair`. Em fluxo
   solo isso era ruido: cada device novo precisava do mesmo ritual manual.
3. O compositor de paineis (`PanelsFrameComposer`, 845 linhas) so existia no
   WinUI. Quando o desktop fechava, o painel ativo (relogio, GIFs, imagens)
   deixava de ser desenhado mesmo quando o widget nao precisava de dados do
   cliente.

A meta combinada com o usuario foi simplificar drasticamente a topologia: um
unico modo de servidor (`MicaAudio.Server` standalone), firmware com Wi-Fi e
endereco do servidor hardcoded, e widgets autonomos rodando no servidor para
sobreviver ao fechamento do WinUI.

## Decisao

1. **Remover o modo embedded.** `Device.Client.Embedded` foi deletado por
   inteiro junto com `JsonDeviceRegistryStore`, `AppEmbeddedDeviceServerSettingsProvider`
   e o enum `DeviceServerMode`. O composition root do `App.WinUI` agora resolve
   apenas `RemoteDeviceServerClient` + `RemoteDeviceFrameTransport`.
2. **Hardcodear Wi-Fi/servidor no firmware.** O portal AP foi retirado em
   favor de um header `firmware/esp32s3-devkitc1/src/mica_config.h` (gitignored,
   copia local de `mica_config.example.h`). O firmware faz `WiFi.begin(SSID,
   PASSWORD)` direto. A dependencia `tzapu/WiFiManager` saiu do `platformio.ini`.
3. **Auto-register sem pair code.** Novo endpoint `POST /api/v1/auto-register`
   no `DeviceServerHost` aceita devices originados em IPs privados (mesma
   politica de `RestrictToPrivateNetworks`). O `deviceId` e computado por
   `SHA256(MAC).first8bytes` (idempotente por MAC), e o token e gerado uma
   unica vez e reutilizado em re-registros.
4. **Compositor compartilhado em projeto cross-platform.** Novo projeto
   `src/Panels.Composition/` (target `net10.0`) carrega `PanelDefinition`,
   `PanelWidgetDefinition`, helpers de desenho 5x7 e o
   `ServerSidePanelCompositor`. O WinUI mantem seu compositor proprio para
   widgets dependentes (audio visualizer, PC metrics) mas tambem upload-a a
   `PanelDefinition` para o servidor a cada `PanelsPlaybackService.StartAsync`.
5. **Widget autonomo no servidor (V1: Clock).** `MicaAudio.Server` ganha
   `FileServerPanelStore` (JSON por device em `{StorageRoot}/panels/`) e
   `PanelCompositorHostedService` (loop 30 FPS, RGBA -> RGB565 ->
   `StreamFrameV2.CreateFrame128x64Rgb565` -> `IDeviceFrameTransport.SendFrame`).
   `PanelServerCapabilityClassifier` decide se o painel e `ServerCapable`,
   `RequiresClient` ou `Empty`. So Clock e server-capable hoje; GIFs/Images
   exigem migrar `Hub75GifDecoder` (Magick.NET) para o container Linux em
   iteracao futura.
6. **Hotfix do board JSON.** `mica_esp32_s3_devkitc1_n16r8.json` passou a usar
   `-DARDUINO_USB_MODE=0` e `-DARDUINO_USB_CDC_ON_BOOT=0` para que os logs do
   firmware aparecam pela UART tradicional. O bin oficial em
   `src/App.WinUI/AppData/Firmware/` foi regerado pelo
   `scripts/build-precompiled-firmware.ps1` ao final da Fase 2.

## Consequencias

- **+** Composition root do WinUI muito menor; nenhum codigo de hosting de
  servidor sobra no cliente desktop.
- **+** Devices novos sao 100% plug-and-play apos flash: sem portal, sem
  digitacao de pair code, sem app Wi-Fi auxiliar.
- **+** O painel de relogio nao desaparece quando o WinUI fecha.
- **-** `mica_config.h` precisa ser criado a mao em cada workspace; perda
  acidental do arquivo quebra o build do firmware. Mitigacao: `mica_config.example.h`
  versionado + `.gitignore` documentado.
- **-** GIFs e imagens nao sao mais autonomos enquanto o
  `PanelCompositorHostedService` nao aprender a decodificar mídia. Esta
  documentado como follow-up.
- **-** O bin oficial agora reflete sempre as credenciais hardcoded de quem
  rodou o build; nao serve mais como artefato compartilhado entre desenvolvedores.
  Aceitavel no fluxo solo com IA.
- **-** Devices anteriores que dependiam do portal AP precisam ser reflashados
  depois da atualizacao para conseguir entrar na nova rede. Aceitavel pelo
  baseline `Funcionando100`.

## Status

Aceita

## Data

2026-05-08

## Referencias

- `docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md`
- `firmware/esp32s3-devkitc1/src/mica_config.example.h`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp` (STA hardcoded + auto-register)
- `src/Device.Server/Hosting/DeviceServerHost.AutoRegister.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelStore.cs`
- `src/MicaAudio.Server/PanelCompositorHostedService.cs`
- `src/MicaAudio.Server/FileServerPanelStore.cs`
- `src/Panels.Composition/ServerSide/ServerSidePanelCompositor.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#TrySyncPanelToServerAsync`
