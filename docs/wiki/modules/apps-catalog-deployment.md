# Modulo Apps Catalog and Deployment

## Objetivo

Documentar o fluxo de catalogo local de apps e envio de comandos de install/activate/config para um dispositivo online.

## Responsabilidades

- Seed e leitura de catalogo JSON.
- Filtro e exibicao de apps na UI.
- Traducao de item de catalogo para payload de comando.
- Disparo de comandos tracked no coordinator.

## Fluxo de execucao

1. `AppCatalogService.LoadCatalogAsync` garante seed e carrega itens validos.
2. `AppsPage` aplica filtro e selecao.
3. `AppDeploymentService` converte selecao em `DeviceAppCommandPayload`.
4. `DeviceOperationsCoordinator` envia comando tracked ao servidor.

## Pontos de alteracao frequente

- Schema do catalogo local.
- Criticidade de validacao de item.
- Campos de payload para firmware.

## Riscos e efeitos colaterais

- Mudanca de schema sem migracao pode invalidar catalogo existente.
- Divergencia entre payload e firmware causa comando sem efeito.

## Checklist apos alteracao

- Recarregar catalogo sem erro.
- Instalar e ativar app em device online.
- Verificar logs e status de comando.

## Referencias de codigo

- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L6) - assinatura: `internal sealed class AppCatalogService`
- [LoadCatalogAsync](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L23) - assinatura: `Task<IReadOnlyList<AppCatalogItem>> LoadCatalogAsync(...)`
- [AppDeploymentService](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L7) - assinatura: `internal sealed class AppDeploymentService`
- [InstallAsync](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L16) - assinatura: `Task<CommandDispatchResult> InstallAsync(...)`
- [AppsPage.LoadCatalogAsync](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L52) - assinatura: `private async Task LoadCatalogAsync()`

## Backlinks no codigo

- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Services/Apps/AppDeploymentService.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`