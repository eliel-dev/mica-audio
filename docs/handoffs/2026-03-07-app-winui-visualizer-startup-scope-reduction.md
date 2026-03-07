# Handoff - reducao de escopo do startup do Visualizador

## Objetivo

Reduzir o raio de mudanca do fix de startup para o core do visualizador, mantendo o app sem crash e evitando endurecimento desnecessario fora da `MainPage`.

## Escopo classificado

- Classificacao: estrutural curta com foco funcional.
- Escopo desta rodada:
  - manter `crash.log` sempre em arquivo;
  - manter a shell viva quando o visualizador falhar;
  - simplificar o runtime seguro do analyzer para atuar apenas na `MainPage`;
  - parar de normalizar a colecao inteira de presets no load.
- Fora desta rodada:
  - refactor em `AudioPipelineCoordinator`;
  - mudanca de `Device.Server`, `Device.Protocol`, `DevicesPage` ou onboarding;
  - redesign da `ShellPage` ou nova arquitetura de DI.

## Arquivos alterados

- Runtime do visualizador:
  - `src/App.WinUI/Views/MainPage.xaml.cs`
  - `src/App.WinUI/Views/MainPage.Startup.cs`
- Testes:
  - `tests/Integration.Smoke/MainPageStartupHelpersTests.cs`
- Documentacao:
  - `docs/wiki/modules/app-winui.md`
  - `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- A carga inicial de presets voltou a ser minimalista:
  - presets com `PresetId` vazio sao ignorados;
  - a colecao nao e mais normalizada inteira no startup.
- A sanitizacao ficou restrita ao caminho que realmente precisa de defesa:
  - `BuildSafeAnalyzerPreset()` agora e a unica barreira de preset seguro para o rebuild do analyzer.
- O fallback continua sendo o builtin `audiomotion-clone`, mas apenas quando:
  - o preset ativo vier invalido para runtime;
  - o renderer configurado nao existir;
  - `RendererParameters` ou `Palette` vierem ausentes.
- `App` e `ShellPage` foram congelados no minimo necessario:
  - `crash.log` obrigatorio;
  - breadcrumbs;
  - shell resiliente com fallback isolado na aba.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
- Validacao manual de startup:
  - `App.WinUI.exe` sobe;
  - `crash.log` nao recebe nova entrada de falha da aba `visualizer`.

## Riscos e rollback

- Risco principal:
  - ainda existir algum preset legado quebrado em campos fora do runtime do analyzer e isso afetar a UX sem derrubar a app.
- Rollback:
  - restaurar a normalizacao ampla em `MainPage.Startup`;
  - voltar o caminho anterior de preset seguro em `InitializeAsync`, `ResolveActivePreset` e `SelectPreset`.

## Proximos passos

- Validar manualmente com o `settings.json` real do usuario e navegacao repetida para a aba `Visualizador`.
- Se surgir novo crash, manter a investigacao local na `MainPage` e no rebuild do analyzer.
- Nao abrir nova frente estrutural fora do visualizador ate estabilizar o startup com dados reais do usuario.
