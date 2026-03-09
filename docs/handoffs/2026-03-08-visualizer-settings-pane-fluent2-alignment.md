## Objetivo

Alinhar o menu de configuracao do `Visualizador` ao Fluent 2 ja usado pelo app, mantendo a metafora de painel lateral e sem reabrir risco de startup ou mexer no runtime do analyzer.

## Escopo classificado

- Classificacao: `estrutural`
- Area principal: `src/App.WinUI/Views/MainPage*`
- Fora de escopo mantido: `AudioPipelineCoordinator`, `ShellPage`, `DevicesPage`, `AppsPage`, `Device.Server`, `Device.Protocol` e firmware

## Arquivos alterados

- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/MainPage.Startup.cs`
- `src/App.WinUI/Views/MainPage.SettingsPane.cs`
- `src/App.WinUI/Views/MainPage.SettingsBindings.cs`
- `src/App.WinUI/Styles/Fluent2/Fluent2Controls.xaml`
- `tests/Integration.Smoke/MainPageStartupHelpersTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- O `SplitView` foi mantido como mecanismo tecnico da lateral, mas a pane foi redesenhada como settings surface Fluent 2.
- `RendererCombo` e `ContentModeCombo` sairam do `CommandBar` e passaram para o grupo `Renderizacao` dentro da pane.
- O `CommandBar` superior ficou reduzido a acoes rapidas do visualizador e a entrada explicita de `Configuracoes`.
- A logica da pane foi extraida para partials dedicados:
  - `MainPage.SettingsPane`
  - `MainPage.SettingsBindings`
- O runtime do analyzer permaneceu centralizado em `MainPage.VisualizerRuntime`, preservando debounce, apply consolidado e fallback seguro.
- O `ContentModeCombo` passou a carregar `Audio` e `GIF`, mas a linha so aparece quando o contexto de GIF faz sentido.

## Validacoes executadas

- `dotnet build .\src\App.WinUI\App.WinUI.csproj -c Debug --no-restore -m:1` -> OK
- `dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore -m:1 --filter MainPageStartupHelpersTests` -> OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1` -> OK
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1` -> OK
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1` -> OK
- Startup manual rapido de `src\App.WinUI\bin\x64\Debug\net10.0-windows10.0.22621.0\win-x64\App.WinUI.exe` -> processo permaneceu ativo por 5s (`PID 8196`)
- Observacao: o rebuild da solucao continua exibindo apenas warnings preexistentes e fora do escopo desta rodada em `src/App.WinUI/Views/DevicesPage.Ui.cs` (`CS0414` para `WizardStepOneBar` e `WizardStepTwoBar`).

## Riscos e rollback

- O principal risco e visual/composicional de XAML na `MainPage`, especialmente em resize e fullscreen.
- O fluxo de runtime foi preservado para reduzir risco funcional; a mudanca ficou concentrada na pane e nos bindings dela.
- Rollback seguro:
  - reverter `MainPage.xaml`, `MainPage.xaml.cs`, `MainPage.Startup.cs`, `MainPage.SettingsPane.cs`, `MainPage.SettingsBindings.cs`
  - reverter os estilos Fluent 2 adicionados em `Fluent2Controls.xaml`

## Proximos passos

- Validar visualmente a `MainPage` em janela normal e estreita:
  - abertura/fechamento da pane;
  - fullscreen;
  - visibilidade contextual do modo GIF;
  - leitura visual da pane como menu de configuracao, e nao como card operacional generico.
- Decidir em rodada separada se os warnings legados de `DevicesPage.Ui.cs` entram em limpeza fora do escopo do menu Fluent 2.
