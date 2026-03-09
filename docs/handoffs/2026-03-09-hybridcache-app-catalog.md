# Handoff - HybridCache baseline para catalogo de apps

## Objetivo

Introduzir `HybridCache` como baseline de cache compartilhado no app e aplica-lo ao catalogo de apps, sem expandir cache para autocomplete ou para o fluxo atual de clima.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `HybridCache` registrado no bootstrap, `AppCatalogService` com leitura cacheada e reload explicito, docs/backlinks consistentes e validacoes estruturais sem regressao.

## Arquivos alterados

- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Infrastructure/Cache/AppCacheKeys.cs`
- `src/App.WinUI/Services/Apps/IAppCatalogService.cs`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Views/AppsPage.Catalog.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/AppCatalogServiceTests.cs`
- `tests/Output.Tests/AppCatalogRuntimeKindTests.cs`
- `tests/Output.Tests/HybridCacheBootstrapTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/apps-catalog-deployment.md`

## Decisoes tomadas

1. A biblioteca adotada foi `Microsoft.Extensions.Caching.Hybrid`, nao `FusionCache`, para manter o baseline em `Microsoft.Extensions.*` e evitar ampliar dependencias nesta etapa.
2. O cache compartilhado foi aplicado apenas ao catalogo, porque o clima sera redesenhado em outra etapa e o autocomplete existe hoje apenas para esse fluxo.
3. O valor cacheado e o catalogo efetivo ja mergeado e normalizado, nao os documentos brutos de seed ou disco.
4. A chave canonica desta etapa ficou em `apps:catalog:effective` com TTL de `10 minutos`.
5. O contrato do servico foi separado em dois caminhos:
   - `LoadCatalogAsync()` para leitura normal cacheada;
   - `ReloadCatalogAsync()` para invalidacao explicita seguida de recarga que volta a popular o cache.
6. Escritas internas do proprio `AppCatalogService` (`SaveDocumentAsync`) tambem invalidam a chave para evitar snapshot antigo depois de seed ou normalizacao.
7. O fluxo atual de clima ficou explicitamente fora do escopo; o proximo item deve partir de cidades fixas em codigo, com primeira opcao `Timbo-SC`.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test tests/Output.Tests/Output.Tests.csproj --filter "FullyQualifiedName~AppCatalogServiceTests|FullyQualifiedName~AppCatalogRuntimeKindTests|FullyQualifiedName~HybridCacheBootstrapTests" -> OK (8 aprovados)
```

## Riscos e rollback

- Risco principal: call sites que precisem de leitura realmente fresca devem usar `ReloadCatalogAsync()`; `LoadCatalogAsync()` agora serve do cache por design.
- Como reverter:
  - remover `AddHybridCache()` e `AppCacheKeys`;
  - voltar `IAppCatalogService` para um unico metodo;
  - remover a invalidacao explicita do `AppCatalogService`.

## Proximos passos

1. Refatorar o app de clima separadamente, com cidade fixa em codigo e primeira opcao `Timbo-SC`.
2. Reavaliar `CityAutocompleteService` junto do redesign do clima; nao expandir cache para esse fluxo antes dessa decisao.
