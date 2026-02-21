# Handoff - Localizacao BR no autocomplete e preview apps

## Objetivo

Garantir que os apps de Clima/Relogio estejam localizados para Brasil e que a lista de cidades no autocomplete exiba nomes corretamente.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: lista de sugestoes de cidade mostra nomes legiveis (nao tipo/classe), busca prioriza cidades do Brasil e previews seguem fallback correto para clima/relogio.

## Arquivos alterados

- src/App.WinUI/Services/Apps/CityAutocompleteService.cs
- src/App.WinUI/Services/Apps/CitySuggestion.cs
- src/App.WinUI/Views/AppsPage.xaml.cs
- src/App.WinUI/Services/Apps/AppCatalogService.cs
- src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs
- src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs
- src/App.WinUI/Views/Controls/Renderers/ClockPreviewRenderer.cs
- src/App.WinUI/Views/Controls/Renderers/WeatherPreviewRenderer.cs
- docs/handoffs/2026-02-21-localizacao-br-autocomplete-preview.md

## Decisoes tomadas

1. Busca de cidade com `language=pt` e `countryCode=BR` para foco em localizacao brasileira.
2. `CitySuggestion.ToString()` retorna `DisplayName` para garantir render correto no dropdown.
3. `AutoSuggestBox` configurado com `TextMemberPath = DisplayName` para reforcar exibicao.
4. Catalogo de apps com fallback por `appId` (`accuweather`/`analogclock`) para manter preview/modifiers corretos mesmo com catalogo antigo no AppData.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

## Riscos e rollback

- Risco principal: filtro BR pode ocultar cidades fora do Brasil no autocomplete.
- Como reverter: remover `countryCode=BR` da URL em `CityAutocompleteService`.

## Proximos passos

1. Se quiser suporte internacional opcional, adicionar toggle `Apenas Brasil` nos modificadores de Clima.
2. Refinar seed para strings com acentuacao PT-BR onde necessario.
