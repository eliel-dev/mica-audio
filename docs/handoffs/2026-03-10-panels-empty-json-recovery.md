# Handoff - Panels empty json recovery

## Objetivo

Eliminar o crash da sessao `Paineis` quando `%APPDATA%\\MicaAudio\\panels\\panels.json` estiver vazio ou corrompido, preservando a shell e reduzindo o risco de truncamento em saves futuros.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `PanelsStore` recupera JSON vazio/corrompido sem throw, `PanelsPage` nao propaga falha async no `OnLoaded` e as validacoes obrigatorias passam.

## Arquivos alterados

- src/App.WinUI/Services/Panels/PanelsStore.cs
- src/App.WinUI/Views/PanelsPage.xaml.cs
- tests/Integration.Smoke/PanelsStoreTests.cs
- docs/wiki/modules/paineis.md
- docs/wiki/reference/code-index.md

## Decisoes tomadas

1. O hotfix ficou restrito a `Paineis`; nao foi expandido para `settings.json` nem `apps/modifiers.json`.
2. `PanelsStore` passou a tratar arquivo ausente, `0 bytes`, whitespace-only, `JsonException`, `IOException` e `UnauthorizedAccessException` como estado recuperavel, retornando documento vazio normalizado.
3. JSON invalido nao-vazio agora e movido para `panels.json.corrupt-<timestamp>.json` antes da sessao continuar vazia.
4. O save de `PanelsStore` passou a usar `panels.json.tmp` + `File.Replace`/`File.Move`, mantendo `panels.json.bak` simples.
5. `PanelsPage.OnLoaded()` agora captura falhas, registra erro e deixa a pagina em fallback leve, sem virar `UnhandledException`.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1
dotnet build MicaAudio.sln -c Debug
dotnet test tests\Output.Tests\Output.Tests.csproj -c Debug --no-build
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-build
```

## Riscos e rollback

- Risco principal: algum problema real de acesso a disco ser mascarado como sessao vazia, exigindo olhar os logs para entender a causa.
- Como reverter: restaurar o `LoadCoreAsync`/`SaveAsync` anteriores de `PanelsStore` e remover o catch de fallback em `PanelsPage.OnLoaded()`.

## Proximos passos

1. Avaliar se o mesmo padrao de escrita atomica deve ser aplicado aos outros stores JSON do app.
2. Adicionar uma smoke/manual check automatizada para abrir a aba `Paineis` com `panels.json` zerado.
3. Se aparecerem muitos arquivos `corrupt-*`, adicionar UX leve para avisar o usuario e sugerir limpeza/backup.
