# Handoff - Fase 6 / Onda 3 (integracao minima no App.WinUI)

## Objetivo

Tirar da `MainPage` a orquestracao tecnica central do pipeline e da persistencia de runtime, mantendo o code-behind como borda de integracao da UI sem abrir refactor visual amplo.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta onda:
  - introduzir `MainPage.Pipeline` como helper dedicado da borda de integracao;
  - remover da `MainPage` duplicacao de coercao e persistencia de settings do visualizer;
  - alinhar rebuild do analyzer com `VisualizerRuntimeSettings` e `AudioPipelineCoordinator.SetAnalyzer()`;
  - manter fluxo de audio/GIF e preview HUB75 consumindo o mesmo contrato central de payload.
- Fora desta onda:
  - redesign de XAML;
  - refactor pesado da `MainPage`;
  - mudancas em `DevicesPage` ou `Hub75VisualizerSessionService`.

## Arquivos alterados

- MainPage / integracao:
  - `src/App.WinUI/Views/MainPage.xaml.cs`
  - `src/App.WinUI/Views/MainPage.Pipeline.cs`
- Documentacao:
  - `docs/wiki/modules/settings-presets-persistence.md`
  - `docs/wiki/modules/app-winui.md`
  - `docs/wiki/modules/output-led.md`
  - `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `MainPage` continua dona da experiencia de tela, mas deixou de:
  - recalcular clamps/defaults do visualizer por conta propria;
  - persistir `AppSettings` campo a campo em cada handler;
  - trocar analyzer em runtime por fabrica solta.
- `MainPage.Pipeline` agora concentra:
  - `BuildVisualizerRuntimeSettings()`;
  - `ApplyVisualizerRuntimeSettings()`;
  - `PersistCurrentVisualizerSettings()`.
- `RebuildAnalyzer()` passou a reconstruir o analyzer via runtime profile central e entregar o resultado ao pipeline por `SetAnalyzer()`.
- O caminho GIF/HUB75 continua compativel, mas o app passou a reaproveitar `LedPayloadFactory` em vez de montar payload manualmente.

## Validacoes executadas

- Checkpoint integrado da fase 6:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
  - `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
- Resultado final do checkpoint integrado:
  - rebuild com `0 warnings`;
  - `229` testes aprovados;
  - `1` teste ignorado.

## Riscos e rollback

- Risco principal:
  - algum handler da `MainPage` ainda depender implicitamente de estado antigo nao persistido pelos novos helpers.
- Rollback:
  - reintegrar as rotinas de persistencia e coercao diretamente em `MainPage.xaml.cs`;
  - remover `MainPage.Pipeline.cs`;
  - restaurar a troca de analyzer anterior baseada em fabrica inline.

## Proximos passos

- Proxima fase estrutural natural:
  - lapidar `MicaAudio.Core` em torno de contratos de preset/settings restantes;
  - revisar complexidade residual da `MainPage` e de services de app nao tocados;
  - seguir para uma onda de polimento/testabilidade sem alterar UX.
