## Objetivo

Decompor `AppsPage` em blocos menores e independentes, preservando catalogo, preview GIF, drafts/modifiers e deploy sem regressao funcional.

## Escopo classificado

- Classificacao: estrutural
- Stack alvo: `.NET 10` / `C# 14`
- Limites mantidos:
  - sem alterar UX visivel da tela
  - sem mexer em `Device.Server`, `Device.Protocol`, firmware ou shell
  - sem abrir nova rodada de MVVM

## Arquivos alterados

- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.Catalog.cs`
- `src/App.WinUI/Views/AppsPage.RuntimePreview.cs`
- `src/App.WinUI/Views/AppsPage.Modifiers.cs`
- `src/App.WinUI/Views/AppsPage.Deployment.cs`
- `tests/Integration.Smoke/AppsPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/apps-catalog-deployment.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `AppsPage` passou a ser casca de composicao/lifecycle.
- Os fluxos foram separados por responsabilidade:
  - catalogo e selecao
  - runtime preview GIF
  - modifiers/drafts
  - install/save/log
- A decomposicao manteve as mesmas assinaturas privadas e o mesmo wiring de eventos da tela.
- Smoke tests novos foram adicionados para:
  - runtime GIF remoto vs. local
  - parsing de cidade
  - montagem de config JSON
  - mensagens de deploy

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
- `dotnet test MicaAudio.sln -c Debug`

## Riscos e rollback

- Risco principal: regressao no binding de catalogo ou no lifecycle do runtime GIF.
- Sinais de problema:
  - card sem selecao
  - preview GIF nao atualiza
  - `Install` / `Salvar` param de responder
  - modifiers deixam de refletir draft salvo
- Rollback seguro:
  - restaurar `AppsPage.xaml.cs` para a versao anterior
  - remover os partials desta onda

## Proximos passos

- Continuar a trilha de qualidade estrutural em services de app ainda concentrados.
- Se o proximo lote continuar em `App.WinUI`, priorizar presenters/helpers internos com testes diretos em vez de concentrar mais regra em code-behind.
