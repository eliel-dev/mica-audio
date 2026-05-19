# Guia - Configurar modificadores de widgets

## Objetivo

Explicar como configurar widgets derivados do catalogo HUB75 dentro da sessao `Paineis`.

## Passos

1. Abra `Paineis` e entre no editor de um painel.
2. Busque o widget desejado na biblioteca lateral.
3. Arraste o widget suportado para o canvas HUB75.
4. Selecione o widget no canvas para abrir seus modificadores no inspetor direito.
5. Ajuste os campos e salve o painel.

## Drafts locais

- O app continua usando `%AppData%/MicaAudio/apps/modifiers.json` como store local de defaults.
- Ao criar um widget novo, a `PanelsPage` reaproveita o draft `__local__|appId`, se existir.
- O widget salvo no painel continua tendo `ConfigValues` proprios; alterar um widget nao sobrescreve automaticamente o draft local.

## Widgets clima

- Widgets baseados em clima continuam usando `CityAutocompleteService` e `OpenMeteoForecastClient` via DI.
- O editor compartilhado de modifiers (`AppModifierEditorHost`) continua normalizando valores de cidade e mensagens de erro de autocomplete.

## Apps Relogio

- O app `analogclock` usa o modifier `mostrador` para selecionar um dos 9 estilos HUB75 renderizados pelo compositor: `cyberterminal`, `flipclock`, `neotokyo`, `relogiochuva`, `aurora`, `gridscifi`, `retroambar`, `cosmico` e `monocromatico`.
- O renderer C#/WinUI e a aba Android de `Mostradores` preservam o mesmo canvas logico `128x64`; widgets menores recebem uma copia reescalada desse frame.

## Referencias de codigo

- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsPage UI](../../../src/App.WinUI/Views/PanelsPage.Ui.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)
- [AppModifierStateStore.SetDraftAsync](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L78)
- [AppModifierStateStore.GetDraftAsync](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L61)
- [CityAutocompleteService](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L1)

## Checklist rapido

- [ ] O widget novo nasce com defaults do catalogo e, quando existir, com o draft local reaplicado.
- [ ] Os campos do inspetor seguem o schema do item no catalogo.
- [ ] O painel salvo preserva a configuracao por widget sem depender da antiga sessao `Apps`.
