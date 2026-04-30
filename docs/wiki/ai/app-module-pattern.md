# Padrao Widget Module

## Objetivo

Definir um contrato arquitetural unico para itens do catalogo HUB75 que passam a ser usados como widgets dentro de `Paineis`, reduzindo acoplamento e mantendo consistencia de preview, configuracao e composicao final.

## Estrutura minima por item de catalogo

Todo item novo deve ser modelado como **Widget Module** contendo:

1. **Manifest (`AppCatalogItem`)**
   - `id`, `name`, `packageName`, `category` e metadados de exibicao.
2. **Config schema (`AppModifierDefinition`)**
   - Lista de modificadores, tipos, defaults e regras basicas.
3. **Preview definition**
   - Define `kind` e parametros visuais para os cards da biblioteca.
4. **Renderer HUB75 opcional**
   - Necessario apenas para itens que podem ser adicionados ao canvas em `Paineis`.
   - A disponibilidade deve ser centralizada no modulo de `Paineis`, hoje via `PanelsFrameComposer.SupportsWidgetApp(...)`.

## Contratos por camada

### 1) Contratos/modelos

**Entrada**: JSON de catalogo + drafts locais de modificadores.

**Saida**: objetos validos de catalogo, preview e schema.

**Responsabilidade**:
- Definir o formato sem efeito colateral.
- Permitir validacao estrutural minima.

### 2) UI (`PanelsPage` + controles)

**Entrada**: item de catalogo + schema de modificadores.

**Saida**: instancia de widget com `ConfigValues` proprios e posicao/tamanho no painel.

**Responsabilidade**:
- Exibir biblioteca de widgets com busca, preview e badge de disponibilidade.
- Criar widgets a partir do catalogo.
- Editar configuracao por instancia no inspetor.

### 3) Aplicacao/servicos

**Entrada**: painel salvo + widget selecionado + device alvo.

**Saida**: preview composto, persistencia local/server-first e playback HUB75 ativo.

**Responsabilidade**:
- Reaproveitar drafts `__local__|appId` como defaults de widget.
- Persistir paineis e widgets em store propria e, no modo Remote, salvar biblioteca/estado ativo no server.
- Compor um unico framebuffer `128x64` e transmiti-lo ao device alvo no modo Embedded; no modo Remote, o runtime autonomo do server compoe widgets `server`.

### 4) Integracao com dispositivo

**Entrada**: frame HUB75 final do painel.

**Saida**: envio direcionado por `deviceId` e estado operacional do painel ativo.

**Responsabilidade**:
- Manter um unico painel ativo por vez.
- Nao executar widgets isolados no ESP32; em modo Remote widgets `server` rodam no `MicaAudio.Server`, e em modo Embedded permanecem no WinUI.

## Regras de dependencia

1. A UI depende de contratos do catalogo e do editor compartilhado de modificadores, nao de um fluxo de deploy individual por app.
2. A disponibilidade HUB75 deve ficar em um unico ponto no modulo `Paineis`.
3. `AppModifierStateStore` continua sendo apenas uma fonte de defaults locais, nao a store final do painel.
4. O compositor de `Paineis` e a unica fonte de verdade do frame final enviado ao device.

## Criterios de aceitacao para novos widgets

- [ ] O item aparece na biblioteca de `Paineis`.
- [ ] O card exibe preview estatico coerente com o `preview.kind`.
- [ ] Se houver renderer HUB75, o widget pode ser arrastado para o canvas.
- [ ] Os modificadores aparecem corretamente no inspetor.
- [ ] Um painel salvo preserva `ConfigValues` e `RuntimeState` por widget.

## Referencias de codigo

- [AppCatalogItem](../../../src/App.WinUI/Models/Apps/AppCatalogItem.cs#L1)
- [AppPreviewDefinition](../../../src/App.WinUI/Models/Apps/AppPreviewDefinition.cs#L1)
- [AppModifierDefinition](../../../src/App.WinUI/Models/Apps/AppModifierDefinition.cs#L1)
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L1)
- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)
- [PanelsFrameComposer](../../../src/MicaAudio.PanelRuntime/Services/Panels/PanelsFrameComposer.cs#L1)
