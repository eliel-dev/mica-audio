# Handoff - Simplificacao dos logs em Configuracoes

## Objetivo

Remover a UI rica de logs da `SettingsPage` e substituir por um atalho simples para abrir a pasta do `crash.log`, deixando esse arquivo como destino unico de erros persistidos.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `SettingsPage` reduzida a `Geral`, `AppLogStore` persistindo apenas `Error` em `crash.log`, build/testes sem regressao.

## Arquivos alterados

- `src/App.WinUI/Infrastructure/AppStartupDiagnostics.cs`
- `src/App.WinUI/Services/Logging/AppLogStore.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/ViewModels/SettingsPageViewModel.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `tests/Output.Tests/AppLogStoreTests.cs`
- `tests/Integration.Smoke/SettingsPageSmokeTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. `crash.log` em `%LocalAppData%\\MicaAudio` passou a ser o arquivo unico de erro do app, reaproveitando o caminho ja canonico de falhas/startup.
2. `AppLogStore` foi simplificado para memoria + persistencia seletiva:
   - `Error` grava em disco;
   - `Info` e `Warning` ficam so em memoria.
3. O arquivo legado `app-logs.json` deixou de ser usado, mas nao e apagado automaticamente.
4. A `SettingsPage` foi reduzida a uma unica superficie `Geral`, mantendo o toggle de Mica e adicionando um card `Logs de erro` com botao `Abrir pasta dos logs`.
5. O `SettingsPageViewModel` foi esvaziado e desacoplado do `AppLogStore`; a pagina nao depende mais dele nem de catalogo/log viewer.

## Validacoes executadas

```text
dotnet build .\src\App.WinUI\App.WinUI.csproj -c Debug --no-restore -m:1 -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore -m:1 --filter "AppLogStoreTests|AppSettingsDomainServiceTests|SettingsRepositoryTests" -> OK (19 aprovados)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore -m:1 --filter "SettingsPageSmokeTests|WinUiBootstrapSmokeTests" -> OK (10 aprovados)
```

## Riscos e rollback

- Risco principal: a simplificacao remove a navegacao lateral e o viewer de logs da `SettingsPage`; qualquer dependencia informal dessa UI deixa de existir.
- Como reverter:
  - restaurar `SettingsPage.xaml.cs` e `SettingsPageViewModel.cs` para o modelo com viewer;
  - restaurar `AppLogStore` para o contrato anterior com `app-logs.json`.

## Proximos passos

1. Rodar a validacao completa da solucao e subir a app para conferir o fluxo manual.
2. Validar manualmente o botao `Abrir pasta dos logs` e a escrita de um erro novo em `crash.log`.
