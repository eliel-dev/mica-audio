# Handoff - Hyper Tunnel renderer (Vizzy inspired)

## Objetivo

Adicionar uma nova visualizacao `Hyper Tunnel` no pipeline `Visual.Win2D`, com preset dedicado, qualidade automatica por FPS e sem quebrar presets customizados do usuario.

## Escopo classificado

- Estrutural (renderer engine + presets + testes + wiki/handoff).

## Arquivos alterados

- `src/Visual.Win2D/Engine/RendererIds.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/add-new-renderer.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. Implementacao em CPU Win2D com aproximacao 2D (sem raymarch completo).
2. Integracao aditiva: `Hyper Tunnel` sem substituir `Blob Neon` e `Orbit Rings`.
3. Paleta base laranja/ciano para aproximar o look de tunel neon.
4. Auto-qualidade por frame time com 3 niveis (`high/medium/low`).
5. Sem painel novo de parametros; controle por `RendererParameters` no preset.
6. `CurrentSchemaVersion` mantido para evitar sobrescrever defaults antigos alterados pelo usuario; preset novo e adicionado via merge nao destrutivo.

## Validacoes executadas

- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet build MicaAudio.sln -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer"`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`

## Riscos e rollback

- Risco: custo de frame alto em hardware fraco.
  - Mitigacao: auto-qualidade + clamps de `sliceCount`, `segmentCount` e `glowPasses`.
- Risco: diferenca de look em relacao ao shader original (por ser aproximacao 2D).
  - Mitigacao: manter parametros expostos por preset para tuning incremental.
- Rollback:
  1. remover ID `vizzy-hyper-tunnel`;
  2. remover registro no `VisualizerEngine`;
  3. remover preset `spectrum-vizzy-hyper-tunnel`;
  4. remover renderer `VizzyHyperTunnelRenderer` e referencias em docs/testes.

## Proximos passos

1. Ajustar preset `Hyper Tunnel` com variantes `Warm` e `Cold`.
2. Incluir medicao de frame-time medio por renderer na tela de debug.
3. Avaliar uma variante futura com deslocamento pseudo-3D mais agressivo.
