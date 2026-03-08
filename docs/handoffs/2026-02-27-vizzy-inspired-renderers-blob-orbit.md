# Handoff - Vizzy inspired renderers (Blob Neon + Orbit Rings)

## Objetivo

Adicionar 2 novas visualizacoes inspiradas no estilo do Vizzy (`Blob Neon` e `Orbit Rings`) no pipeline `Visual.Win2D`, com presets prontos e migracao nao destrutiva de presets.

## Escopo classificado

- Estrutural (`src/Visual.Win2D`, `src/App.WinUI/Services`, `tests/Integration.Smoke`, `docs/wiki`).

## Arquivos alterados

- `src/Visual.Win2D/Engine/RendererIds.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `src/App.WinUI/Services/PresetRepository.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/add-new-renderer.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. Escopo limitado a 2 renderers novos sem editor de camadas.
2. Parametrizacao via `RendererParameters` de preset (sem controles novos de UI).
3. Performance priorizada com clamps de `pointCount` e `glowPasses`.
4. Migracao de presets alterada para merge nao destrutivo:
   - adiciona defaults ausentes,
   - atualiza defaults desatualizados por `SchemaVersion`,
   - preserva presets customizados do usuario.

## Validacoes executadas

- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`

## Riscos e rollback

- Risco: efeitos glow podem reduzir FPS em hardware mais fraco.
  - Mitigacao: reduzir `blobPointCount/orbitPointCount` e `glowPasses` via preset.
- Risco: usuario com preset default antigo sobrescrito por schema novo.
  - Mitigacao: somente defaults com schema inferior sao substituidos; presets customizados permanecem.
- Rollback:
  1. remover IDs/registro dos renderers novos,
  2. remover presets `spectrum-vizzy-*`,
  3. retornar `PresetRepository` ao comportamento anterior.

## Proximos passos

1. Avaliar terceiro renderer inspirado em waveform circular com trilha.
2. Considerar painel opcional de ajuste rapido por preset (fase futura).
3. Adicionar benchmark de frame time por renderer em ambiente de teste manual.
