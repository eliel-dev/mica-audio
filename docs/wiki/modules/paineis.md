# Modulo Paineis

A sessao `Paineis` e uma experiencia `galeria -> editor dedicado` para layouts HUB75 `128x64`, com persistencia local, composicao desktop-streamed e carga direcionada por `deviceId`.

## Galeria De Paineis

- A shell expõe a aba `Paineis` e resolve a pagina de forma lazy, no mesmo padrao das outras sessoes.
- A tela inicial mostra uma grade de cards com miniaturas HUB75 `128x64`, nome do painel e toggle `Ativo`.
- Cada item da galeria e materializado por um card dedicado, evitando depender de `ContainerContentChanging` para montar a UI final do `GridView`.
- O preview de cada card usa tamanho explicito e proporcao `2:1`, para manter o poster HUB75 visivel mesmo em cards estreitos.
- O topo da galeria concentra `titulo + seletor global de device + botao Novo painel`; `Importar` e `Exportar` ficam fora desse fluxo no V1.
- A galeria opera em modo `static first`: abre sem animacao, sem rebuild global de thumbnails e sem criar sessoes completas para todos os cards.
- Cada card usa poster frame cacheado; o card ativo apenas reflete o ultimo frame conhecido do playback, sem animacao local continua na galeria.

## Editor Hub75

- Clicar num card abre um editor dedicado dentro da mesma `PanelsPage`, sem nova pagina top-level.
- O painel permanece fixo em `128x64`; `Width` e `Height` continuam no modelo apenas por compatibilidade e sao normalizados automaticamente nesse tamanho.
- O header do editor traz `Voltar`, `Salvar`, `Duplicar`, `Excluir` e o nome do painel como campo editavel inline.
- O editor central usa um canvas HUB75 ampliado com overlay de selecao, drag e resize por alcas nas bordas e cantos, sem misturar os adornos de edicao no frame real enviado ao device.
- Em desktop e widescreen, o editor usa layout `workbench`: uma faixa compacta de `Widgets` no topo, `Widget`/configuracao em coluna fixa a esquerda e canvas HUB75 dominante na area principal a direita; o layout nao volta mais para tres colunas em tela cheia.
- O editor segue a regra global `canvas-first` do app: header e status ficam fixos, o corpo da pagina vira o dono do scroll vertical e o canvas HUB75 mantem minimo visivel antes de qualquer compressao mais agressiva.
- As panes inferiores continuam com scroll proprio quando necessario, mas apenas como regioes secundarias; elas nao podem mais empurrar o canvas HUB75 para fora do viewport.
- O editor abre com preview local desligado por default; a animacao so liga quando o usuario ativa explicitamente o toggle `Preview`.
- A biblioteca de widgets virou uma rail compacta no topo do editor: continua sendo drag source do catalogo, mas agora usa tiles visuais fixos estilo mini HUB75, com identidade estatica por widget, nome curto e subtitulo de categoria para os widgets disponiveis, como `Relogio` e `Foto / GIF`.
- Itens sem renderer no compositor HUB75 continuam visiveis, mas aparecem desabilitados na biblioteca ate terem suporte.
- A biblioteca lateral e derivada do catalogo atual de apps, permitindo instancias independentes dos itens suportados, hoje `analogclock` e `gifhub75`.
- Arrastar um app da biblioteca para o canvas cria o widget imediatamente no painel atual, sem exigir salvamento para ele aparecer.
- O canvas HUB75 usa preview `clean fit`: preserva a proporcao real `128x64`, reduz o padding interno e remove a moldura preta extra do editor para aproveitar melhor a area disponivel sem distorcer a matriz.
- O inspetor da esquerda ficou restrito ao widget selecionado: remocao, modifiers compartilhados e selecao de fonte local para `gifhub75`.
- Sobreposicao de widgets continua suportada, mas a ordem agora e manual: selecionar no canvas nao sobe mais a camada automaticamente, e o proprio editor HUB75 expoe uma toolbar minima na faixa preta superior do letterbox com icones para `Mover para tras`, `Trazer para frente` e alternar entre widgets realmente sobrepostos.
- `Salvar` reaplica automaticamente o painel no mesmo device quando o painel editado ja estiver ativo.

