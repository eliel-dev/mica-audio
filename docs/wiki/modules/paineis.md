# Modulo Paineis

A sessao `Paineis` e uma experiencia `galeria -> editor dedicado` para layouts HUB75 `128x64`, com persistencia local, composicao desktop-streamed e carga direcionada por `deviceId`.

## Galeria De Paineis

- A shell expõe a aba `Paineis` e resolve a pagina de forma lazy, no mesmo padrao das outras sessoes.
- A tela inicial mostra uma grade de cards com miniaturas HUB75 `128x64`, nome do painel e toggle `Ativo`.
- O topo da galeria concentra `titulo + seletor global de device + botao Novo painel`; `Importar` e `Exportar` ficam fora desse fluxo no V1.
- Somente o card ativo anima com os frames reais do `PanelsPlaybackService`; os demais usam poster frame estatico gerado pelo compositor.

## Editor Hub75

- Clicar num card abre um editor dedicado dentro da mesma `PanelsPage`, sem nova pagina top-level.
- O painel permanece fixo em `128x64`; `Width` e `Height` continuam no modelo apenas por compatibilidade e sao normalizados automaticamente nesse tamanho.
- O header do editor traz `Voltar`, `Salvar`, `Duplicar`, `Excluir` e o nome do painel como campo editavel inline.
- O editor central usa um canvas HUB75 ampliado com overlay de selecao, drag e resize por alcas nas bordas e cantos, sem misturar os adornos de edicao no frame real enviado ao device.
- A biblioteca lateral e derivada do catalogo atual de apps, permitindo instancias independentes de `analogclock` e `gifhub75`.
- Arrastar um app da biblioteca para o canvas cria o widget imediatamente no painel atual, sem exigir salvamento para ele aparecer.
- O inspetor da direita ficou restrito ao widget selecionado: remocao, modifiers compartilhados e selecao de fonte local para `gifhub75`.
- `Salvar` reaplica automaticamente o painel no mesmo device quando o painel editado ja estiver ativo.

## Persistencia Do Layout

- O estado local fica em `PanelsStore`, salvo em `%APPDATA%\\MicaAudio\\panels\\panels.json` via `MicaAudioOptions.PanelsFilePath`.
- Cada painel persiste `PanelId`, nome, dimensoes HUB75 e a lista de widgets, incluindo `ConfigValues` e `RuntimeState`.
- `RuntimeState` existe para dados locais que nao cabem no contrato do catalogo; no V1 ele guarda o `sourcePath` do widget `gifhub75`.
- A selecao mais recente da tela fica em `lastSelectedPanelId`, restaurada ao reabrir a sessao.

## Editor Compartilhado De Modifiers

- `AppModifierEditorHost` extraiu do code-behind da `AppsPage` a criacao dos controles de modifiers, normalizacao de draft e autocomplete de cidade.
- A mesma superficie agora atende `AppsPage` e `PanelsPage`, evitando drift entre configuracao do catalogo e configuracao por widget.
- O estado salvo por widget e isolado do draft local `__local__` da aba `Apps`.

## Compositor Hub75

- `PanelsFrameComposer` cria um unico framebuffer RGBA `128x64` por tick e compoe os widgets em ordem de `ZIndex`.
- `analogclock` e renderizado nativamente no compositor com texto `5x7` e barra de segundos.
- `gifhub75` usa decodificacao propria por widget, inclusive para arquivos estaticos e slideshow local por pasta.
- Caminhos invalidos nao derrubam o runtime: o widget entra em erro para o preview e contribui preto no frame final.

## Runtime Em Background

- `PanelsPlaybackService` mantem somente um painel ativo por vez, em background, enquanto o app desktop estiver aberto.
- O toggle `Ativo` da galeria usa snapshot salvo do painel; editar depois disso nao muda o device ate novo `Salvar` ou nova ativacao.
- O tick padrao do painel e `12 FPS`, alinhado ao runtime GIF atual.
- O preview local e o envio para o ESP32 saem da mesma composicao.

## Carga Direcionada Por Device

- `PanelsDeviceSessionService` marca o device alvo como app logico `panels-hub75`, restaura o app anterior ao parar e tenta reativar o painel em reconexao.
- `Esp32S3LedOutput` agora aceita `LedOutputConfig.TargetDeviceId` e escolhe entre `SendFrame(deviceId, ...)` e `BroadcastFrame(...)`.
- `DeviceServerHost` mantem o broadcast existente para fluxos antigos e adiciona envio direcionado sem mudar o payload wire.
- O V1 suporta um unico painel ativo e um unico device alvo por vez.

## Referencias De Codigo

- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsPage UI](../../../src/App.WinUI/Views/PanelsPage.Ui.cs#L1)
- [PanelsPageViewModel](../../../src/App.WinUI/ViewModels/PanelsPageViewModel.cs#L1)
- [Hub75PanelThumbnailControl](../../../src/App.WinUI/Views/Controls/Hub75PanelThumbnailControl.cs#L1)
- [Hub75PanelEditorControl](../../../src/App.WinUI/Views/Controls/Hub75PanelEditorControl.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)
- [PanelsStore](../../../src/App.WinUI/Services/Panels/PanelsStore.cs#L1)
- [PanelsStoreDocument](../../../src/App.WinUI/Models/Panels/PanelsStoreDocument.cs#L1)
- [PanelDefinition](../../../src/App.WinUI/Models/Panels/PanelDefinition.cs#L1)
- [PanelWidgetDefinition](../../../src/App.WinUI/Models/Panels/PanelWidgetDefinition.cs#L1)
- [PanelsFrameComposer](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [PanelsDeviceSessionService](../../../src/App.WinUI/Services/Devices/PanelsDeviceSessionService.cs#L1)
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1)
- [ShellPageContentFactory](../../../src/App.WinUI/Views/ShellPageContentFactory.cs#L1)
- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [LedOutputConfig](../../../src/MicaAudio.Core/Led/LedOutputConfig.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [IDeviceServerHost](../../../src/Device.Server/Hosting/IDeviceServerHost.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
