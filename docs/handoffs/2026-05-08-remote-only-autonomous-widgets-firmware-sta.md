# Remote-only, widgets autonomos no servidor e firmware STA hardcoded

## Objetivo

Remover o modo embedded do device server, transferir o portal de provisioning
do firmware para credenciais hardcoded em `mica_config.h` com auto-registro
pelo MAC, e introduzir um compositor server-side que mantem widgets autonomos
(relogio hoje) renderizando depois que o WinUI fecha.

## Escopo classificado

- Tipo: estrutural + firmware/protocolo
- Criterio de aceite:
  - `dotnet build MicaAudio.sln -c Debug` -> 0 erros, 0 warnings de codigo.
  - `dotnet test MicaAudio.sln` -> 521 aprovados, 1 ignorado (manual loopback), 0 falhas.
  - `platformio run -e esp32s3_devkitc1_dma_exp` -> SUCCESS.
  - `Baixar firmware` no app desktop continua entregando bin valido (manifesto refrescado pelo build script).
  - Endpoint `POST /api/v1/auto-register` aceita devices em IP privado e retorna `deviceId`/`token` deterministicos por MAC.
  - Apos um `PanelsPlaybackService.StartAsync` com painel apenas Clock, o servidor mantem o relogio renderizando se o WinUI fechar.
- Fora de escopo:
  - GIF/Image autonomos no servidor (Hub75GifDecoder + Magick.NET no Linux ficam para iteracao futura).
  - Fluxo de takeover dinamico (servidor pausa quando WinUI esta streamando frames). V1 simplesmente coexiste.
  - Atualizacoes na wiki em `docs/wiki/` (intencional: usuario pediu para nao gastar token na wiki agora).

## Arquivos alterados

