# Guia - Adicionar item no catalogo de widgets

## Objetivo

Adicionar um novo item ao catalogo HUB75 para que ele apareca na biblioteca de `Paineis` e possa virar widget quando houver renderer suportado.

## Passos

1. Edite `src/App.WinUI/AppData/apps-catalog.seed.json` e inclua o item com `id`, `name`, `packageName` e `category`.
2. Defina `preview.kind` e os parametros visuais esperados para o card.
3. Defina `modifiers` com `key`, `label`, `type` e defaults.
4. Valide os tipos suportados em `AppModifierFieldType`.
5. Nao altere `AppCatalogService` para cadastrar o item: a fonte de verdade continua sendo o JSON.
6. Abra `Paineis` e confirme que o item aparece na biblioteca.
7. Se o item precisar ser adicionavel ao canvas, implemente o renderer HUB75 correspondente em `PanelsFrameComposer` no projeto compartilhado `MicaAudio.PanelRuntime`.

## Referencias de codigo

- [AppCatalogItem](../../../src/App.WinUI/Models/Apps/AppCatalogItem.cs#L1)
- [AppModifierDefinition](../../../src/App.WinUI/Models/Apps/AppModifierDefinition.cs#L1)
- [AppModifierFieldType](../../../src/App.WinUI/Models/Apps/AppModifierFieldType.cs#L1)
- [AppCatalogService.LoadCatalogAsync](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L1)
- [PanelsPage.LoadCatalogAsync](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L149)
- [PanelsPage.ResolveWidgetDefaultValuesAsync](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1505)
- [PanelsFrameComposer](../../../src/MicaAudio.PanelRuntime/Services/Panels/PanelsFrameComposer.cs#L1)

## Checklist rapido

- [ ] O item aparece na biblioteca de widgets.
- [ ] O preview do card renderiza sem erro.
- [ ] Os modificadores aparecem no inspetor.
- [ ] O item ganha badge de indisponivel quando ainda nao existe renderer HUB75.
- [ ] Quando houver renderer, o widget pode ser arrastado para o canvas.
