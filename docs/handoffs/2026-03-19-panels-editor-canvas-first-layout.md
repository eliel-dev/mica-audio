# Layout canvas-first no editor de Paineis

## Objetivo

Manter o editor da sessao `Paineis` em organizacao `canvas-first` mesmo em tela cheia, com o canvas HUB75 dominante na faixa superior e `Widgets` + `Widget`/configuracao abaixo.

## Escopo classificado

- Classificacao: `funcional`
- Escopo efetivo:
  - `src/App.WinUI/Views/PanelsPage.Ui.cs`
  - `src/App.WinUI/Views/PanelsPage.xaml.cs`
  - `tests/Integration.Smoke/PanelsPageSmokeTests.cs`
  - `docs/wiki/modules/paineis.md`

## Arquivos alterados

- `src/App.WinUI/Views/PanelsPage.Ui.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `tests/Integration.Smoke/PanelsPageSmokeTests.cs`
- `docs/wiki/modules/paineis.md`

## Decisoes tomadas

- O branch widescreen de tres colunas do editor foi removido.
- O editor agora trabalha com apenas dois modos adaptativos:
  - `CompactStacked` para `< 920px`
  - `CanvasFirstDesktop` para `>= 920px`
- No modo desktop, o canvas ocupa a linha superior com `ColumnSpan = 2` e linha `Star`.
- `Widgets` e `Widget`/configuracao permanecem na linha inferior, lado a lado.
- O card do canvas deixou de usar `StackPanel` e passou a usar `Grid` para permitir crescimento vertical real do `EditorCanvas`.
- O passo seguinte consolidou o redimensionamento vertical:
  - o helper puro de layout agora considera `width + height`;
  - a linha do canvas passou a ter peso maior que a base (`2*` vs `1*`) no desktop;
  - as panes inferiores ganharam altura maxima adaptativa e scroll interno para ceder antes de esconder o canvas;
  - os minimos do canvas foram harmonizados para a faixa de `300-320px`.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "PanelsPageSmokeTests|WinUiBootstrapSmokeTests"`
- `dotnet build MicaAudio.sln -c Debug -m:1`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "PanelsPageSmokeTests|WinUiBootstrapSmokeTests" --no-build`
- Launch verificado em `src/App.WinUI/bin/x64/Debug/net10.0-windows10.0.22621.0/win-x64/App.WinUI.exe` com `MainWindowTitle = WinUI Desktop` e `Responding = True`

## Riscos e rollback

- O layout inferior continua dependendo da altura minima do `ListView` de widgets; se a area inferior crescer demais em casos reais, o proximo ajuste deve ser de densidade/altura minima, nao de retorno ao layout lateral.
- O uso de cap adaptativo por altura pode exigir tuning fino em monitores muito baixos ou escalas de DPI incomuns.
- Rollback: restaurar o branch widescreen anterior em `PanelsPage.xaml.cs`, o `StackPanel` do `CanvasPane` em `PanelsPage.Ui.cs` e os minimos antigos do editor.

## Proximos passos

- Validar manualmente o editor maximizado em monitor widescreen real.
- Se necessario, ajustar apenas proporcoes da linha superior/inferior sem reintroduzir o layout de tres colunas.
