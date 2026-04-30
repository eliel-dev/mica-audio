# Modulo Paineis

A sessao `Paineis` e uma experiencia `galeria -> editor dedicado` para layouts HUB75 `128x64`, com biblioteca server-first, runtime autonomo no servidor e carga direcionada por `deviceId`.

## Direcao oficial

- `Paineis` passa a ser oficialmente `asset/config sync + runtime server-owned + cache/editor no cliente`.
- O server fica como fonte de verdade de assets, catalogo, manifests, runtime de widgets `server` e metadata de device/ownership.
- O server passa a ser a fonte de verdade dos paineis salvos e das midias enviadas; o arquivo local vira cache/migracao.
- O server tambem guarda o estado ativo por device (`activePanelId`, `activeAppId`, `lastServerOwnedPanelId`) para que widgets server-owned continuem conhecidos apos fechar o WinUI.
- Em modo Remote, o WinUI salva/ativa o painel e o `MicaAudio.Server` compoe widgets `server` e envia batches `WebP` ao ESP. Em modo Embedded, o comportamento local existente permanece porque fechar o WinUI tambem encerra o servidor embutido.

## Baseline atual / transicao

- O transporte batch `WebP` via `Device.Server` continua vivo e documentado como baseline atual.
- O fluxo `queue_panels_batch + download HTTP autenticado` permanece como caminho de compatibilidade enquanto o push local client-owned converge.
- Ownership continua sendo por `device`: um cliente ativo por vez para modos client-driven.

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

- O estado autoritativo fica no server via Admin API de biblioteca:
  - `GET /api/v1/admin/library/panels`
  - `PUT /api/v1/admin/library/panels`
  - `POST /api/v1/admin/library/media`
  - `GET /api/v1/admin/library/media/{mediaId}`
  - `DELETE /api/v1/admin/library/media/{mediaId}`
- O estado local em `%APPDATA%\\MicaAudio\\panels\\panels.json` via `MicaAudioOptions.PanelsFilePath` permanece como cache e fonte de migracao.
- Cada painel persiste `PanelId`, nome, dimensoes HUB75 e a lista de widgets, incluindo `dataSource`, `ConfigValues` e `RuntimeState`.
- `dataSource` declara quem fornece os dados do widget: `server`, `windows-client`, `android-client` ou `device`.
- Widgets `server` sao compostos pelo runtime autonomo do servidor e continuam apos fechar o WinUI enquanto o servidor estiver ligado; widgets `windows-client` e `android-client` sao efemeros e devem expirar quando o cliente dono desconectar.
- `RuntimeState` existe para dados de runtime que nao cabem no contrato do catalogo. `sourcePath` fica apenas no cache local do WinUI; o documento salvo no server aceita somente `mediaId` e `mediaIds`.
- A selecao mais recente da tela fica em `lastSelectedPanelId`, restaurada ao reabrir a sessao.
- O estado ativo por device fica em `activePanels[]`; ao ativar um painel, o WinUI grava `activePanelId=panelId`, `activeAppId=panels-hub75` e atualiza `lastServerOwnedPanelId`.
- Ao parar explicitamente o runtime, `activePanelId` e `activeAppId` sao limpos, mas `lastServerOwnedPanelId` permanece para diagnostico e retomada server-first futura.
- `PanelsStore` agora trata `panels.json` ausente, vazio ou corrompido como estado recuperavel: a sessao volta com documento vazio, a shell nao cai e `PanelsPage` recria `Painel 1` no fluxo normal.
- Quando encontra JSON invalido nao-vazio, `PanelsStore` preserva evidencia em `panels.json.corrupt-<timestamp>.json` antes de continuar com documento vazio.
- O save de `PanelsStore` passou a ser atomico com `panels.json.tmp` + replace/move, mantendo `panels.json.bak` simples para reduzir risco de truncamento em crash/interrupcao.
- Ao carregar, `PanelsStore` tenta primeiro a biblioteca remota/embedded do `IDeviceServerClient`.
- Se o server tiver paineis, o documento do server substitui o cache local.
- Se o server estiver vazio e o cache local tiver paineis, o WinUI migra automaticamente o documento local para o server.
- Se a chamada ao server falhar, o cache local continua permitindo abrir e editar a sessao.
- Midias novas passam pela biblioteca de midia do server para deduplicacao por `SHA-256`; ao salvar/ativar, o WinUI faz upload de arquivo/pasta local e troca a copia remota para `mediaId`/`mediaIds`.

## Editor Compartilhado De Modifiers

- `AppModifierEditorHost` consolidou a criacao dos controles de modifiers, a normalizacao de draft e o autocomplete de cidade do fluxo antigo de catalogo.
- A mesma superficie agora atende o fluxo legado de catalogo e o editor de `PanelsPage`, evitando drift entre schema do catalogo e configuracao por widget.
- Ao criar um widget novo, `PanelsPage` reaproveita o draft local `__local__|appId` como default inicial e depois isola a configuracao em `ConfigValues` proprios do widget.

