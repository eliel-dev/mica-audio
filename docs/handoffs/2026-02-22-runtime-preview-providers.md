# Handoff - Providers declarativos para preview/runtime de apps

## Objetivo
Desacoplar resolução de preview e runtime local do catálogo de apps por interfaces de extensão (`preview.kind` / `runtime.kind`), removendo condicionais por `item.Id` na UI.

## Escopo classificado
Estrutural.

## Arquivos alterados
- src/App.WinUI/Models/Apps/AppCatalogItem.cs
- src/App.WinUI/Models/Apps/AppRuntimeDefinition.cs
- src/App.WinUI/Views/Controls/IAppPreviewProvider.cs
- src/App.WinUI/Views/Controls/AppPreviewProvider.cs
- src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs
- src/App.WinUI/Services/Apps/IAppRuntimeProvider.cs
- src/App.WinUI/Services/Apps/AppRuntimeHost.cs
- src/App.WinUI/Services/Apps/AppRuntimeProviderRegistry.cs
- src/App.WinUI/Services/Apps/GifHub75RuntimeProvider.cs
- src/App.WinUI/Services/Apps/AppCatalogService.cs
- src/App.WinUI/AppData/apps-catalog.seed.json
- src/App.WinUI/Views/AppsPage.xaml.cs
- src/App.WinUI/Views/AppsPage.Ui.cs
- tests/Output.Tests/Output.Tests.csproj
- tests/Output.Tests/AppCatalogRuntimeKindTests.cs
- tests/Output.Tests/AppRuntimeProviderRegistryTests.cs

## Decisoes tomadas
1. Registry de preview passou a ser orientado por `IAppPreviewProvider` com mapeamento declarativo por kind.
2. Runtime local foi extraído para `IAppRuntimeProvider` e provider dedicado `GifHub75RuntimeProvider`.
3. `AppCatalogItem` ganhou `Runtime` com `Runtime.Kind` para resolução por capacidade, sem condicional por `item.Id` na seleção da UI.
4. `AppsPage` passou a selecionar provider via `AppRuntimeProviderRegistry` e delegar start/stop/configuração.
5. Foram adicionados testes de resolução de provider/runtime e mapeamento do catálogo.

## Validacoes executadas
- Não foi possível executar validações .NET/PowerShell no ambiente atual por ausência de `dotnet` e `powershell` no PATH.

## Riscos e rollback
- Risco: regressão no ciclo de vida do runtime GIF ao trocar seleção rapidamente.
- Risco: divergência entre metadata `runtime.kind` do catálogo e providers registrados.
- Rollback: reverter commit atual e restaurar fluxo anterior de runtime no `AppsPage` com `GifCatalogAppRuntimeService` direto.

## Proximos passos
1. Registrar novos providers de runtime conforme crescimento do catálogo.
2. Cobrir com testes de integração de seleção de card + lifecycle de runtime.
3. Validar em máquina Windows com `dotnet build` e scripts de governança.
