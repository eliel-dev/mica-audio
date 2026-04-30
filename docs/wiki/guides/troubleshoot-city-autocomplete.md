# Guia - Troubleshoot autocomplete de cidade

## Objetivo

Resolver problemas quando um widget de clima nao retorna sugestoes no campo de cidade do inspetor em `Paineis`.

## Passos

1. Verifique conectividade HTTP de saida para `geocoding-api.open-meteo.com`.
2. Digite pelo menos 2 caracteres no campo `Cidade` do widget selecionado.
3. Confirme que a lista de sugestoes do `AutoSuggestBox` abre abaixo do campo.
4. Verifique timeout ou bloqueio local de HTTPS.
5. Revise a mensagem de status da pagina; falhas de HTTP, timeout, circuito aberto ou resposta invalida aparecem ali.
6. Se continuar sem sugestoes, use texto manual da cidade e salve o widget.

## Referencias de codigo

- [CityAutocompleteService.SearchWithDiagnosticsAsync](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L1)
- [ExternalHttpClients](../../../src/App.WinUI/Infrastructure/Http/ExternalHttpClients.cs#L1)
- [AppModifierEditorHost.OnCitySuggestTextChanged](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L376)
- [AppModifierEditorHost.OnCitySuggestionChosen](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L456)
- [CitySuggestion.ToConfigValue](../../../src/App.WinUI/Services/Apps/CitySuggestion.cs#L15)

## Checklist rapido

- [ ] Busca retorna sugestoes com internet ativa.
- [ ] Busca inicia com 2 caracteres.
- [ ] A lista abre quando ha itens retornados.
- [ ] Falhas de timeout/HTTP/JSON aparecem na tela, sem sumir silenciosamente.
- [ ] Circuit breaker nao derruba o editor.
- [ ] Escolha de sugestao preenche o valor salvo no widget.
