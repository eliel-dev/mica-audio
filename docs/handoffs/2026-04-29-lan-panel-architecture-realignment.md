# Handoff - Painel LAN Sempre Ligado + Visualizador Direto

## Objetivo

Realinhar o projeto para servidor LAN sempre ligado como fonte de verdade, com ESP32-S3 como runtime visual e WinUI como controlador/fonte de dados. Nesta entrega, o corte pratico foi consolidar o estado server-first dos paineis e expor diagnostico do caminho remoto direto do visualizador.

## Escopo classificado

- `estrutural`
- `firmware_protocolo`
- `funcional`

## Arquivos alterados

- `src/Device.Protocol/Models/PanelLibraryDocument.cs`
- `src/Device.Protocol/Models/PanelDeviceState.cs`
- `src/Device.Protocol/Models/PanelWidgetItem.cs`
- `src/Device.Protocol/Models/PanelWidgetDataSources.cs`
- `src/Device.Server/Hosting/InMemoryPanelLibraryStore.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Models/Panels/PanelsStoreDocument.cs`
- `src/App.WinUI/Models/Panels/PanelWidgetDefinition.cs`
- `src/App.WinUI/Services/Panels/PanelsStore.cs`
- `src/App.WinUI/Services/Devices/RemoteDeviceTransportDiagnosticsFormatter.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `tests/Output.Tests/DeviceServerHostLibraryApiTests.cs`
- `tests/Output.Tests/StandaloneLibraryStoreTests.cs`
- `tests/Output.Tests/RemoteDeviceServerClientTests.cs`
- `tests/Output.Tests/RemoteDeviceTransportDiagnosticsFormatterTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Integration.Smoke/PanelsStoreTests.cs`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `PanelLibraryDocument.ActivePanels` passa a ser o contrato server-first para estado ativo por device, com `activePanelId`, `activeAppId`, `lastServerOwnedPanelId` e `updatedAtUtc`.
- `PanelWidgetItem.DataSource` formaliza a origem do dado do widget: `server`, `windows-client`, `android-client` ou `device`.
- O WinUI salva o estado ativo no servidor ao ativar um painel HUB75 e preserva `lastServerOwnedPanelId` ao parar explicitamente o runtime.
- O compositor de `Paineis` ainda roda no WinUI nesta etapa; a persistencia server-first prepara a retomada futura de widgets server-owned sem exigir Android agora.
- O visualizador remoto continua no caminho oficial `Bins128` direto WinUI -> ESP via UDP LAN; a UI agora mostra contadores tecnicos do transporte remoto para depurar firewall, endpoint ausente e fallback WS.
- OTA nao ganhou endpoint novo nesta entrega porque o fluxo existente `update_firmware` + progresso tracked ja cobre o primeiro corte; a mudanca foi documentar essa direcao como fluxo principal apos o primeiro flash manual.

## Validacoes executadas

- `dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DeviceServerHostLibraryApiTests|FullyQualifiedName~StandaloneLibraryStoreTests|FullyQualifiedName~RemoteDeviceTransportDiagnosticsFormatterTests|FullyQualifiedName~RemoteDeviceServerClientTests.RemoteDeviceServerClient_ShouldRoundTripPanelLibraryAndMedia"` - passou, 7 testes.
- `dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore --filter "FullyQualifiedName~PanelsStoreTests"` - passou, 7 testes.
- `git diff --check` - passou; avisos apenas de normalizacao LF/CRLF.
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` - passou.
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` - passou.
- `dotnet build .\MicaAudio.sln -c Debug` - passou com avisos NU1902 ja existentes em OpenTelemetry.
- `python -m platformio run -d firmware\esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp` - passou; RAM 39.2%, Flash 49.7%.

## Riscos e rollback

- `ActivePanels` e `DataSource` sao campos aditivos no JSON; clientes antigos devem ignorar os campos, mas nao vao persistir essas informacoes se sobrescreverem a biblioteca.
- O estado ativo server-first ainda nao faz o firmware renderizar paineis sozinho apos o WinUI fechar; ele apenas torna o estado duravel e observavel para a proxima etapa.
- Se o diagnostico remoto poluir a UI, o rollback e remover o `TextBlock` de `SettingsPage`; os contadores permanecem no transporte.

## Proximos passos

- Fazer o firmware/app de painel consumir o ultimo estado server-owned e manter widgets `server` ativos sem WinUI aberto.
- Expirar explicitamente widgets `windows-client`/`android-client` quando o cliente dono desconectar.
- Evoluir concorrencia para owner ativo por `device/app` com lease, mantendo `last-writer-wins` apenas como v1 simples.
- Testar fisicamente WinUI Remote + Docker default + HUB75 e conferir `UDP direto enviados > 0` em Configuracoes.

## Referencias

- ESP-IDF v5.5.4 ESP32-S3: https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/index.html
- ESP-IDF v5.5.4 index source: https://github.com/espressif/esp-idf/blob/v5.5.4/docs/en/index.rst
- System.Text.Json em .NET: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview
