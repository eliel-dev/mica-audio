# Modulo Shared App Catalog

O catalogo de apps HUB75 continua existindo como fonte de verdade dos itens disponiveis, mas a sessao `Apps` saiu da shell. A configuracao operacional agora acontece em `Paineis`, onde cada app entra como widget configuravel por instancia.

## Estado Atual

- `AppCatalogService` continua carregando o seed local + override do usuario e produzindo o catalogo efetivo do app.
- `AppModifierStateStore` permanece como repositório de drafts locais em `%AppData%/MicaAudio/apps/modifiers.json`.
- Os drafts legados `__local__|appId` agora servem como defaults ao criar widgets na `PanelsPage`.
- `DevicesPage` ainda consome catalogo + drafts para preview e diagnostico de `ActiveAppId`.
- O fluxo de deploy individual por app nao faz mais parte da experiencia principal da app.

## Integracoes HTTP Externas

- As integracoes HTTP que continuam ativas para o catalogo/widgets estao hoje no fluxo de clima:
  - `CityAutocompleteService` usa `open-meteo-geocoding`;
  - `OpenMeteoForecastClient` usa `open-meteo-forecast`.
- O padrao oficial para qualquer nova integracao externa continua sendo:
  - registrar named client em `AddExternalHttpClients`;
  - escolher perfil interno de resiliencia/timeout;
  - consumir `IHttpClientFactory`.

## Cache e Compatibilidade

- `AppCatalogService` continua usando `HybridCache` para evitar merge/reparse repetido do catalogo.
- `ReloadCatalogAsync()` segue sendo o ponto de invalidação manual do cache.
- Os caminhos `apps/catalog.json` e `apps/modifiers.json` foram mantidos por compatibilidade e para reaproveitar dados locais ja existentes.

## Referencias de codigo

- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L1)
- [IAppCatalogService](../../../src/App.WinUI/Services/Apps/IAppCatalogService.cs#L1)
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L1)
- [IAppModifierStateStore](../../../src/App.WinUI/Services/Apps/IAppModifierStateStore.cs#L1)
- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)
- [AppCatalogCardControl](../../../src/App.WinUI/Views/Controls/AppCatalogCardControl.cs#L1)
- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [AppCacheKeys](../../../src/App.WinUI/Infrastructure/Cache/AppCacheKeys.cs#L1)
- [ExternalHttpClients](../../../src/App.WinUI/Infrastructure/Http/ExternalHttpClients.cs#L1)
- [CityAutocompleteService](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L1)
- [OpenMeteoForecastClient](../../../src/App.WinUI/Services/Apps/OpenMeteoForecastClient.cs#L1)
- [WeatherPreviewDataService](../../../src/App.WinUI/Services/Apps/WeatherPreviewDataService.cs#L1)
- [apps-catalog.seed.json](../../../src/App.WinUI/AppData/apps-catalog.seed.json#L1)
