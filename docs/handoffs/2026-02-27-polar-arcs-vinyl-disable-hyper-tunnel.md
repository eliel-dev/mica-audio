# Handoff - Polar Arcs estilo vinil + desativacao do Hyper Tunnel builtin

## Objetivo

Ajustar o preset `Polar Arcs` para um layout inspirado em vinil, mas alinhado ao estilo dos presets do visualizador (paleta do preset + arcos espelhados esquerda/direita), e remover os presets builtin de `Hyper Tunnel` do catalogo carregado para evitar travamentos em VM sem GPU dedicada.

## Escopo classificado

- Tipo: funcional
- Criterio de aceite: `Polar Arcs` deve seguir a composicao inspirada em vinil, mas com comportamento visual coerente com presets como `AudioMotion Clone`, e os presets builtin de `Hyper Tunnel` nao devem mais aparecer no `PresetCombo`.

## Arquivos alterados

- `src/Visual.Win2D/Renderers/PolarArcsRenderer.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `src/App.WinUI/Services/PresetRepository.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/handoffs/2026-02-27-polar-arcs-vinyl-disable-hyper-tunnel.md`

## Decisoes tomadas

1. `Polar Arcs` continua CPU/Win2D classico, sem shader GPU, e passa a seguir uma composicao inspirada em vinil, mas usando a paleta do preset e guias coloridas no estilo dos outros renderizadores.
2. Os renderers de Hyper Tunnel permanecem no codigo para fallback tecnico e testes, mas seus presets builtin deixam de ser semeados e presets legados com os mesmos IDs sao podados na carga.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
dotnet build src/Visual.Win2D/Visual.Win2D.csproj -c Debug -> OK
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer" -> OK
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-build -> OK
```

## Riscos e rollback

- Risco principal: usuarios que dependiam manualmente dos presets builtin de Hyper Tunnel deixam de ve-los no combo, e qualquer preset local com o mesmo ID builtin tambem sera removido por seguranca operacional.
- Como reverter: recolocar os presets `spectrum-vizzy-hyper-tunnel*` em `DefaultPresets.Create()` e remover a poda em `PresetRepository.DisabledBuiltInPresetIds`.

## Proximos passos

1. Ajustar apenas `polarArcsMaxSweepDegrees`, `polarArcsBarsStart`, `polarArcsBarsEnd`, `polarArcsJitter` e a paleta do preset se o visual ainda estiver distante da referencia desejada.
2. Reavaliar a volta do Hyper Tunnel builtin quando houver deteccao confiavel de GPU/ambiente suportado.

