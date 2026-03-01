# 2026-02-28 - Visualizer Keyboard-Only Preset Switching

## Objetivo

Remover a galeria de presets com miniaturas e simplificar o fluxo do Visualizador para navegacao por teclado (`Left`/`Right`) com HUD temporario mostrando numero + nome do preset atual.

## Escopo classificado

- remover `PresetGalleryPanel` e toda a infraestrutura de miniaturas/preview
- introduzir ordem linear de presets em `MainPage`
- trocar presets com wrap-around via keyboard accelerators
- exibir HUD temporario sobre o canvas principal
- limpar testes e docs que dependiam da galeria

## Arquivos alterados

- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs`
- `src/App.WinUI/Services/Visualizer/PresetNavigationHelper.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/PresetNavigationHelperTests.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/change-visualizer-settings.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Arquivos removidos

- `src/App.WinUI/Views/Controls/PresetGalleryCardControl.cs`
- `src/App.WinUI/Views/Controls/PresetPreviewThumbnailControl.cs`
- `src/App.WinUI/Services/Visualizer/PresetPreviewSignalFactory.cs`
- `src/App.WinUI/Services/Visualizer/PresetPreviewSettingsSnapshot.cs`

## Decisoes tomadas

1. Nenhuma lista visivel de presets permanece na UI.
2. A navegacao principal de presets agora e somente por teclado.
3. A ordem e alfabetica por `Name`, com wrap-around.
4. O HUD mostra `NN. Nome do preset` por 1200ms.
5. O painel de configuracoes aberto bloqueia a navegacao por setas para evitar conflito com sliders e combos.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer|FullyQualifiedName~WinUiBootstrap"`
- `dotnet build MicaAudio.sln -c Debug`

## Riscos e rollback

- O usuario perde qualquer affordance visual para descoberta de presets.
- A navegacao depende de foco e estado do painel lateral; conflitos de teclado precisam permanecer bloqueados corretamente.
- Rollback: reverter o commit desta entrega para restaurar a galeria e a infraestrutura de miniaturas.

## Rollback

Reverter o commit desta entrega para restaurar a galeria e a infraestrutura de miniaturas.

## Proximos passos

- Se necessario, ajustar somente a apresentacao do HUD (contraste, duracao ou tipografia) sem reintroduzir lista ou miniaturas.

