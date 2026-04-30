# Handoff - Resiliencia HTTP para clients externos

## Objetivo

Padronizar retry, timeout e circuit breaker para os clients HTTP de internet do app, cobrindo autocomplete de cidades e preview do clima com `Microsoft.Extensions.Http.Resilience`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `App.WinUI` registra named clients externos com resiliencia, autocomplete e forecast deixam de criar transporte manual, docs/backlinks ficam consistentes e validacoes estruturais passam sem regressao.

## Arquivos alterados

- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Infrastructure/Http/ExternalHttpClients.cs`
- `src/App.WinUI/Services/Apps/CityAutocompleteService.cs`
- `src/App.WinUI/Services/Apps/OpenMeteoForecastClient.cs`
- `src/App.WinUI/Services/Apps/WeatherPreviewDataService.cs`
- `src/App.WinUI/Views/Controls/Renderers/WeatherPreviewRenderer.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/CityAutocompleteServiceTests.cs`
- `tests/Output.Tests/OpenMeteoForecastClientTests.cs`
- `tests/Output.Tests/ExternalHttpClientsTests.cs`
- `tests/Output.Tests/WeatherPreviewDataServiceTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/apps-catalog-deployment.md`
- `docs/wiki/guides/troubleshoot-city-autocomplete.md`
- `docs/wiki/guides/configure-app-modifiers.md`

## Decisoes tomadas

1. O ponto oficial de integracao passou a ser `Microsoft.Extensions.Http.Resilience` por named client, sem introduzir `Microsoft.Extensions.Http.Polly` nem aplicar politica global ao HTTP local.
2. O autocomplete ficou no named client `open-meteo-geocoding` com profile `Short`, enquanto o preview do clima ficou no named client `open-meteo-forecast` com profile `Medium`.
3. O SDK `OpenMeteo.dotnet.client.sdk` foi removido do caminho clima e substituido por `OpenMeteoForecastClient`, porque o SDK encapsulava `HttpClient` fora do DI.
4. `CityAutocompleteService` e `OpenMeteoForecastClient` passaram a consumir `IHttpClientFactory`; os testes continuam podendo injetar `HttpClient` stubado diretamente.
5. O `WeatherPreviewRenderer` deixou de criar fallback de rede fora do container; sem `App.Services`, o preview entra em estado local de erro em vez de sair para a internet.
6. O circuit breaker foi ajustado para desktop de baixo throughput (`MinimumThroughput = 2`, `FailureRatio = 0.5`, `SamplingDuration = 30s`, `BreakDuration = 30s`) para evitar uma configuracao que nunca abriria na pratica.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test tests/Output.Tests/Output.Tests.csproj --filter "FullyQualifiedName~CityAutocompleteServiceTests|FullyQualifiedName~OpenMeteoForecastClientTests|FullyQualifiedName~WeatherPreviewDataServiceTests|FullyQualifiedName~ExternalHttpClientsTests" --no-build -> OK (17 aprovados)
```

## Riscos e rollback

- Risco principal: o circuit breaker de baixo throughput pode expor indisponibilidade temporaria de forma mais rapida na UI, o que e desejado, mas muda o perfil de erro percebido quando o Open-Meteo fica instavel.
- Como reverter:
  - remover `ExternalHttpClients` e os registros de named clients;
  - restaurar os construtores antigos que criavam `HttpClient`/SDK manualmente;
  - recolocar `OpenMeteo.dotnet.client.sdk` nos projetos que voltarem a depender dele.

## Proximos passos

1. Fazer smoke manual na aba `Apps` cobrindo autocomplete de cidade, card de clima e comportamento da UI em ausencia de rede.
2. Reusar `AddExternalHttpClients` quando entrarem clients de news/scores/finance ou qualquer chamada de runtime/deploy para internet.
