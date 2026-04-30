# Handoff - Sessao Paineis HUB75

## Objetivo

Adicionar uma sessao `Paineis` com galeria de cards HUB75, editor dedicado, persistencia local de layouts, composicao desktop-streamed e carga direcionada por device para ESP32.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - nova aba top-level `Paineis` na shell;
  - galeria inicial com miniaturas HUB75 `128x64` e toggle `Ativo` por card;
  - editor dedicado com drag/reposition e widgets do catalogo atual;
  - carga de um painel para um unico `deviceId` a partir da galeria;
  - cobertura de testes para persistencia, compositor, sessao por device e transporte direcionado.

## Arquivos alterados

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/ViewModels/PanelsPageViewModel.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `src/App.WinUI/Views/PanelsPage.Ui.cs`
- `src/App.WinUI/Views/Controls/Hub75PanelThumbnailControl.cs`
- `src/App.WinUI/Views/Controls/Hub75PanelEditorControl.cs`
- `src/App.WinUI/Views/Controls/AppModifierEditorHost.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.Modifiers.cs`
- `src/App.WinUI/Views/AppsPage.RuntimePreview.cs`
- `src/App.WinUI/Views/AppsPage.Catalog.cs`
- `src/App.WinUI/Views/ShellPage.xaml`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/ShellPageContentFactory.cs`
- `src/App.WinUI/Models/Panels/PanelDefinition.cs`
- `src/App.WinUI/Models/Panels/PanelWidgetDefinition.cs`
- `src/App.WinUI/Models/Panels/PanelsStoreDocument.cs`
- `src/App.WinUI/Services/Panels/PanelsStore.cs`
- `src/App.WinUI/Services/Panels/PanelsFrameComposer.cs`
- `src/App.WinUI/Services/Panels/PanelsMatrixDrawHelpers.cs`
- `src/App.WinUI/Services/Panels/MatrixFont5x7.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `src/App.WinUI/Services/Devices/PanelsDeviceSessionService.cs`
- `src/MicaAudio.Core/Config/MicaAudioOptions.cs`
- `src/MicaAudio.Core/Led/LedOutputConfig.cs`
- `src/Device.Server/Hosting/IDeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Output/Led/Esp32S3LedOutput.cs`
- `tests/Output.Tests/Esp32S3LedOutputTests.cs`
- `tests/Output.Tests/DeviceServerHostTargetedFrameTests.cs`
- `tests/Integration.Smoke/PanelsStoreTests.cs`
- `tests/Integration.Smoke/PanelsFrameComposerTests.cs`
- `tests/Integration.Smoke/PanelsDeviceSessionServiceTests.cs`
- `tests/Integration.Smoke/PanelsPageSmokeTests.cs`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. O V1 foi mantido como `desktop-streamed`, com um unico compositor no PC gerando o framebuffer final `128x64`. Isso evita ampliar firmware/protocolo para persistencia ou execucao autonoma no ESP32.
2. O envio para HUB75 passou a suportar destino opcional por `deviceId` em `IDeviceServerHost` e `Esp32S3LedOutput`, preservando o caminho broadcast existente para os fluxos antigos.
3. A edicao de modifiers foi extraida para `AppModifierEditorHost`, reduzindo duplicacao entre `AppsPage` e `PanelsPage` e mantendo a normalizacao de config em um ponto so.
4. O runtime de painel foi isolado em `PanelsPlaybackService` e `PanelsDeviceSessionService`, separando composicao visual, transporte e restauracao do app anterior do device.
5. A UX final foi consolidada como `galeria -> editor dedicado`: ativacao centralizada na galeria, cards com poster frame estatico, animacao apenas para o painel ativo e autosave ao voltar do editor.
6. O code-behind da `PanelsPage` foi ajustado para permanecer no thread da UI, removendo `DispatcherQueue.EnqueueAsync(async ...)` e `ConfigureAwait(...)` improprios no caminho WinUI.
7. O refinamento do editor fixou o painel em `128x64`, moveu a edicao de nome para o header, removeu os `NumberBox` de bounds do inspetor e trocou a manipulacao do layout por drag/drop robusto e resize por alcas diretamente no canvas.
8. A sessao `Apps` deixou de ser exposta na shell; o catalogo passou a alimentar diretamente a biblioteca de widgets de `Paineis`, com drafts legados `__local__|appId` reutilizados como defaults na criacao de widgets.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug /m:1 -> sucesso
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-build -> 228 aprovados
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug -> 91 aprovados, 1 ignorado (manual validation)
```

## Riscos e rollback

- Risco principal: o compositor do V1 suporta apenas `analogclock` e `gifhub75`; novos apps do catalogo ainda exigem renderer dedicado para aparecer no painel.
- Como reverter:
  - remover a aba `Paineis` da shell e o registro no DI;
  - retirar `PanelsPlaybackService` e `PanelsDeviceSessionService`;
  - voltar `Esp32S3LedOutput` ao caminho exclusivo de broadcast;
  - apagar `panels.json` local se necessario.

## Proximos passos

1. Adicionar resize por alca, snap visual de grid e selecao multipla no editor.
2. Expandir o compositor para outros apps do catalogo alem de `analogclock` e `gifhub75`.
3. Avaliar export/import de paineis e presets de layout para compartilhamento entre maquinas.
4. Considerar filtros, ordenacao e estados de device no topo da galeria quando o catalogo de widgets crescer.
