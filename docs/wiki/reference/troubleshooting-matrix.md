# Referencia - Troubleshooting Matrix

| Sintoma | Verificacao | Causa provavel | Acao |
| --- | --- | --- | --- |
| Preview HUB75 nao aparece no Visualizador | conferir se `Modo HUB75` esta ativo e se `HubPreviewPanel` esta visivel | preview local oculto por toggle | ativar `Modo HUB75`; validar `OnHubCanvasDraw` e `InvalidateHubPreviews()` em `MainPage` |
| Device conecta, mas nao atualiza a matriz | conferir telemetria `panelType` | firmware antigo 64x32 ou protocolo legado | regravar firmware DevKitC-1 128x64 e validar `StreamFrameV2` |
| WS retorna 401 logo apos upgrade | conferir handshake WS no firmware e a flag `AllowLegacyWebSocketQueryToken` | firmware antigo ainda envia token por query com servidor em default seguro (legado OFF) | atualizar firmware para header WS; em incidente, habilitar temporariamente `"AllowLegacyWebSocketQueryToken": true` em `%AppData%\\MicaAudio\\settings.json` |
| GIF parece deformado | revisar `GifScaleMode` | modo de escala inadequado para 128x64 | alternar entre `Fit`, `Fill` e `Stretch` |
| Visual parece estreito na loja | revisar renderers de preview | helper ainda usando grade antiga em branch local | validar `Hub75PreviewHelper.PanelWidth=128` e `PanelHeight=64` |
| A lista de dispositivos mostra texto cru em vez de item rico | revisar a UI ativa da pagina | `DevicesPage.Ui.cs` sem bind para `DeviceListRowControl` | validar `DeviceListRowControl` e `ApplyRenderedItemsDiff()` |
| Miniatura inline nao aparece na lista | verificar se o device esta online e se reporta app ativo | `ActiveAppId` vazio ou sem match no catalogo | validar `DevicePreviewResolver` e o catalogo retornado por `IAppCatalogService` |
| O app ativo nao bate com a miniatura esperada | conferir `ActiveAppId` e catalogo local de apps | sem match exato no catalogo, pagina cai em fallback heuristico | validar `DevicePreviewResolver` e o catalogo retornado por `IAppCatalogService` |
| ESP some da lista e volta so com reset | conferir status no backend e `LastSeenUtc` | timeout de offline curto ou filtro online-only local | validar `DeviceOfflineTimeout=15s` e `DeviceListVisibilityPolicy` |
| ESP aparece como offline, mas continua na lista | conferir `StatusLine` e ordenacao da lista | comportamento esperado novo | manter item; aguardar reconexao automatica do mesmo `deviceId` |
| Botoes de comando ficam desabilitados em offline | conferir se item selecionado esta `Offline` | comportamento esperado para evitar comandos invalidos | reconectar o device; comandos habilitam apenas com `Status=Online` |
| A lista parece recarregar a cada refresh | observar se itens somem e voltam por inteiro | rebuild total da lista ou rebind desnecessario | validar diff incremental em `DevicesPage` e evitar `ClearRenderedItems()` em refresh normal |
| A miniatura da lista pisca em refresh | observar rebind da linha sem mudanca real de app | diff da lista ou bind de preview sendo reexecutado sem necessidade | validar `ApplyRenderedItemsDiff()` e `DeviceListRowControl.Bind(...)` |
| Dashboard ESP mostra somente placeholder | verificar se ha selecao ativa | nenhum device selecionado | selecionar um device; sem selecao, placeholder e esperado |
| Dashboard mostra offline mesmo com dados na tela | conferir `StatusLabel` do dashboard | snapshot selecionado nao esta `Online` | comportamento esperado: offline exibe ultimo snapshot conhecido |
| Linha da lista nao mostra IP/RSSI | conferir `StatusLine` no item selecionado | comportamento esperado da UI atual | consultar IP/RSSI no dashboard do device online |
| RSSI aparece com device offline | validar status do snapshot no formatter/UI | regra de exibicao nao aplicada na camada de apresentacao | garantir `RSSI` somente em `Status=Online` e ocultar em offline |
| Dashboard sem barra de fragmentacao | conferir `free*Bytes` e `largest*BlockBytes` | dados incoerentes, ausentes ou sanitizados no firmware | validar payload de telemetria e limites de `largest*BlockBytes` |
| Tendencia NOC da carga do loop nao se move | verificar se `loopLoadPercent` esta chegando e se `LastTelemetryUtc` avanca | device sem amostra nova ou payload sem loop load | validar telemetria v2 e reconectar device para atualizar historico local |
| Device alterna online/offline em ciclo de ~2s | conferir logs por device e estabilidade do WS | reconexao rapida de socket + detach antigo derrubando sessao ou limite de payload WS no firmware | validar patch de detach por identidade + grace de 500ms no servidor e `WEBSOCKETS_MAX_DATA_SIZE` no firmware |
| Logs nao acompanham o device selecionado | trocar selecao e observar texto do card de logs | UI ainda lendo logs globais | validar `GetDeviceLogs(deviceId)` no `ApplySelectionDetails()` |
| Card de logs fica vazio sem contexto | verificar placeholder sem selecao/sem eventos | placeholder nao aplicado no fluxo atual | validar placeholders de logs na `DevicesPage` |
| Remover em online nao tenta revogar | confirmar status e logs do comando antes da remocao | fluxo consolidado nao foi aplicado no handler | validar `OnRemoveDeviceClicked`: online envia `RevokeAndRestart` e depois remove local |
| Botoes de acao nao aparecem no card de resumo | conferir layout do card do dispositivo selecionado | acoes ainda estao em card separado | manter `Testar LED` e `Remover` no mesmo card de nome/status |

Notas:

- O fluxo ativo usa snapshot nativo `128x64` do simulador.
- `64x32` foi aposentado do caminho principal.
- O perfil `stable` foi removido; se ele aparecer em registro legado, o sistema normaliza para `dma_exp`.
- Na `DevicesPage`, a visualizacao de app usa apenas miniaturas inline na lista.
- A lista de devices nao e mais online-only; offline continua visivel para preservar cadastro e reduzir flapping.

## Estado de Devices (2026-03)

- `Offline | Configurado`: registro local ainda valido; device sem telemetria no momento.
- `Offline | Configuracao incerta`: registro local existe, mas sem contato ha tempo suficiente para reduzir confianca da configuracao no ESP.
- `Registrado | Nunca conectado`: existe registro local, sem historico confiavel de primeira sessao.
- `Registrado | Aguardando provisionamento`: compatibilidade legada para snapshots com `Status=Pairing`.
- A UI nao usa automaticamente o termo `Nao configurado`; o protocolo atual nao da essa certeza.
