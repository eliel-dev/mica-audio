# Handoff - startup App.WinUI estabilizado

## Objetivo

Corrigir a regressao de startup da `App.WinUI` apos a fase 6, garantindo observabilidade real (`crash.log` sempre gerado), bootstrap previsivel da `MainPage` e isolamento de falha da `MainPage` dentro da `ShellPage`.

## Escopo classificado

- Classificacao: estrutural curta com impacto funcional no startup da `App.WinUI`.
- Escopo desta rodada:
  - garantir `crash.log` em arquivo + logger;
  - registrar breadcrumbs de startup;
  - trocar a `ShellPage` para resolucao lazy/cached das abas;
  - proteger a hidratacao da `MainPage` contra reentrancia de handlers;
  - normalizar presets legados/parciais antes do rebuild do analyzer.
- Fora desta rodada:
  - firmware/protocolo;
  - mudancas em `Device.Server` ou `Device.Protocol`;
  - redesign de XAML;
  - reset automatico de `%AppData%\MicaAudio`.

## Arquivos alterados

- Startup e diagnostico:
  - `src/App.WinUI/App.xaml.cs`
  - `src/App.WinUI/Infrastructure/AppStartupDiagnostics.cs`
  - `src/App.WinUI/Views/AppFailureViewFactory.cs`
- Shell e resolucao lazy:
  - `src/App.WinUI/Views/ShellPage.xaml.cs`
  - `src/App.WinUI/Views/ShellPageContentFactory.cs`
- Bootstrap seguro da MainPage:
  - `src/App.WinUI/Views/MainPage.xaml.cs`
  - `src/App.WinUI/Views/MainPage.Startup.cs`
  - `src/App.WinUI/Views/MainPage.Pipeline.cs`
  - `src/App.WinUI/Views/MainPage.Hub75DeviceSession.cs`
  - `src/App.WinUI/Views/MainPageUiBootstrapGuard.cs`
- Testes:
  - `tests/Integration.Smoke/AppStartupDiagnosticsTests.cs`
  - `tests/Integration.Smoke/MainPageStartupHelpersTests.cs`
  - `tests/Integration.Smoke/ShellPageContentFactoryTests.cs`
  - `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- Documentacao:
  - `docs/wiki/modules/app-winui.md`
  - `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `App.WriteCrashLog()` agora sempre escreve arquivo local e logger em paralelo; a disponibilidade de `ILogger<App>` nao bloqueia mais o `crash.log`.
- O startup ganhou breadcrumbs canonicos para os pontos criticos:
  - `BuildServiceProvider`
  - `Resolve ShellPage`
  - `Resolve MainPage`
  - `MainPage.InitializeAsync`
  - `MainPage.RebuildAnalyzer`
  - `MainPage.ActivateVisualizerSessionAsync`
- `ShellPage` deixou de depender de paginas prontas; agora resolve cada aba sob demanda via `ShellPageContentFactory`.
- Falha na `MainPage` nao deve mais derrubar a shell inteira:
  - a excecao e logada;
  - o `ContentFrame` recebe fallback local;
  - a navegacao para `Devices`/`Apps` continua possivel.
- A `MainPage` agora hidrata a UI inteira sob `MainPageUiBootstrapGuard`, impedindo:
  - persistencia durante `SelectionChanged`/`Toggled` programaticos;
  - sincronizacao HUB75 durante hidratacao;
  - rebuild/sync redundante disparado por eventos de controle.
- Presets legados/parciais passaram a cair em fallback seguro antes do rebuild do analyzer.
- `RebuildAnalyzer()` agora tenta fallback builtin seguro e preserva analyzer anterior se o rebuild falhar.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
- `dotnet test MicaAudio.sln -c Debug`
- Validacao manual prevista apos build:
  - abrir a app com o `settings.json` real do usuario;
  - confirmar que a shell sobe mesmo se a aba `Visualizador` falhar;
  - confirmar que `crash.log` e criado/atualizado no caminho exibido.

## Riscos e rollback

- Risco principal:
  - ainda existir algum estado legado em `settings/presets` que nao passe pelo novo caminho de normalizacao e force uma falha diferente na `MainPage`.
- Rollback:
  - restaurar a injecao eager de paginas na `ShellPage`;
  - remover `ShellPageContentFactory` e `MainPageUiBootstrapGuard`;
  - voltar o rebuild do analyzer para o caminho inline anterior;
  - remover `AppStartupDiagnostics` e retornar ao logging anterior.

## Proximos passos

- Executar validacao manual com o `%AppData%\MicaAudio` real e revisar o novo `crash.log` se houver nova falha.
- Se o startup estabilizar, seguir para uma fase de lapidacao do `App.WinUI` focada em reduzir complexidade residual da `MainPage`.
- Se ainda houver crash em startup, usar os breadcrumbs do `crash.log` para isolar o ponto exato antes de abrir qualquer ajuste em firmware/protocolo.
