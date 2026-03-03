# Referencia - Troubleshooting Matrix

| Sintoma | Verificacao | Causa provavel | Acao |
| --- | --- | --- | --- |
| Preview HUB75 nao aparece no Visualizador | conferir se `Modo HUB75` esta ativo e se `HubPreviewPanel` esta visivel | preview local oculto por toggle | ativar `Modo HUB75`; validar `OnHubCanvasDraw` e `InvalidateHubPreviews()` em `MainPage` |
| Device conecta, mas nao atualiza a matriz | conferir telemetria `panelType` | firmware antigo 64x32 ou protocolo legado | regravar firmware DevKitC-1 128x64 e validar `StreamFrameV2` |
| GIF parece deformado | revisar `GifScaleMode` | modo de escala inadequado para 128x64 | alternar entre `Fit`, `Fill` e `Stretch` |
| Visual parece estreito na loja | revisar renderers de preview | helper ainda usando grade antiga em branch local | validar `Hub75PreviewHelper.PanelWidth=128` e `PanelHeight=64` |
| A lista de dispositivos mostra texto cru em vez de item rico | revisar a UI ativa da pagina | `DevicesPage.Ui.cs` sem bind para `DeviceListRowControl` | validar `DeviceListRowControl` e `ApplyRenderedItemsDiff()` |
| O preview maior do device fica vazio | verificar se ha selecao e se o device reporta app ativo | nenhum device selecionado ou `ActiveAppId` vazio | selecionar um device; sem app ativo, placeholder e esperado |
| O app ativo nao bate com a miniatura esperada | conferir `ActiveAppId` e catalogo local de apps | sem match exato no catalogo, pagina cai em fallback heuristico | validar `DevicePreviewResolver` e o catalogo retornado por `IAppCatalogService` |
| ESP some da lista e volta so com reset | conferir status no backend e `LastSeenUtc` | timeout de offline curto ou filtro online-only local | validar `DeviceOfflineTimeout=15s` e `DeviceListVisibilityPolicy` |
| ESP aparece como offline, mas continua na lista | conferir `StatusLine` e ordenacao da lista | comportamento esperado novo | manter item; aguardar reconexao automatica do mesmo `deviceId` |
| Botoes de comando ficam desabilitados em offline | conferir se item selecionado esta `Offline` | comportamento esperado para evitar comandos invalidos | reconectar o device; comandos habilitam apenas com `Status=Online` |
| A lista parece recarregar a cada refresh | observar se itens somem e voltam por inteiro | rebuild total da lista ou rebind desnecessario | validar diff incremental em `DevicesPage` e evitar `ClearRenderedItems()` em refresh normal |
| O preview maior parece reiniciar toda hora | observar se miniatura grande para e volta | `Stop()/Bind()/Start()` chamados sem mudanca real | validar caches `currentSelectedPreviewDeviceId` e `currentSelectedPreviewAppId` |
| Dashboard ESP mostra somente placeholder | verificar se ha selecao ativa | nenhum device selecionado | selecionar um device; sem selecao, placeholder e esperado |
| Dashboard mostra offline mesmo com dados na tela | conferir `StatusLabel` do dashboard | snapshot selecionado nao esta `Online` | comportamento esperado: offline exibe ultimo snapshot conhecido |
| Dashboard sem barra de fragmentacao | conferir `free*Bytes` e `largest*BlockBytes` | dados incoerentes, ausentes ou sanitizados no firmware | validar payload de telemetria e limites de `largest*BlockBytes` |
| Logs nao acompanham o device selecionado | trocar selecao e observar texto do card de logs | UI ainda lendo logs globais | validar `GetDeviceLogs(deviceId)` no `ApplySelectionDetails()` |
| Card de logs fica vazio sem contexto | verificar placeholder sem selecao/sem eventos | placeholder nao aplicado no fluxo atual | validar placeholders de logs na `DevicesPage` |
| Remover apaga so do app local | confirmar dialogo da acao Remover | comportamento esperado | remocao exclui registro local e nao envia comando ao ESP |
| Revogar continua sendo a acao para o ESP online | validar botoes habilitados | diferenca intencional entre acao local e remota | usar Revogar quando o device estiver online |

Notas:

- O fluxo ativo usa snapshot nativo `128x64` do simulador.
- `64x32` foi aposentado do caminho principal.
- O perfil `stable` foi removido; se ele aparecer em registro legado, o sistema normaliza para `dma_exp`.
- Na `DevicesPage`, apenas o preview maior do item selecionado anima; miniaturas da lista ficam estaticas.
- A lista de devices nao e mais online-only; offline continua visivel para preservar cadastro e reduzir flapping.

## Estado de Devices (2026-03)

- `Offline | Configurado`: registro local ainda valido; device sem telemetria no momento.
- `Offline | Configuracao incerta`: registro local existe, mas sem contato ha tempo suficiente para reduzir confianca da configuracao no ESP.
- `Registrado | Nunca conectado`: existe registro local, sem historico confiavel de primeira sessao.
- `Registrado | Aguardando provisionamento`: compatibilidade legada para snapshots com `Status=Pairing`.
- A UI nao usa automaticamente o termo `Nao configurado`; o protocolo atual nao da essa certeza.