## Compositor Hub75

- `PanelsFrameComposer` cria um unico framebuffer RGBA `128x64` por apresentacao e compoe os widgets em ordem de `ZIndex`.
- `PanelsFrameComposer`, modelos de painel e encoder `WebP` vivem em `MicaAudio.PanelRuntime` (`net10.0`) para serem consumidos pelo WinUI e pelo `MicaAudio.Server`.
- `CreatePosterAsync(...)` separa poster render de playback render para manter a galeria leve e previsivel.
- `analogclock` e renderizado nativamente no compositor com texto `5x7` e barra de segundos.
- `gifhub75` usa decodificacao propria por widget, inclusive para arquivos estaticos e slideshow por pasta/cache local ou biblioteca de midia do servidor.
- Imagens estaticas agora usam `Magick.NET` cross-platform no compositor compartilhado; GIF, PNG, JPG/JPEG, BMP e WebP sao aceitos no runtime.
- GIF animado agora preserva os delays reais do arquivo por frame; o compositor resolve o frame ativo por timeline da midia, nao mais por indice global fixo.
- O decoder de `gifhub75` coalesce os frames animados antes do formatter/blit, respeitando transparencia e disposal para evitar ghosting no preview e no transporte `WebP`.
- `PanelsMediaCache` trata midia animada como sequencia temporal (`frames + durationMs + totalDurationMs`), o que evita redecodificacao e permite playback mais fiel.
- Quando a midia ja entra no tamanho do widget, o compositor faz fast path e reaproveita os pixels sem reescala desnecessaria.
- Posters de `gifhub75` decodificam apenas o primeiro frame util da midia; a animacao completa fica reservada ao playback real ou ao preview manual do editor.
- `PanelsMediaCache` compartilha posters e frames animados entre galeria, editor e playback para evitar redecodificacao redundante.
- Caminhos invalidos nao derrubam o runtime: o widget entra em erro para o preview e contribui preto no frame final.

## Runtime Em Background

- `ServerOwnedPanelsRuntimeService` roda no `MicaAudio.Server` quando `MICA_SERVER__PANELSAUTORUNTIMEENABLED=true` (default), observa `ActivePanels` e mantem o painel server-owned ativo mesmo apos fechar o WinUI.
- `PanelsPlaybackService` continua existindo para modo Embedded e preview/compatibilidade local; em modo Remote a ativacao de painel persiste estado no servidor e nao inicia compositor continuo no WinUI.
- O toggle `Ativo` da galeria usa snapshot salvo do painel; editar depois disso nao muda o device ate novo `Salvar` ou nova ativacao.
- O scheduler padrao do painel e `30 FPS`, ancorado em relogio monotonic para reduzir drift do loop de reproducao.
- O playback real de `gifhub75` usa `30 Hz` como teto de apresentacao, mas respeita os delays reais do GIF; frames repetidos continuam sendo deduplicados antes do envio.
- A fila de saida do device segue politica `newest-wins` (`capacity=1` + `DropOldest`), portanto o runtime prioriza o frame mais novo sob carga em vez de tentar entregar todos.
- A entrada recomendada para `gifhub75` neste v1 e imagem/GIF ja preformatado externamente para `128x64`; o compositor ainda escala quando necessario, mas esse nao e o caminho ideal de qualidade/performance.
- Mesmo com o visualizador principal operando em `Bins128`, `Paineis` continuam usando transporte dedicado `Frame128x64` para o HUB75 fisico.
- Quando o device anuncia `animatedWebpBatchSupported = true`, o host troca o stream frame-a-frame por lotes animados `WebP` de `1 s / 30 frames`:
  - o compositor continua autoritativo e resolve todos os widgets/sobreposicoes no host;
  - o storage default `InMemoryPanelsBatchStore` guarda os batches em memoria por device/sessao e retem os `4` mais recentes por device;
  - o envio ao device acontece por `queue_panels_batch` + download HTTP autenticado no `Device.Server`;
  - o fallback para `Frame128x64` continua automatico se o device nao suportar batches ou se a fila de lotes falhar.
