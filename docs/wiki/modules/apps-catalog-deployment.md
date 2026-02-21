# Modulo Apps Catalog and Deployment

## Objetivo

Documentar o fluxo de catalogo local, miniaturas animadas, modificadores dinamicos por app e envio de comandos `install/activate/set_app_config` para dispositivo online.

## Responsabilidades

- Carregar catalogo (`schemaVersion: 2`) com `preview` e `modifiers`.
- Renderizar cards com preview animado procedural (Win2D).
- Persistir modificadores por escopo `deviceId + appId`.
- Buscar cidade (clima) via Open-Meteo Geocoding.
- Enviar comandos tracked para install/activate/config no coordinator.

## Fluxo de execucao

1. `AppCatalogService.LoadCatalogAsync` carrega `catalog.json` e valida itens.
2. `AppsPage` monta cards (`AppCatalogCardControl`) e anima previews visiveis/selecionado.
3. Selecionar app + dispositivo carrega draft em `AppModifierStateStore`.
4. `Salvar` persiste localmente os modificadores.
5. `Aplicar` envia `set_app_config` via `AppDeploymentService`.
6. `Instalar` usa draft salvo para incluir `configJson` no payload quando houver.

## Pontos de alteracao frequente

- Schema do catalogo (`preview/modifiers`).
- Tipos de campo dinamico (`AppModifierFieldType`).
- Renderizadores de preview por categoria.
- Validacao/serializacao de `configJson`.

## Riscos e efeitos colaterais

- Modificador mal definido no catalogo pode bloquear salvamento/aplicacao.
- Excesso de previews ativos pode aumentar custo de render.
- Falha de autocomplete nao deve bloquear entrada manual de cidade.

## Checklist apos alteracao

- Recarregar catalogo sem erro.
- Cards exibem preview animado no viewport.
- `Salvar` persiste por `deviceId+appId`.
- `Aplicar` envia comando tracked e atualiza logs/progresso.
- `Instalar` inclui config salvo quando disponivel.

## Referencias de codigo

- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L7) - assinatura: `internal sealed class AppCatalogService`
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L7) - assinatura: `internal sealed class AppModifierStateStore`
- [CityAutocompleteService](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L7) - assinatura: `internal sealed class CityAutocompleteService`
- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L16) - assinatura: `public sealed partial class AppsPage`
- [AppPreviewThumbnailControl](../../../src/App.WinUI/Views/Controls/AppPreviewThumbnailControl.cs#L12) - assinatura: `internal sealed class AppPreviewThumbnailControl`
- [AppPreviewRendererRegistry](../../../src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs#L6) - assinatura: `internal static class AppPreviewRendererRegistry`
- [AppDeploymentService](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L8) - assinatura: `internal sealed class AppDeploymentService`

## Backlinks no codigo

- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Services/Apps/AppModifierStateStore.cs`
- `src/App.WinUI/Services/Apps/CityAutocompleteService.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.Ui.cs`
- `src/App.WinUI/Services/Apps/AppDeploymentService.cs`
