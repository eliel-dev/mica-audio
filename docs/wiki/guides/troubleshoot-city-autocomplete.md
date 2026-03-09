# Guia - Troubleshoot autocomplete de cidade

## Objetivo

Resolver problemas quando o campo de cidade (clima) nao retorna sugestoes no `AutoSuggestBox`.

## Passos

1. Verifique conectividade HTTP de saida na maquina (acesso a `geocoding-api.open-meteo.com`).
2. Digite pelo menos 2 caracteres no campo `Cidade` para disparar o autocomplete.
3. Se nada aparecer, verifique se a lista de sugestoes abriu abaixo do campo; quando houver itens, o `AutoSuggestBox` deve exibir o dropdown automaticamente.
4. Confirme que nao ha timeout/restricao de firewall local para HTTPS. O app considera timeout de aproximadamente 8 segundos para a busca.
5. Revise a notificacao/log da aba `Apps`; falhas de HTTP, timeout ou resposta invalida do Open-Meteo agora aparecem explicitamente ali.
6. Se continuar sem sugestoes, use texto manual da cidade e aplique configuracao.

## Referencias de codigo

- [CityAutocompleteService.SearchAsync](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L14) - assinatura: `Task<IReadOnlyList<CitySuggestion>> SearchAsync(...)`
- [AppsPage.OnCitySuggestTextChanged](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1) - assinatura: `private async void OnCitySuggestTextChanged(...)`
- [AppsPage.OnCitySuggestionChosen](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1) - assinatura: `private void OnCitySuggestionChosen(...)`
- [CitySuggestion.ToConfigValue](../../../src/App.WinUI/Services/Apps/CitySuggestion.cs#L15) - assinatura: `string ToConfigValue()`

## Checklist rapido

- [ ] Busca retorna sugestoes com internet ativa.
- [ ] Busca inicia com 2 caracteres, sem divergencia entre UI e service.
- [ ] Lista de sugestoes abre quando ha itens retornados.
- [ ] Falhas de timeout/HTTP/JSON aparecem na aba `Apps`, sem sumir silenciosamente.
- [ ] Timeout/cancelamento nao quebram a tela.
- [ ] Escolha de sugestao preenche e persiste o valor.
- [ ] Campo manual continua funcional sem autocomplete.
