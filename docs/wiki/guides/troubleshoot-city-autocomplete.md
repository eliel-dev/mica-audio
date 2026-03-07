# Guia - Troubleshoot autocomplete de cidade

## Objetivo

Resolver problemas quando o campo de cidade (clima) nao retorna sugestoes no `AutoSuggestBox`.

## Passos

1. Verifique conectividade HTTP de saida na maquina (acesso a `geocoding-api.open-meteo.com`).
2. Digite pelo menos 2 caracteres no campo `Cidade`.
3. Confirme que nao ha timeout/restricao de firewall local para HTTPS.
4. Revise logs da aba `Apps` para mensagens de erro no fluxo de configuracao.
5. Se continuar sem sugestoes, use texto manual da cidade e aplique configuracao.

## Referencias de codigo

- [CityAutocompleteService.SearchAsync](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L14) - assinatura: `Task<IReadOnlyList<CitySuggestion>> SearchAsync(...)`
- [AppsPage.OnCitySuggestTextChanged](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1) - assinatura: `private async void OnCitySuggestTextChanged(...)`
- [AppsPage.OnCitySuggestionChosen](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1) - assinatura: `private void OnCitySuggestionChosen(...)`
- [CitySuggestion.ToConfigValue](../../../src/App.WinUI/Services/Apps/CitySuggestion.cs#L15) - assinatura: `string ToConfigValue()`

## Checklist rapido

- [ ] Busca retorna sugestoes com internet ativa.
- [ ] Timeout/cancelamento nao quebram a tela.
- [ ] Escolha de sugestao preenche e persiste o valor.
- [ ] Campo manual continua funcional sem autocomplete.
