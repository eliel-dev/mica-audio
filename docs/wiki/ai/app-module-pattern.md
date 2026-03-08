# Padrao App Module

## Objetivo

Definir um contrato arquitetural unico para novos apps de catalogo, reduzindo acoplamento e garantindo consistencia de validacao, preview, persistencia e deploy.

## Estrutura minima por app

Todo app novo deve ser modelado como **App Module** contendo:

1. **Manifest (`AppModuleDefinition`)**
   - `id`, `name`, `packageName`, `category` e metadados de exibicao.
   - Baseado no item de catalogo (`AppCatalogItem`).
2. **Config schema (`AppModuleConfigSchema`)**
   - Lista de modificadores, tipos, defaults e regras (`required`, ranges, options).
   - Baseado em `AppModifierDefinition` e `AppModifierFieldType`.
3. **Preview definition (obrigatorio)**
   - Define `kind` e parametros visuais para renderer de card/lista.
   - Baseado em `AppPreviewDefinition` e resolvido pelo `AppPreviewRendererRegistry`.
4. **Runtime (`AppModuleRuntime`) opcional**
   - Necessario apenas para apps com execucao local dedicada (ex.: GIF playback).
   - Implementado em servico especifico (ex.: `GifCatalogAppRuntimeService`).

## Contratos de entrada/saida por camada

### 1) Camada de contratos/modelos

**Entrada**: JSON de catalogo + drafts de configuracao.

**Saida**: objetos de contrato validos (`AppCatalogItem`, preview e modifiers).

**Responsabilidade**:
- Definir formato dos dados sem efeito colateral.
- Expor validacao estrutural minima (`IsValid`).

### 2) Camada de UI (AppsPage + controles)

**Entrada**: `AppModuleDefinition` + `AppModuleConfigSchema`.

**Saida**: payload tipado para persistencia/deploy e render de preview.

**Responsabilidade**:
- Renderizar formulario dinamico de modificadores.
- Exibir preview com base em contrato (nao por tipo concreto de servico).
- Mostrar erros de validacao para input invalido.

### 3) Camada de aplicacao/servicos

**Entrada**: configuracao validada e contexto do dispositivo.

**Saida**: persistencia local, comando de deploy e estado operacional.

**Responsabilidade**:
- Persistir drafts de modificadores por `deviceId + appId`.
- Construir e enviar `set_app_config` via servico de deploy.
- Orquestrar runtime opcional, isolado por app.

### 4) Camada de integracao com dispositivo

**Entrada**: payload final de app/config.

**Saida**: resultado de dispatch (sucesso/falha + progresso).

**Responsabilidade**:
- Aplicar app/config no dispositivo sem regra de UI.
- Reportar status para telemetria e feedback de tela.

## Regras de dependencia

1. UI depende de **contratos** (manifest/schema/preview), nao de implementacoes concretas.
2. Implementacoes concretas de runtime/deploy ficam em **servicos especificos** (namespace `Services/Apps`).
3. Modelos de contrato nao dependem de UI nem de infraestrutura.
4. Registry/fabrica de preview pode mapear `preview.kind` para renderer concreto, preservando fronteira de contrato.
5. Quando houver runtime opcional, ele nao deve ser requisito para apps sem execucao local dedicada.

## Criterios de aceitacao para novos apps

Um novo app so e considerado aceito quando atender todos os itens:

- [ ] **Validacao de schema**: manifesto e modificadores passam validacao estrutural e tipos esperados.
- [ ] **Render de preview**: card/lista renderiza preview sem erro para `preview.kind` definido.
- [ ] **Persistencia de config**: salvar/restaurar draft funciona por `deviceId + appId`.
- [ ] **Deploy**: instalacao/aplicacao envia payload de configuracao esperado e retorna status observavel.

## Referencias de codigo

- [AppCatalogItem](../../../src/App.WinUI/Models/Apps/AppCatalogItem.cs#L1) - assinatura: `public sealed class AppCatalogItem`
- [AppPreviewDefinition](../../../src/App.WinUI/Models/Apps/AppPreviewDefinition.cs#L1) - assinatura: `public sealed class AppPreviewDefinition`
- [AppModifierDefinition](../../../src/App.WinUI/Models/Apps/AppModifierDefinition.cs#L1) - assinatura: `public sealed class AppModifierDefinition`
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L1) - assinatura: `public sealed class AppModifierStateStore`
- [AppDeploymentService](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L1) - assinatura: `public sealed class AppDeploymentService`
- [AppPreviewRendererRegistry](../../../src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs#L1) - assinatura: `public static class AppPreviewRendererRegistry`
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1) - assinatura: `internal sealed class GifCatalogAppRuntimeService`
