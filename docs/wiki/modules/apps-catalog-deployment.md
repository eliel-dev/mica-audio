# Modulo Apps Catalog And Deployment

Os apps HUB75 da loja agora assumem grade nativa `128x64`.

## Atualizacao 2026-03 - Fase 9 Wave 3

- `AppsPage` deixou de concentrar catalogo, runtime GIF, drafts e deploy em um unico code-behind.
- A tela foi decomposta em partials por responsabilidade:
  - `AppsPage.Catalog`
  - `AppsPage.RuntimePreview`
  - `AppsPage.Modifiers`
  - `AppsPage.Deployment`
- O fluxo funcional nao mudou:
  - catalogo continua carregando do seed/disk;
  - deploy continua usando `SaveAppConfigUseCase` + `DeployAppUseCase`;
  - runtime GIF continua ligado ao catalogo via `GifCatalogAppRuntimeService`.

## Referencias de codigo

- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L1)
- [AppsPage Catalog](../../../src/App.WinUI/Views/AppsPage.Catalog.cs#L1)
- [AppsPage RuntimePreview](../../../src/App.WinUI/Views/AppsPage.RuntimePreview.cs#L1)
- [AppsPage Modifiers](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1)
- [AppsPage Deployment](../../../src/App.WinUI/Views/AppsPage.Deployment.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1)
- [apps-catalog.seed.json](../../../src/App.WinUI/AppData/apps-catalog.seed.json#L1)
