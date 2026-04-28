# Handoff - Zero-Code LAN Onboarding + Server-First Panels

## Objetivo

Eliminar o codigo de pareamento do fluxo normal: apos flash e configuracao de Wi-Fi, o ESP32-S3 descobre o `MicaAudio.Server` na LAN, registra/reutiliza o device automaticamente por MAC e aparece no WinUI. Em paralelo, mover paineis e midias para biblioteca server-first em `StorageRoot`, preservando migracao automatica do cache local.

## Escopo classificado

- Tipo: `firmware_protocolo` + `estrutural`.
- Criterio de aceite: firmware nao bloqueia `loopTask` em discovery/HTTP/MQTT/WS, server responde discovery UDP em `5275/udp`, registro LAN respeita `TrustedLanAutoRegistration`, biblioteca de paineis/midia persiste em JSON + blobs, WinUI sincroniza/migra paineis e a UX normal usa `Reprovisionar Wi-Fi` em vez de remover device.
- Fora de escopo: blocklist de devices, cloud/public auto-registration, Postgres/S3, teste fisico automatizado de 5 minutos no ESP real.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/Device.Protocol/Models/*Discovery*`, `PanelLibrary*`, `PanelWidgetItem.cs`, `MediaAssetInfo.cs`, `DeviceRecord.cs`
- `src/Device.Server.Abstractions/Hosting/*LibraryStore.cs`, `DeviceRecordMutations.cs`, `IDeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost*.cs`, `DeviceServerRuntimeConfig.cs`, in-memory library stores
- `src/Device.Client.Abstractions/IDeviceServerClient.cs`
- `src/Device.Client.Embedded/*`
- `src/Device.Client.Remote/RemoteDeviceServerClient.cs`
- `src/MicaAudio.Server/*Options.cs`, `MicaAudioServerBootstrap.cs`, standalone library stores, `Dockerfile`
- `render.yaml`
- `src/App.WinUI/Services/Panels/PanelsStore.cs`
- `src/App.WinUI/Views/DevicesPage*.cs`
- `src/Device.Server/wwwroot/dashboard/*`
- testes em `tests/Output.Tests/*` e `tests/Integration.Smoke/PanelsStoreTests.cs`
- docs em `docs/wiki/modules/*`, `docs/wiki/reference/*` e este handoff

## Decisoes tomadas

1. Auto-registro LAN e opt-in no server standalone por `TrustedLanAutoRegistration`; o embedded WinUI habilita por default para o fluxo local confiavel.
2. Re-registro usa `DeviceMac` para preservar `deviceId/token` e evitar duplicacao depois de reboot/flash.
3. `/api/v1/pair` permanece como compatibilidade/deprecado, mas `StartupPairCodeTtlSeconds` fica `0` por default e a UX normal nao pede codigo.
4. O firmware aceita Wi-Fi sem servidor manual; `Servidor` no portal AP fica apenas como fallback tecnico.
5. Discovery UDP usa backoff cooperativo e timeouts explicitos em HTTP/MQTT/WS para manter o TWDT ativo sem reset normal de boot.
6. Biblioteca server-first usa `PanelLibraryDocument` e `MediaAssetInfo`; standalone persiste em `panels/panels.json` e `media/*`, embedded usa memoria.
7. `PanelsStore` migra cache local para o server somente quando a biblioteca remota/embedded esta vazia.
8. `RemoveDevice` deixa de ser acao principal de usuario; `Reprovisionar Wi-Fi` envia `enter_provisioning`.

## Validacoes executadas

```text
dotnet test MicaAudio.Dev.slnf -c Debug -> baseline antes das mudancas aprovado
dotnet test tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore -> aprovado apos novos contratos/server tests
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore --filter PanelsStoreTests -> aprovado
dotnet build src\App.WinUI\App.WinUI.csproj -c Debug --no-restore -> aprovado
python -m platformio run -d firmware\esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet test MicaAudio.Dev.slnf -c Debug -> aprovado
dotnet build MicaAudio.sln -c Debug -> aprovado
```

## Riscos e rollback

- Risco: UDP discovery pode ser bloqueado por firewall, Docker NAT ou rede guest. Mitigacao: publicar `-p 5275:5275/udp`, liberar firewall e usar `Servidor` manual como fallback tecnico.
- Risco: auto-registro LAN em rede nao confiavel permite que device local reapareca sozinho. Mitigacao: `MICA_SERVER__TRUSTEDLANAUTOREGISTRATION=false`.
- Risco: biblioteca server-first ainda nao move todos os caminhos locais de midia do editor. Mitigacao: cache local continua como fallback; completar upload/seletores de midia em fase posterior.
- Rollback operacional: desabilitar `TrustedLanAutoRegistration`, reativar pair code legado com `StartupPairCodeTtlSeconds > 0` e usar `/api/v1/pair`.
- Rollback tecnico: restaurar `PanelsStore` para local-only e remover stores/API de biblioteca sem tocar no transporte de frames existente.

## Proximos passos

1. Executar teste fisico de 5 minutos em flash limpo com server online/offline para confirmar ausencia de reset por task watchdog.
2. Validar Docker local com `-p 5275:5275/udp` em uma LAN real e firewall do Windows habilitado.
3. Completar UX de upload/selecionador de midia para usar a biblioteca `MediaAssetInfo` em vez de caminhos locais no `RuntimeState`.
