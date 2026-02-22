# Modulo Apps Catalog and Deployment

## Objetivo

Documentar o fluxo de catalogo local, miniaturas animadas, modificadores dinamicos por app, runtime local do app `gifhub75` e envio de comandos de deploy para dispositivo online.

## Responsabilidades

- Carregar catalogo (`schemaVersion: 2`) com `preview` e `modifiers`.
- Renderizar cards com preview animado procedural (Win2D).
- Persistir modificadores por escopo `deviceId + appId`.
- Buscar cidade (clima) via Open-Meteo Geocoding.
- Enviar comandos tracked para install/activate/config no coordinator.
- Executar runtime desktop do app `gifhub75` com fonte `url|file`, escala `fit|fill|stretch` e `12 FPS`.
- Fazer broadcast de frame HUB75 para devices online e preview local no painel de runtime do AppsPage.

## Fluxo de execucao

1. `AppCatalogService.LoadCatalogAsync` carrega `catalog.json` e valida itens.
2. `AppsPage` monta cards (`AppCatalogCardControl`) e anima previews visiveis/selecionado.
3. Selecionar app + dispositivo carrega draft em `AppModifierStateStore`.
4. Clique manual no card `gifhub75` tenta auto-start do runtime: `sourceMode=url` usa `gifUrl`; `sourceMode=file` usa arquivo da sessao.
5. `Salvar` persiste modificadores localmente e, se `gifhub75` estiver selecionado, reaplica o runtime imediatamente.
6. `Instalar` usa draft salvo para incluir `configJson` no payload quando houver.
7. Troca para outro app (ou unload da pagina) executa `Stop()` do runtime GIF.

## Pontos de alteracao frequente

- Schema do catalogo (`preview/modifiers`).
- Tipos de campo dinamico (`AppModifierFieldType`).
- Renderizadores de preview por categoria.
- Validacao/serializacao de `configJson`.
- Regras de start/stop do runtime `gifhub75`.

## Riscos e efeitos colaterais

- Modificador mal definido no catalogo pode bloquear salvamento/aplicacao.
- Excesso de previews ativos pode aumentar custo de render.
- Falha de autocomplete nao deve bloquear entrada manual de cidade.
- `gifhub75` com URL invalida/timeout deve manter estabilidade da UI sem crash.

## Checklist apos alteracao

- Recarregar catalogo sem erro.
- Cards exibem preview animado no viewport.
- `Salvar` persiste por `deviceId+appId`.
- Selecionar manualmente `gifhub75` inicia runtime quando configuracao estiver valida.
- Trocar para outro app interrompe runtime GIF.
- `Instalar` inclui config salvo quando disponivel.

## Referencias de codigo

- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L7) - assinatura: `internal sealed class AppCatalogService`
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L7) - assinatura: `internal sealed class AppModifierStateStore`
- [CityAutocompleteService](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L7) - assinatura: `internal sealed class CityAutocompleteService`
- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L16) - assinatura: `public sealed partial class AppsPage`
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1) - assinatura: `internal sealed class GifCatalogAppRuntimeService`
- [AppPreviewThumbnailControl](../../../src/App.WinUI/Views/Controls/AppPreviewThumbnailControl.cs#L12) - assinatura: `internal sealed class AppPreviewThumbnailControl`
- [AppPreviewRendererRegistry](../../../src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs#L6) - assinatura: `internal static class AppPreviewRendererRegistry`
- [GifPreviewRenderer](../../../src/App.WinUI/Views/Controls/Renderers/GifPreviewRenderer.cs#L1) - assinatura: `internal sealed class GifPreviewRenderer`
- [AppDeploymentService](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L8) - assinatura: `internal sealed class AppDeploymentService`

## Backlinks no codigo

- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs`
- `src/App.WinUI/Services/Apps/AppModifierStateStore.cs`
- `src/App.WinUI/Services/Apps/CityAutocompleteService.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.Ui.cs`
- `src/App.WinUI/Views/Controls/Renderers/GifPreviewRenderer.cs`
- `src/App.WinUI/Services/Apps/AppDeploymentService.cs`