## Persistencia Do Layout

- O estado local fica em `PanelsStore`, salvo em `%APPDATA%\\MicaAudio\\panels\\panels.json` via `MicaAudioOptions.PanelsFilePath`.
- Cada painel persiste `PanelId`, nome, dimensoes HUB75 e a lista de widgets, incluindo `ConfigValues` e `RuntimeState`.
- `RuntimeState` existe para dados locais que nao cabem no contrato do catalogo; no V1 ele guarda o `sourcePath` do widget `gifhub75`.
- A selecao mais recente da tela fica em `lastSelectedPanelId`, restaurada ao reabrir a sessao.
- `PanelsStore` agora trata `panels.json` ausente, vazio ou corrompido como estado recuperavel: a sessao volta com documento vazio, a shell nao cai e `PanelsPage` recria `Painel 1` no fluxo normal.
- Quando encontra JSON invalido nao-vazio, `PanelsStore` preserva evidencia em `panels.json.corrupt-<timestamp>.json` antes de continuar com documento vazio.
- O save de `PanelsStore` passou a ser atomico com `panels.json.tmp` + replace/move, mantendo `panels.json.bak` simples para reduzir risco de truncamento em crash/interrupcao.

## Editor Compartilhado De Modifiers

- `AppModifierEditorHost` consolidou a criacao dos controles de modifiers, a normalizacao de draft e o autocomplete de cidade do fluxo antigo de catalogo.
- A mesma superficie agora atende o fluxo legado de catalogo e o editor de `PanelsPage`, evitando drift entre schema do catalogo e configuracao por widget.
- Ao criar um widget novo, `PanelsPage` reaproveita o draft local `__local__|appId` como default inicial e depois isola a configuracao em `ConfigValues` proprios do widget.

## Compositor Hub75

- `PanelsFrameComposer` cria um unico framebuffer RGBA `128x64` por apresentacao e compoe os widgets em ordem de `ZIndex`.
- `CreatePosterAsync(...)` separa poster render de playback render para manter a galeria leve e previsivel.
- `analogclock` e renderizado nativamente no compositor com texto `5x7` e barra de segundos.
- `gifhub75` usa decodificacao propria por widget, inclusive para arquivos estaticos e slideshow local por pasta.
- GIF animado agora preserva os delays reais do arquivo por frame; o compositor resolve o frame ativo por timeline da midia, nao mais por indice global fixo.
- `PanelsMediaCache` trata midia animada como sequencia temporal (`frames + durationMs + totalDurationMs`), o que evita redecodificacao e permite playback mais fiel.
- Quando a midia ja entra no tamanho do widget, o compositor faz fast path e reaproveita os pixels sem reescala desnecessaria.
- Posters de `gifhub75` decodificam apenas o primeiro frame util da midia; a animacao completa fica reservada ao playback real ou ao preview manual do editor.
- `PanelsMediaCache` compartilha posters e frames animados entre galeria, editor e playback para evitar redecodificacao redundante.
- Caminhos invalidos nao derrubam o runtime: o widget entra em erro para o preview e contribui preto no frame final.

## Runtime Em Background

- `PanelsPlaybackService` mantem somente um painel ativo por vez, em background, enquanto o app desktop estiver aberto.
- O toggle `Ativo` da galeria usa snapshot salvo do painel; editar depois disso nao muda o device ate novo `Salvar` ou nova ativacao.
- O scheduler padrao do painel e `30 FPS`, ancorado em relogio monotonic para reduzir drift do loop de reproducao.
- O playback real de `gifhub75` usa `30 Hz` como teto de apresentacao, mas respeita os delays reais do GIF; frames repetidos continuam sendo deduplicados antes do envio.
- A fila de saida do device segue politica `newest-wins` (`capacity=1` + `DropOldest`), portanto o runtime prioriza o frame mais novo sob carga em vez de tentar entregar todos.
- A entrada recomendada para `gifhub75` neste v1 e imagem/GIF ja preformatado externamente para `128x64`; o compositor ainda escala quando necessario, mas esse nao e o caminho ideal de qualidade/performance.
- Mesmo com o visualizador principal operando em `Bins128`, `Paineis` continuam usando transporte dedicado `Frame128x64` para o HUB75 fisico.
- Quando o device anuncia `animatedWebpBatchSupported = true`, o host troca o stream frame-a-frame por lotes animados `WebP` de `1 s / 30 frames`:
  - o compositor continua autoritativo e resolve todos os widgets/sobreposicoes no host;
  - o host guarda apenas `ativo + proximo` em memoria por sessao/device;
  - o envio ao device acontece por `queue_panels_batch` + download HTTP autenticado no `Device.Server`;
  - o fallback para `Frame128x64` continua automatico se o device nao suportar batches ou se a fila de lotes falhar.
