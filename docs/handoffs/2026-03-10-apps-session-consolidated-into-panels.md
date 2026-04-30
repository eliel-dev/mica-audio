# Handoff - consolidacao da sessao Apps em Paineis

## Objetivo

Remover a sessao `Apps` da experiencia ativa da app e consolidar descoberta, configuracao e ativacao de itens do catalogo no fluxo de `Paineis`/widgets.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: a shell nao exibe mais `Apps`, `Paineis` centraliza o catalogo de widgets, o bootstrap nao depende mais de `AppsPage`/deploy individual e a solucao continua validada por build/testes/governanca.

## Arquivos alterados

- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/ShellPageContentFactory.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `src/App.WinUI/Views/PanelsPage.Ui.cs`
- `src/App.WinUI/Views/Controls/AppCatalogCardControl.cs`
- `src/App.WinUI/Services/Panels/PanelsFrameComposer.cs`
- `tests/Integration.Smoke/ShellPageContentFactoryTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Integration.Smoke/PanelsPageSmokeTests.cs`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/apps-catalog-deployment.md`
- `docs/wiki/guides/configure-app-modifiers.md`
- `docs/wiki/guides/add-app-catalog-item.md`
- `docs/wiki/guides/troubleshoot-city-autocomplete.md`
- `docs/wiki/guides/load-gif-hub75.md`
- `docs/wiki/architecture/02-runtime-lifecycle.md`
- `docs/wiki/ai/app-module-pattern.md`
- `docs/wiki/README.md`

## Decisoes tomadas

1. A sessao `Apps` saiu da shell e do bootstrap ativo, mas os arquivos legados foram mantidos no repositorio e removidos da compilacao em `App.WinUI.csproj` para preservar links historicos da documentacao sem reativar o fluxo antigo.
2. `PanelsPage` passou a reutilizar `IAppCatalogService` e `IAppModifierStateStore`; o draft `__local__|appId` agora serve apenas como default inicial de widget, enquanto a configuracao real permanece por instancia dentro do painel.
3. A disponibilidade de widget HUB75 ficou centralizada em `PanelsFrameComposer.SupportsWidgetApp(...)`, evitando duplicacao entre UI e compositor.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-build -> OK
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --no-build -> OK
```

## Riscos e rollback

- Risco principal: algum documento ou arquivo legado ainda depender semanticamente do fluxo de deploy individual por app, apesar de ele nao existir mais na experiencia ativa.
- Como reverter: restaurar o item `apps` na shell, recolocar os registros de `AppsPage`/use cases em `App.xaml.cs` e remover os `Compile Remove` do projeto WinUI.

## Proximos passos

1. Migrar os itens restantes do catalogo para renderers HUB75 para ampliar a biblioteca utilizavel em `Paineis`.
2. Limpar telemetria e codigo legado residual do antigo fluxo de deploy individual quando a documentacao historica puder ser arquivada ou reindexada.