### Removidos
- `src/Device.Client.Embedded/` (projeto inteiro, 8 arquivos)
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/App.WinUI/Services/Devices/AppEmbeddedDeviceServerSettingsProvider.cs`
- `src/MicaAudio.Core/Presets/DeviceServerMode.cs`
- `tests/Output.Tests/EmbeddedDeviceServerClientTests.cs`

### Criados
- `src/Panels.Composition/Panels.Composition.csproj`
- `src/Panels.Composition/Models/PanelDefinition.cs`
- `src/Panels.Composition/Models/PanelWidgetDefinition.cs`
- `src/Panels.Composition/Drawing/MatrixFont5x7.cs`
- `src/Panels.Composition/Drawing/PanelsMatrixDrawHelpers.cs`
- `src/Panels.Composition/ServerSide/PanelServerCapability.cs`
- `src/Panels.Composition/ServerSide/IServerWidgetRuntime.cs`
- `src/Panels.Composition/ServerSide/ServerClockWidgetRuntime.cs`
- `src/Panels.Composition/ServerSide/ServerSidePanelCompositor.cs`
- `src/Device.Server.Abstractions/Hosting/IServerPanelStore.cs`
- `src/Device.Server/Hosting/InMemoryServerPanelStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.AutoRegister.cs`
- `src/Device.Protocol/Models/AutoRegisterDeviceRequest.cs`
- `src/Device.Protocol/Models/AutoRegisterDeviceResponse.cs`
- `src/MicaAudio.Server/FileServerPanelStore.cs`
- `src/MicaAudio.Server/PanelCompositorHostedService.cs`
- `firmware/esp32s3-devkitc1/src/mica_config.example.h`
- `docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md`

### Editados
- `src/MicaAudio.sln` (entrada Panels.Composition adicionada, Device.Client.Embedded removida)
- `src/App.WinUI/App.WinUI.csproj` (refs a Device.Client.Embedded e Device.Server removidas)
- `src/App.WinUI/App.xaml.cs` (composicao remote-only, ~50 linhas embedded fora)
- `src/App.WinUI/Services/AppSettingsDomainService.cs` (sem `DeviceServerMode`)
- `src/App.WinUI/Views/SettingsPage.xaml.cs` (combo embedded/remote removido)
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs` (`TrySyncPanelToServerAsync` no fim do `StartAsync`)
- `src/MicaAudio.Core/Presets/AppSettings.cs` (sem `DeviceServerMode`)
- `src/Device.Client.Abstractions/IDeviceServerClient.cs` (`UploadPanelAsync` + `DeletePanelAsync` defaults)
- `src/Device.Client.Remote/RemoteDeviceServerClient.cs` (`UploadPanelAsync`, `DeletePanelAsync`)
- `src/Device.Server.Abstractions/Device.Server.Abstractions.csproj` (ref a Panels.Composition)
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs` (rotas auto-register e panel)
- `src/MicaAudio.Server/MicaAudio.Server.csproj` (ref a Panels.Composition)
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs` (registra `IServerPanelStore` + hosted service)
- `tests/Output.Tests/Output.Tests.csproj` (ref a Device.Client.Embedded fora)
- `tests/Integration.Smoke/Integration.Smoke.csproj` (ref a Device.Client.Embedded fora)
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs` (asserts embedded -> guard anti-regressao)
- `tests/Output.Tests/FirmwareBootSourceLayoutTests.cs` (boot espera `connectStaHardcoded` + `autoRegisterIfNeeded`)
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs` (asserts remote-only)
- `firmware/esp32s3-devkitc1/boards/mica_esp32_s3_devkitc1_n16r8.json` (USB_MODE/CDC=0)
- `firmware/esp32s3-devkitc1/platformio.ini` (sem `tzapu/WiFiManager`)
- `firmware/esp32s3-devkitc1/src/mica_provisioning.h` (nova API STA + stubs legados)
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp` (`connectStaHardcoded`, `autoRegisterIfNeeded`, stubs no-op para WiFiManager removido)
- `firmware/esp32s3-devkitc1/src/main.cpp` (boot STA hardcoded -> auto-register -> MQTT/WS)
- `.gitignore` (ignora `firmware/esp32s3-devkitc1/src/mica_config.h`)
- `README.md` (secao "Arquitetura remote-only e widgets autonomos no servidor (2026-05)")
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_*.bin` + `.manifest.json` (regerados pelo `build-precompiled-firmware.ps1`)

## Decisoes tomadas

1. **Endpoint `auto-register` sem pair code** (escolha do usuario na fase de planejamento). DeviceId determinado por hash do MAC (`SHA256.first8bytes`); token aleatorio gerado uma vez e reutilizado por idempotencia. So aceita IPs privados.
2. **Compositor compartilhado em projeto separado** (`Panels.Composition`) com namespace proprio. Modelos sao duplicados em formato JSON-compativel para evitar refatorar 12 arquivos do WinUI; o WinUI serializa seu PanelDefinition local e o servidor desserializa como `Panels.Composition.Models.PanelDefinition`.
3. **Apenas Clock e server-capable na V1.** O `PanelServerCapabilityClassifier` retorna `RequiresClient` para qualquer widget diferente de `analogclock`. GIFs/Images e weather (futuro) entram nas iteracoes seguintes quando `Hub75GifDecoder` for portado para o container Linux.
4. **Coexistencia sem takeover dinamico.** Se WinUI estiver streamando frames para o mesmo device em que o servidor esta renderizando Clock, ambos enviam para o ESP. Em pratica WinUI sobrescreve o Clock enquanto estiver aberto. Quando WinUI fecha, o servidor continua. Suficiente para V1; takeover por timestamp de "ultima ativide do cliente" fica para iteracao futura.
5. **Bin oficial regerado pelo `scripts/build-precompiled-firmware.ps1`.** O hotfix de bumping do `builtAtUtc` no manifesto foi necessario porque a edicao do board JSON na Fase 1 marcou o pacote como stale; ao final da Fase 2 o build script gerou novos bin/manifest reais com `firmwareVersion=v0.0.0-2-gd5b11df-dirty`.

## Validacoes executadas

