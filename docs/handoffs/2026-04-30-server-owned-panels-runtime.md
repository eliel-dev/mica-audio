# Handoff - Runtime autonomo de paineis server-owned

## Objetivo

Mover a execucao continua de paineis server-owned para o `MicaAudio.Server`, mantendo o WinUI como editor/controlador e preservando o comportamento local no modo Embedded.

## Escopo classificado

- Tipo: estrutural + firmware/protocolo + funcional
- Criterio de aceite:
  - `MicaAudio.PanelRuntime` concentra o compositor compartilhado `net10.0`.
  - Em modo Remote, o WinUI salva/ativa o painel no servidor e nao inicia compositor continuo local.
  - `MicaAudio.Server` executa um `HostedService` que le `ActivePanels`, compoe widgets `dataSource=server` e envia batches `WebP` ao ESP.
  - Midia local de GIF/imagem e migrada para a biblioteca do servidor como `mediaId`/`mediaIds`; `sourcePath` nao e persistido no documento remoto.
  - `GET /api/v1/admin/panels/runtime` expoe diagnostico tecnico por device.

## Arquivos alterados

- `src/MicaAudio.PanelRuntime/MicaAudio.PanelRuntime.csproj`
- `src/MicaAudio.PanelRuntime/Models/Panels/PanelDefinition.cs`
- `src/MicaAudio.PanelRuntime/Models/Panels/PanelWidgetDefinition.cs`
- `src/MicaAudio.PanelRuntime/Services/Gif/Hub75GifDecoder.cs`
- `src/MicaAudio.PanelRuntime/Services/Panels/PanelsFrameComposer.cs`
- `src/MicaAudio.PanelRuntime/Services/Panels/PanelMediaSource.cs`
- `src/MicaAudio.PanelRuntime/Services/Panels/PanelsAnimatedWebpEncoder.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Services/Panels/PanelsStore.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `src/Device.Protocol/Models/PanelWidgetItem.cs`
- `src/Device.Protocol/Models/PanelWidgetRuntimeStateKeys.cs`
- `src/Device.Protocol/Models/PanelRuntimeDiagnosticsResponse.cs`
- `src/Device.Server.Abstractions/Hosting/IPanelRuntimeDiagnosticsStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Admin.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/Hosting/InMemoryPanelLibraryStore.cs`
- `src/Device.Server/Hosting/InMemoryPanelRuntimeDiagnosticsStore.cs`
- `src/MicaAudio.Server/MicaAudio.Server.csproj`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs`
- `src/MicaAudio.Server/MicaAudioServerOptions.cs`
- `src/MicaAudio.Server/StandalonePanelLibraryStore.cs`
- `src/MicaAudio.Server/ServerOwnedPanelsRuntimeService.cs`
- `scripts/docker-server-redeploy.ps1`
- `tests/Output.Tests/DeviceServerHostLibraryApiTests.cs`
- `tests/Output.Tests/MicaAudioServerStandaloneTests.cs`
- `tests/Output.Tests/StandaloneLibraryStoreTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/architecture/01-system-overview.md`
- `docs/wiki/architecture/02-runtime-lifecycle.md`

## Decisoes tomadas

1. O compositor foi extraido para `MicaAudio.PanelRuntime` com namespace preservado para reduzir refactor em cascata.
2. O decode estatico saiu de `System.Drawing` e passou para `Magick.NET-Q8-AnyCPU`, mantendo compatibilidade cross-platform para Docker/Raspberry Pi.
3. O runtime server-owned usa apenas widgets `dataSource=server` no V1; widgets `windows-client` e `android-client` sao ignorados sem derrubar o painel.
4. O servidor gera batches `WebP` de 1 segundo e manda `queue_panels_batch`, reaproveitando o transporte que o firmware ja entende.
5. `sourcePath` fica local no WinUI; o documento server-first so recebe `RuntimeState.mediaId` ou `RuntimeState.mediaIds`.
6. O modo Embedded ficou com o fluxo antigo porque encerrar o WinUI tambem encerra o server embutido.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --filter "AdminLibraryPanels_ShouldRoundTripDocument|AdminPanelsRuntime_ShouldReturnRuntimeDiagnostics|Options_ShouldEnableServerOwnedPanelsRuntimeByDefault|Options_ShouldMapPrefixedEnvironmentStyleValuesToServerConfig" -> aprovado, 4 testes
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --filter "DeviceServerHostLibraryApiTests|StandaloneLibraryStoreTests|MicaAudioServerStandaloneTests" -> aprovado, 18 testes
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet build .\MicaAudio.sln -c Debug -> aprovado, 0 avisos/0 erros
python -m platformio run -d firmware\esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> aprovado; PlatformIO ainda emite aviso de Long Path Support desabilitado no Windows
git diff --check -> aprovado; avisos apenas de normalizacao LF/CRLF
dotnet test .\MicaAudio.Dev.slnf -c Debug --no-restore -> aprovado, 403 testes
```

## Riscos e rollback

- Risco principal: o runtime server-owned V1 depende de firmware com suporte a batches `WebP`; device sem `AnimatedWebpBatchSupported` fica em estado `unsupported` no diagnostico.
- Risco operacional: midias grandes ou GIFs muito pesados podem aumentar CPU no server; o loop registra erro por device e retenta no proximo tick.
- Como reverter: desabilitar `MICA_SERVER__PANELSAUTORUNTIMEENABLED=false` para voltar o modo Remote a apenas persistir estado sem composicao server-side.

## Proximos passos

1. Testar fisicamente Docker + WinUI Remote + ESP, fechar o WinUI e confirmar HUB75 mantendo relogio/GIF/imagem.
2. Evoluir expiracao de widgets `windows-client`/`android-client` com lease quando o modelo de concorrencia for implementado.
3. Avaliar fallback tecnico para firmware antigo sem batches `WebP`, se ainda houver device legado em uso.
