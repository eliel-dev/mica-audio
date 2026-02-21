# Handoff - Apps vNext com miniaturas animadas e modificadores dinamicos

## Objetivo

Implementar UX da aba `Apps` com previews animados (Win2D procedural), editor dinamico de modificadores e persistencia por `deviceId+appId`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: cards animados no catalogo, salvar/aplicar modificadores por dispositivo e build/documentacao validos.

## Arquivos alterados

- `src/App.WinUI/Models/Apps/AppCatalogItem.cs`
- `src/App.WinUI/Models/Apps/AppPreviewDefinition.cs`
- `src/App.WinUI/Models/Apps/AppModifierDefinition.cs`
- `src/App.WinUI/Models/Apps/AppModifierOption.cs`
- `src/App.WinUI/Models/Apps/AppModifierFieldType.cs`
- `src/App.WinUI/Models/Apps/AppConfigDraft.cs`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Services/Apps/AppModifierStateDocument.cs`
- `src/App.WinUI/Services/Apps/AppModifierStateStore.cs`
- `src/App.WinUI/Services/Apps/CityAutocompleteService.cs`
- `src/App.WinUI/Services/Apps/CitySuggestion.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/AppsPage.Ui.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/Controls/AppPreviewRenderContext.cs`
- `src/App.WinUI/Views/Controls/IAppPreviewRenderer.cs`
- `src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs`
- `src/App.WinUI/Views/Controls/AppPreviewDrawHelpers.cs`
- `src/App.WinUI/Views/Controls/AppPreviewThumbnailControl.cs`
- `src/App.WinUI/Views/Controls/AppCatalogCardControl.cs`
- `src/App.WinUI/Views/Controls/Renderers/*.cs`
- `src/App.WinUI/AppData/apps-catalog.seed.json`
- `docs/wiki/modules/apps-catalog-deployment.md`
- `docs/wiki/guides/add-app-catalog-item.md`
- `docs/wiki/guides/configure-app-modifiers.md`
- `docs/wiki/guides/troubleshoot-city-autocomplete.md`
- `docs/wiki/reference/ws-protocol-v1.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/README.md`

## Decisoes tomadas

1. Removida compatibilidade extra de fallback v1 no `AppCatalogService` por solicitacao explicita do usuario.
2. Mantido layout code-first em `AppsPage.Ui.cs` para evitar regressao com `Page Remove` atual.
3. Preview animado com limite de execucao em cards visiveis + selecionado (max 6 + selecionado).
4. Persistencia local de modificadores em `%AppData%/MicaAudio/apps/modifiers.json`, chave `deviceId|appId`.
5. `Salvar` e `Aplicar` separados; `Instalar` reaproveita draft salvo quando disponivel.
6. Autocomplete de cidade usando Open-Meteo Geocoding com debounce/cancelamento e fallback silencioso.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> (pendente apos ajustes finais de docs)
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> (pendente apos ajustes finais de docs)
dotnet build MicaAudio.sln -c Debug -> sucesso (com warnings existentes de analyzers)
```

## Riscos e rollback

- Risco principal: alta complexidade no code-behind da `AppsPage` pode aumentar manutencao.
- Como reverter: restaurar `AppsPage` para fluxo antigo (catalogo simples) e remover `Views/Controls/*` novos.

## Proximos passos

1. Executar app e validar manualmente previews + modificadores (especialmente clima/autocomplete).
2. Rodar `docs-validate` e `ai-governance-check` apos revisao final das referencias/linhas da wiki.
3. Opcional: mover a parte de form dinamico para um controle dedicado (`AppModifierEditorControl`) para reduzir acoplamento.