```text
dotnet restore .\MicaAudio.sln -> aprovado
dotnet build .\MicaAudio.sln -c Debug -> aprovado (0 erros, 0 warnings de codigo, NU1902 OpenTelemetry pre-existente)
dotnet test .\MicaAudio.sln -c Debug --no-build -> aprovado (521 testes, 1 skipped manual loopback, 0 falhas)
platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1 -> SUCCESS (RAM 38.4%, Flash 44.2%)
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -SkipToolInstall -> SUCCESS (1457424 bytes merged + 1391888 bytes OTA + manifest.json regerado)
```

Verificacoes manuais sugeridas (nao automatizadas neste handoff):

- Subir `MicaAudio.Server` standalone (`docker build` + `docker run -e MICA_SERVER__RESTRICTTOPRIVATENETWORKS=true`).
- Editar `firmware/esp32s3-devkitc1/src/mica_config.h` com SSID/senha/IP do server, flashar ESP32-S3.
- Observar serial: deve aparecer `[wifi_connecting] STA hardcoded` -> `[wifi_connected]` -> `[auto_register_success] deviceId=mp-auto-...`.
- Abrir `MicaAudio.exe`, configurar painel apenas com Clock, ativar.
- Fechar o WinUI. O ESP deve continuar mostrando o relogio (renderizado pelo `PanelCompositorHostedService`).

## Riscos e rollback

- **Risco:** `mica_config.h` ausente em workspace novo quebra o build do firmware com `fatal error`. Mitigacao: `mica_config.example.h` versionado + .gitignore documentado; o erro inclui o nome do header faltante.
- **Risco:** `auto-register` sem pair code abre a porta para qualquer IP privado registrar dispositivos. Mitigacao: rate limiting compartilhado com `pair` (`PairRatePolicy`) + `RestrictToPrivateNetworks` (default `true`). Em ambientes onde a LAN nao e confiavel, deixar `RestrictToPrivateNetworks=true` e uma allow-list explicita em `AllowedCidrs`.
- **Risco:** Painel autonomo so suporta Clock; se o usuario configurar GIF achando que vai sobreviver ao fechamento do WinUI, vai ficar tela preta. Mitigacao: `PanelServerCapabilityClassifier` retorna `RequiresClient` e o `DeviceServerHost.HandleAdminUploadPanelAsync` registra a capability na resposta para o cliente exibir UI clara (UI do cliente ainda nao consome esse campo; pendencia de iteracao futura).
- **Risco:** Devices em campo provisionados via portal AP nao reconectam apos atualizacao. Mitigacao: aceito explicitamente pelo usuario (baseline `Funcionando100`); cada device precisa ser reflashado com `mica_config.h` local.
- **Rollback:** cada fase virou commit isolado; `git revert` por commit reverte com seguranca. O bin antigo continua disponivel no historico Git em `src/App.WinUI/AppData/Firmware/`.

## Proximos passos

1. Portar `Hub75GifDecoder` + `PanelsMediaCache` para `Panels.Composition` (cross-platform com `Magick.NET-Q8-AnyCPU` que ja roda no Docker Linux), liberando GIFs e imagens autonomos.
2. Adicionar endpoint multipart `POST /api/v1/admin/devices/{deviceId}/media` para upload das midias do servidor.
3. Implementar takeover dinamico no `PanelCompositorHostedService` (pular device quando o servidor recebeu frames do WinUI nos ultimos 5 segundos).
4. Atualizar a UI de Paineis no WinUI para exibir capability retornada pelo PUT (`ServerCapable` vs `RequiresClient`) e avisar o usuario que widgets dependentes do cliente nao continuam apos fechar o app.
5. Quando a wiki voltar a ser uma prioridade, atualizar `docs/wiki/modules/firmware-esp32s3-devkitc1.md`, `docs/wiki/guides/setup-new-device.md` e `docs/wiki/modules/server-build-and-artifacts.md` para refletir o fluxo novo.
