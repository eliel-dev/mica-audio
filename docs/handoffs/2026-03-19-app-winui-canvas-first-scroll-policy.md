# Handoff - 2026-03-19 - App WinUI canvas-first scroll policy

## Objetivo

Formalizar uma regra global de scroll para paginas WinUI com superficie primaria de trabalho e aplicar essa politica primeiro na `PanelsPage`, para que o canvas HUB75 permaneça visivel e dominante mesmo quando a janela nao estiver maximizada.

## Escopo classificado

- Tipo: `estrutural`
- Area principal: `App.WinUI`
- Politica adotada:
  - header e comandos ficam fixos;
  - o body da pagina passa a ser o dono padrao do scroll vertical;
  - a superficie primaria mantem minimo visivel;
  - scroll interno fica restrito a regioes secundarias delimitadas.

## Arquivos alterados

- `src/App.WinUI/Views/CanvasFirstPageScrollPolicy.cs`
- `src/App.WinUI/Views/PanelsPage.Ui.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `tests/Integration.Smoke/PanelsPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/paineis.md`

## Decisoes tomadas

- A regra global segue o guidance oficial de WinUI/Fluent para preferir scroll vertical quando o conteudo nao cabe no viewport, em vez de comprimir ate esconder a superficie principal.
- A `PanelsPage` passou a usar `ScrollViewer` no body do editor, mantendo header e status fora do scroll.
- O layout `canvas-first` foi preservado, mas a medicao vertical deixou de depender principalmente de rows em `*`.
- As panes inferiores continuam podendo rolar por dentro, mas agora ficam contidas como regioes secundarias e nao podem mais empurrar o canvas HUB75 para fora do viewport.

## Validacoes executadas

- `dotnet build MicaAudio.sln -c Debug -m:1`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "PanelsPageSmokeTests|WinUiBootstrapSmokeTests" --no-build`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`

## Riscos e rollback

- A politica nova muda a ownership do scroll no editor, entao qualquer codigo que presumisse medicao por `Grid` com rows em `*` pode precisar de ajuste fino se surgirem novas superficies canvas-first.
- O risco principal e visual: necessidade de refinar a altura preferida do canvas e o teto das panes secundarias apos validacao manual em tamanhos intermediarios de janela.
- Rollback simples:
  - remover `CanvasFirstPageScrollPolicy`;
  - voltar `PanelsPage` para grid direto sem `ScrollViewer` no body;
  - restaurar a politica anterior de distribuicao vertical.

## Proximos passos

- Validar manualmente a `PanelsPage` em janela media e estreita para confirmar:
  - canvas visivel no topo;
  - scroll vertical no body do editor;
  - widgets e inspector continuando abaixo do canvas;
  - sem clipping silencioso de elementos interativos.
- Se a politica se provar estavel, aplicar o mesmo helper a outras paginas editoriais futuras em vez de reinventar layout local por pagina.