- O modo WebP batch e `play-once queue` no v1: o firmware toca o lote atual uma vez, troca no boundary para o proximo e, em underrun, segura o ultimo frame valido.
- Quando o `Visualizador HUB75` assume prioridade, o runtime do painel entra em suspensao retomavel:
  - o loop/frame output para;
  - o painel deixa de aparecer como ativo na galeria;
  - o snapshot + `deviceId` ficam retidos apenas para retomada posterior.

## Carga Direcionada Por Device

- `PanelsDeviceSessionService` marca o device alvo como app logico `panels-hub75`, restaura o app anterior ao parar e tenta reativar o painel em reconexao.
- Quando o `Visualizador HUB75` esta ativo, `PanelsDeviceSessionService` entra em supressao por prioridade superior:
  - nao reativa `panels-hub75` em reconnect/refresh;
  - nao executa restore do app anterior durante a preempcao;
  - volta a reconciliar apenas quando o painel e retomado explicitamente.
- `Esp32S3LedOutput` agora aceita `LedOutputConfig.TargetDeviceId` e escolhe entre `SendFrame(deviceId, ...)` e `BroadcastFrame(...)`.
- `DeviceServerHost` mantem o broadcast existente para fluxos antigos e adiciona envio direcionado sem mudar o payload wire.
- O V1 suporta um unico painel ativo e um unico device alvo por vez.
- A aba `Paineis` bloqueia novas ativacoes enquanto o `Visualizador HUB75` estiver dono do HUB75; edicao/salvamento continuam liberados, mas sem disputar `activate-app`.

## Referencias De Codigo

- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsPage UI](../../../src/App.WinUI/Views/PanelsPage.Ui.cs#L1)
- [PanelsPageViewModel](../../../src/App.WinUI/ViewModels/PanelsPageViewModel.cs#L1)
- [Hub75PanelThumbnailControl](../../../src/App.WinUI/Views/Controls/Hub75PanelThumbnailControl.cs#L1)
- [PanelGalleryCardControl](../../../src/App.WinUI/Views/Controls/PanelGalleryCardControl.cs#L1)
- [Hub75PanelEditorControl](../../../src/App.WinUI/Views/Controls/Hub75PanelEditorControl.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)
- [PanelsStore](../../../src/App.WinUI/Services/Panels/PanelsStore.cs#L1)
- [PanelsStoreDocument](../../../src/App.WinUI/Models/Panels/PanelsStoreDocument.cs#L1)
- [PanelDefinition](../../../src/App.WinUI/Models/Panels/PanelDefinition.cs#L1)
- [PanelWidgetDefinition](../../../src/App.WinUI/Models/Panels/PanelWidgetDefinition.cs#L1)
- [PanelsFrameComposer](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)
- [PanelsMediaCache](../../../src/App.WinUI/Services/Panels/PanelsMediaCache.cs#L1)
- [PanelsAnimatedWebpEncoder](../../../src/App.WinUI/Services/Panels/PanelsAnimatedWebpEncoder.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [PanelsDeviceSessionService](../../../src/App.WinUI/Services/Devices/PanelsDeviceSessionService.cs#L1)
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1)
- [ShellPageContentFactory](../../../src/App.WinUI/Views/ShellPageContentFactory.cs#L1)
- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [LedOutputConfig](../../../src/MicaAudio.Core/Led/LedOutputConfig.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [IDeviceServerHost](../../../src/Device.Server/Hosting/IDeviceServerHost.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [PanelsBatchCommandPayload](../../../src/Device.Protocol/Models/PanelsBatchCommandPayload.cs#L1)
- [DeviceServerHost.PanelsBatches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1)
