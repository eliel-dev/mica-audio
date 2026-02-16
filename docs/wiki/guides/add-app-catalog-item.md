# Guia - Adicionar item no catalogo de apps

## Objetivo

Adicionar um novo app no catalogo local sem quebrar validacao e fluxo de deploy.

## Passos

1. Editar `src/App.WinUI/AppData/apps-catalog.seed.json` e incluir item valido (`id`, `name`, `packageName`, `category`).
2. Conferir regras de validacao em `AppCatalogItem.IsValid()`.
3. Executar app e clicar em recarregar catalogo.
4. Validar que item aparece, instala e ativa em device online.

## Referencias de codigo

- [AppCatalogService.LoadCatalogAsync](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L23) - assinatura: `Task<IReadOnlyList<AppCatalogItem>> LoadCatalogAsync(...)`
- [AppCatalogService.EnsureCatalogSeededAsync](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L38) - assinatura: `private async Task EnsureCatalogSeededAsync(...)`
- [AppsPage.LoadCatalogAsync](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L52) - assinatura: `private async Task LoadCatalogAsync()`
- [AppDeploymentService.InstallAsync](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L16) - assinatura: `Task<CommandDispatchResult> InstallAsync(...)`

## Checklist rapido

- [ ] Item novo aparece no catalogo da UI.
- [ ] Install retorna comando aceito.
- [ ] Activate muda app ativo reportado no device.