- `PanelsPlaybackService` consome `Device.Client.IDeviceServerClient` para snapshots/comandos/batches e `Device.Client.IDeviceFrameTransport` apenas para frames; no runtime WinUI esses contratos sao atendidos por `Device.Client.Embedded` + `DeviceServerHost`, preservando o server embutido mas removendo dependencia direta do host completo.
- No modo WinUI Remote, o server compoe widgets `dataSource=server`, registra batches `WebP` no `DeviceServerHost` e envia `queue_panels_batch` ao ESP. Widgets `windows-client`/`android-client` sao ignorados no V1 sem derrubar o painel.
- `GET /api/v1/admin/panels/runtime` expoe diagnostico por device: `deviceId`, `panelId`, `state`, `lastBatchSequence`, `lastError` e `updatedAtUtc`.
- As operacoes sensiveis de client (`CreatePairingCode`, `GetDevices`, `RemoveDevice`, batches) possuem caminho async em `IDeviceServerClient`, permitindo remote HTTP sem bloquear o loop do app.
- O storage dos batches `WebP` foi isolado em `Device.Server.Hosting.IPanelsBatchStore`, preparando troca futura de backend sem alterar o contrato de `PanelsPlaybackService`, comandos ou endpoint de download.
- O caminho oficial de batch deixou de materializar `30` arrays RGBA por segundo antes do encode:
  - `PanelCompositionSession` agora aceita render em buffer fornecido pelo chamador (`RenderFrameInto(...)`);
  - o encode WebP consome um unico framebuffer RGBA reutilizavel no hot path;
  - `PanelsPlaybackService` passou a renderizar e codificar o lote de forma incremental, reduzindo churn de heap e copias no host.
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
- [PanelDefinition](../../../src/MicaAudio.PanelRuntime/Models/Panels/PanelDefinition.cs#L1)
- [PanelWidgetDefinition](../../../src/MicaAudio.PanelRuntime/Models/Panels/PanelWidgetDefinition.cs#L1)
- [PanelsFrameComposer](../../../src/MicaAudio.PanelRuntime/Services/Panels/PanelsFrameComposer.cs#L1)
- [PanelsMediaCache](../../../src/MicaAudio.PanelRuntime/Services/Panels/PanelsMediaCache.cs#L1)
- [PanelsAnimatedWebpEncoder](../../../src/MicaAudio.PanelRuntime/Services/Panels/PanelsAnimatedWebpEncoder.cs#L1)
- [ServerOwnedPanelsRuntimeService](../../../src/MicaAudio.Server/ServerOwnedPanelsRuntimeService.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [PanelsDeviceSessionService](../../../src/App.WinUI/Services/Devices/PanelsDeviceSessionService.cs#L1)
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1)
- [ShellPageContentFactory](../../../src/App.WinUI/Views/ShellPageContentFactory.cs#L1)
- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [LedOutputConfig](../../../src/MicaAudio.Core/Led/LedOutputConfig.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [IDeviceFrameTransport](../../../src/Device.Client.Abstractions/IDeviceFrameTransport.cs#L1)
- [IDeviceServerHost](../../../src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs#L1)
- [IDeviceServerClient](../../../src/Device.Client.Abstractions/IDeviceServerClient.cs#L1)
- [EmbeddedDeviceServerClient](../../../src/Device.Client.Embedded/EmbeddedDeviceServerClient.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [PanelLibraryDocument](../../../src/Device.Protocol/Models/PanelLibraryDocument.cs#L1)
- [PanelDeviceState](../../../src/Device.Protocol/Models/PanelDeviceState.cs#L1)
- [PanelWidgetDataSources](../../../src/Device.Protocol/Models/PanelWidgetDataSources.cs#L1)
- [PanelLibraryItem](../../../src/Device.Protocol/Models/PanelLibraryItem.cs#L1)
- [PanelWidgetItem](../../../src/Device.Protocol/Models/PanelWidgetItem.cs#L1)
- [PanelWidgetRuntimeStateKeys](../../../src/Device.Protocol/Models/PanelWidgetRuntimeStateKeys.cs#L1)
- [PanelRuntimeDiagnosticsResponse](../../../src/Device.Protocol/Models/PanelRuntimeDiagnosticsResponse.cs#L1)
- [MediaAssetInfo](../../../src/Device.Protocol/Models/MediaAssetInfo.cs#L1)
- [IPanelRuntimeDiagnosticsStore](../../../src/Device.Server.Abstractions/Hosting/IPanelRuntimeDiagnosticsStore.cs#L1)
- [InMemoryPanelRuntimeDiagnosticsStore](../../../src/Device.Server/Hosting/InMemoryPanelRuntimeDiagnosticsStore.cs#L1)
- [IPanelLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IPanelLibraryStore.cs#L1)
- [IMediaLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IMediaLibraryStore.cs#L1)
- [PanelsBatchCommandPayload](../../../src/Device.Protocol/Models/PanelsBatchCommandPayload.cs#L1)
- [PanelsBatchRegistration](../../../src/Device.Client.Abstractions/PanelsBatchRegistration.cs#L1)
- [IPanelsBatchStore](../../../src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs#L1)
- [PanelsBatchWrite](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchWrite.cs#L1)
- [PanelsBatchEntry](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchEntry.cs#L1)
- [InMemoryPanelsBatchStore](../../../src/Device.Server/Hosting/InMemoryPanelsBatchStore.cs#L1)
- [DeviceServerHost.PanelsBatches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1)
