# Referencia - Troubleshooting Matrix

| Sintoma | Verificacao | Causa provavel | Acao |
| --- | --- | --- | --- |
| Preview HUB75 nao aparece no Visualizador | conferir se `Modo HUB75` esta ativo e se `HubPreviewPanel` esta visivel | preview local esta oculto pelo toggle | ativar `Modo HUB75`; validar `OnHubCanvasDraw` e `InvalidateHubPreviews()` em `MainPage` |
| Device conecta, mas nao atualiza a matriz | conferir telemetria `panelType` | firmware antigo 64x32 ou protocolo legado | regravar firmware DevKitC-1 128x64 e validar `StreamFrameV2` |
| GIF parece deformado | revisar `GifScaleMode` | modo de escala inadequado para 128x64 | alternar entre `Fit`, `Fill` e `Stretch` |
| Visual parece estreito na loja | revisar renderers de preview | helper ainda usando grade antiga em branch local | validar `Hub75PreviewHelper.PanelWidth=128` e `PanelHeight=64` |
| A lista de dispositivos mostra o nome do tipo em vez do preview | revisar a UI ativa da pagina | `DevicesPage.Ui.cs` montou a lista sem item visual real | validar `DeviceListRowControl` e a aplicacao incremental em `ApplyRenderedItemsDiff()` |
| O preview maior do device fica vazio | verificar se ha selecao e se o device reporta app ativo | nenhum device selecionado ou `ActiveAppId` vazio | selecionar um device; sem app ativo, o placeholder e o comportamento esperado |
| O app ativo nao bate com a miniatura esperada | conferir `ActiveAppId` e o catalogo local de apps | sem match exato no catalogo, a pagina cai no fallback heuristico | validar `DevicePreviewResolver` e o catalogo retornado por `IAppCatalogService` |
| ESP some da lista e volta so com reset | conferir status no backend e o ultimo `LastSeenUtc` | timeout de offline curto ou filtro online-only na UI | validar `DeviceOfflineTimeout=15s` e `DeviceListVisibilityPolicy` |
| ESP aparece como offline, mas continua na lista | conferir `StatusLine` e ordenacao da lista | comportamento esperado novo | manter o item; aguardar reconexao automatica do mesmo `deviceId` |
| Botoes de comando ficam desabilitados em offline | conferir se o item selecionado esta `Offline` | comportamento esperado para evitar comandos invalidos | reconectar o device; os comandos so habilitam com `Status=Online` |

| A lista parece recarregar a cada refresh | observar se itens somem e voltam por inteiro | rebuild total da lista ou rebind desnecessario | validar diff incremental em `DevicesPage` e evitar `ClearRenderedItems()` em refresh normal |
| O preview maior parece reiniciar toda hora | observar se a miniatura grande para e volta | `Stop()/Bind()/Start()` chamados sem mudanca real | validar caches de `currentSelectedPreviewDeviceId` e `currentSelectedPreviewAppId` em `DevicesPage` |
Notas:
- O fluxo ativo usa snapshot nativo `128x64` do simulador.
- `64x32` foi aposentado do caminho principal.
- O perfil `stable` foi removido; se ele aparecer em registro legado, o sistema normaliza para `dma_exp`.
- Na `DevicesPage`, apenas o preview maior do item selecionado anima; as miniaturas da lista ficam estaticas.
- A lista de devices nao e mais online-only; offline continua visivel para preservar o cadastro e reduzir flapping.

## Estado de Devices (2026-03)

- `Offline | Configurado`: o registro local ainda e valido; o device so esta sem telemetria no momento.
- `Offline | Configuracao incerta`: o registro local ainda existe, mas o device esta sem contato ha tempo suficiente para tornar a configuracao no ESP nao confiavel.
- `Registrado | Nunca conectado`: existe registro local, mas nao ha historico confiavel de primeira sessao.
- `Registrado | Aguardando provisionamento`: compatibilidade legada para snapshots com `Status=Pairing`.
- A UI nao usa automaticamente o termo `Nao configurado`; o protocolo atual nao da essa certeza.


| Device offline nao mostra miniatura do app | verificar Status do item selecionado | comportamento esperado | para devices offline, a UI oculta o preview visual e mostra apenas o ultimo app conhecido em texto |
| Painel da direita mostra Dispositivo offline | verificar conectividade do device | comportamento esperado para device offline | o preview grande so aparece para devices online; offline usa placeholder |
| Remover apaga so do app local | confirmar dialogo da acao Remover | comportamento esperado | a remocao exclui o registro local e nao envia comando ao ESP |
| Revogar continua sendo a acao para o ESP online | validar botoes habilitados | diferenca intencional entre acao local e remota | use Revogar apenas quando o device estiver online e voce quiser alterar o dispositivo fisico |
