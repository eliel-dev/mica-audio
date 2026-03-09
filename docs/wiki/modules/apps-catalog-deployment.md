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

## Integracoes HTTP externas

- O catalogo local continua offline-first; as integracoes HTTP externas da aba `Apps` hoje ficam restritas ao fluxo clima.
- `CityAutocompleteService` usa o named client `open-meteo-geocoding` para consultar sugestoes brasileiras no Open-Meteo.
- `OpenMeteoForecastClient` usa o named client `open-meteo-forecast` para alimentar o preview local do card de clima.
- O padrao oficial para novos clients de news/scores/finance e para qualquer runtime/deploy que saia para a internet passa a ser:
  - registrar named client em `AddExternalHttpClients`;
  - escolher perfil interno `Short`, `Medium` ou `Long`;
  - consumir `IHttpClientFactory`, sem `new HttpClient()` espalhado nem SDKs que encapsulem transporte sem DI.

## Cache compartilhado

- `AppCatalogService` agora usa `HybridCache` como cache compartilhado em memoria para o catalogo efetivo.
- A chave canonica desta etapa cobre apenas o catalogo de apps.
- `LoadCatalogAsync()` atende pela entrada cacheada.
- `ReloadCatalogAsync()` invalida a chave e forca leitura nova do seed/disco.
- O objetivo e reduzir merge/reparse/disco repetido quando:
  - a app sobe;
  - a aba `Apps` e aberta;
  - a `DevicesPage` resolve nomes/previews de apps.
- O cache do clima foi adiado de forma intencional:
  - o app de clima atual nao entra neste item;
  - o futuro redesign deve partir de cidade fixa em codigo, com primeira opcao `Timbó-SC`.

## Referencias de codigo

- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L1)
- [AppsPage Catalog](../../../src/App.WinUI/Views/AppsPage.Catalog.cs#L1)
- [AppsPage RuntimePreview](../../../src/App.WinUI/Views/AppsPage.RuntimePreview.cs#L1)
- [AppsPage Modifiers](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1)
- [AppsPage Deployment](../../../src/App.WinUI/Views/AppsPage.Deployment.cs#L1)
- [AppCacheKeys](../../../src/App.WinUI/Infrastructure/Cache/AppCacheKeys.cs#L1)
- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L1)
- [ExternalHttpClients](../../../src/App.WinUI/Infrastructure/Http/ExternalHttpClients.cs#L1)
- [CityAutocompleteService](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L1)
- [OpenMeteoForecastClient](../../../src/App.WinUI/Services/Apps/OpenMeteoForecastClient.cs#L1)
- [WeatherPreviewDataService](../../../src/App.WinUI/Services/Apps/WeatherPreviewDataService.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1)
- [apps-catalog.seed.json](../../../src/App.WinUI/AppData/apps-catalog.seed.json#L1)
