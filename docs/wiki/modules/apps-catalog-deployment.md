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
4. `SaveAppConfigUseCase` salva o draft local sem acoplar a pagina ao repositório.
5. `AppConfigValidationUseCase` valida/converte `rawValues` para `configJson` tipado.
6. `DeployAppUseCase` orquestra validar + persistir draft + `AppDeploymentService.InstallAsync`.
7. `StartLocalRuntimeUseCase` encapsula start/stop/autostart do `gifhub75` (URL/arquivo).
8. `AppsPage` fica apenas em composição de controles, binding e delegação para use cases.

## Pontos de alteracao frequente

- Schema do catalogo (`preview/modifiers`).
- Tipos de campo dinamico (`AppModifierFieldType`).
- Renderizadores de preview por categoria.
- Validacao/serializacao de `configJson`.
- Regras de start/stop do runtime `gifhub75`.
- Orquestracao de casos de uso em `Services/Apps/UseCases`.

## Riscos e efeitos colaterais

- Modificador mal definido no catalogo pode bloquear salvamento/aplicacao.
- Excesso de previews ativos pode aumentar custo de render.
- Falha de autocomplete nao deve bloquear entrada manual de cidade.
- `gifhub75` com URL invalida/timeout deve manter estabilidade da UI sem crash.

## Checklist apos alteracao

- Recarregar catalogo sem erro.
- Cards exibem preview animado no viewport.
- `Salvar` persiste por `deviceId+appId` via use case.
- Selecionar manualmente `gifhub75` inicia runtime quando configuracao estiver valida.
- Trocar para outro app interrompe runtime GIF.
- `Instalar` valida payload e inclui config salvo quando disponivel.

## Referencias de codigo

- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L7) - assinatura: `internal sealed class AppCatalogService`
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L7) - assinatura: `internal sealed class AppModifierStateStore`
- [CityAutocompleteService](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L7) - assinatura: `internal sealed class CityAutocompleteService`
- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L16) - assinatura: `public sealed partial class AppsPage`
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1) - assinatura: `internal sealed class GifCatalogAppRuntimeService`
- [SaveAppConfigUseCase](../../../src/App.WinUI/Services/Apps/UseCases/SaveAppConfigUseCase.cs#L1) - assinatura: `internal sealed class SaveAppConfigUseCase`
- [AppConfigValidationUseCase](../../../src/App.WinUI/Services/Apps/UseCases/AppConfigValidationUseCase.cs#L1) - assinatura: `internal sealed class AppConfigValidationUseCase`
- [DeployAppUseCase](../../../src/App.WinUI/Services/Apps/UseCases/DeployAppUseCase.cs#L1) - assinatura: `internal sealed class DeployAppUseCase`
- [StartLocalRuntimeUseCase](../../../src/App.WinUI/Services/Apps/UseCases/StartLocalRuntimeUseCase.cs#L1) - assinatura: `internal sealed class StartLocalRuntimeUseCase`

## Backlinks no codigo

- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs`
- `src/App.WinUI/Services/Apps/AppModifierStateStore.cs`
- `src/App.WinUI/Services/Apps/CityAutocompleteService.cs`
- `src/App.WinUI/Services/Apps/UseCases/SaveAppConfigUseCase.cs`
- `src/App.WinUI/Services/Apps/UseCases/AppConfigValidationUseCase.cs`
- `src/App.WinUI/Services/Apps/UseCases/DeployAppUseCase.cs`
- `src/App.WinUI/Services/Apps/UseCases/StartLocalRuntimeUseCase.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.Ui.cs`
