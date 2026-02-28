# Handoff - 2026-02-28 - visual-2d-only-remove-hyper-tunnel-gpu

## Objetivo

Simplificar o modulo de visualizacao para um caminho oficial 2D-only em Win2D, removendo o Hyper Tunnel (shader GPU e pseudo-3D) do fluxo suportado.

## Escopo classificado

Estrutural.

## Arquivos alterados

- `src/Visual.Win2D/Engine/RendererIds.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Visual.Win2D.csproj`
- `src/Visual.Win2D/Shaders/HyperTunnelShadertoyShader.cs`
- `src/Visual.Win2D/Shaders/HyperTunnelAudioMapper.cs`
- `src/Visual.Win2D/Shaders/HyperTunnelShaderUniforms.cs`
- `src/Visual.Win2D/Renderers/VizzyHyperTunnelShaderRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `src/App.WinUI/Services/PresetRepository.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/add-new-renderer.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/adr/0008-visualizacoes-2d-only-para-hub75.md`
- `docs/adr/README.md`
- `scripts/validate-shader-toolchain.ps1`

## Decisoes tomadas

1. O modulo visual passa a ser oficialmente 2D-only.
2. Os IDs e registros de Hyper Tunnel foram removidos do engine.
3. O pipeline `ComputeSharp` foi aposentado do `Visual.Win2D`.
4. Presets legados com renderer Hyper Tunnel passam a migrar para `AudioMotion Clone`.
5. O estado salvo de renderer invalido na `MainPage` passa a ser normalizado e persistido no startup.
6. Como a politica do ambiente bloqueou a exclusao fisica de alguns arquivos rastreados, os arquivos do caminho Hyper Tunnel foram aposentados em conteudo (`tombstone`) e removidos do fluxo ativo.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build src/Visual.Win2D/Visual.Win2D.csproj -c Debug`
- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet build MicaAudio.sln -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer|FullyQualifiedName~Preset|FullyQualifiedName~RendererIntegration"`

## Riscos e rollback

- Presets antigos de Hyper Tunnel deixarao de manter a identidade visual anterior.
- Rollback: restaurar os IDs do Hyper Tunnel, recolocar o registro no engine, reativar os package references de `ComputeSharp` e desfazer a migracao em `PresetRepository`.

## Proximos passos

1. Se a simplificacao 2D-only se mostrar estavel, remover de vez os arquivos aposentados quando a politica do ambiente permitir deletar arquivos rastreados.
2. Revisar renderers 2D restantes para aumentar consistencia visual com HUB75, sem reintroduzir caminhos 3D/GPU.
