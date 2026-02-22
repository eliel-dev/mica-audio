# Guia - Adicionar item no catalogo de apps

## Objetivo

Adicionar um novo app no catalogo `schemaVersion: 2` com preview animado e modificadores dinamicos sem quebrar o fluxo de deploy. O serviço de catálogo lê os itens diretamente do JSON, então não é necessário alterar C# para incluir novos apps válidos.

## Passos

1. Edite `src/App.WinUI/AppData/apps-catalog.seed.json` e inclua item com campos obrigatorios (`id`, `name`, `packageName`, `category`).
2. Defina `preview.kind` (ex.: `clock`, `weather`, `scores`, `news`, `productivity`, `finance`, `decorative`).
3. Defina `modifiers` com `key`, `label`, `type` e defaults.
4. Valide tipos aceitos em `AppModifierFieldType`.
5. Não altere `AppCatalogService` para cadastrar o app: inclusão/edição é feita só no JSON.
6. Rode o app e clique `Recarregar` na aba `Apps`.
7. Valide `Salvar`, `Aplicar`, `Instalar` e `Ativar` com um dispositivo online.

## Referencias de codigo

- [AppCatalogItem](../../../src/App.WinUI/Models/Apps/AppCatalogItem.cs#L3) - assinatura: `public sealed class AppCatalogItem`
- [AppModifierDefinition](../../../src/App.WinUI/Models/Apps/AppModifierDefinition.cs#L3) - assinatura: `public sealed class AppModifierDefinition`
- [AppModifierFieldType](../../../src/App.WinUI/Models/Apps/AppModifierFieldType.cs#L3) - assinatura: `public enum AppModifierFieldType`
- [AppCatalogService.LoadCatalogAsync](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L26) - assinatura: `Task<IReadOnlyList<AppCatalogItem>> LoadCatalogAsync(...)`
- [AppsPage.LoadCatalogAsync](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L80) - assinatura: `private async Task LoadCatalogAsync()`
- [AppsPage.TryBuildConfigFromEditor](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L706) - assinatura: `private bool TryBuildConfigFromEditor(...)`

## Checklist rapido

- [ ] App novo aparece no catalogo e abre detalhes.
- [ ] Preview animado renderiza no card.
- [ ] Modificadores aparecem com controles corretos.
- [ ] `Salvar` persiste draft no escopo `deviceId+appId`.
- [ ] `Aplicar` envia `set_app_config`.
- [ ] `Instalar` inclui config salvo quando houver.
