# Handoff - Hyper Tunnel shader fix (GPU + fallback classic)

## Objetivo

Substituir o visual Hyper Tunnel 2D por um caminho shader GPU fiel (raymarch/SDF + fbm/steam), mantendo fallback classico sem crash.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: preset `Hyper Tunnel` usa renderer shader GPU; `Hyper Tunnel Classic` permanece funcional; fallback automatico em falha de shader.

## Arquivos alterados

- `src/Visual.Win2D/Visual.Win2D.csproj`
- `src/Visual.Win2D/Engine/RendererIds.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/VizzyHyperTunnelShaderRenderer.cs`
- `src/Visual.Win2D/Shaders/HyperTunnelShadertoyShader.cs`
- `src/Visual.Win2D/Shaders/HyperTunnelAudioMapper.cs`
- `src/Visual.Win2D/Shaders/HyperTunnelShaderUniforms.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `tests/Integration.Smoke/Integration.Smoke.csproj`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `scripts/validate-shader-toolchain.ps1`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/add-new-renderer.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. `ComputeSharp.D2D1.WinUI` foi adotado no `Visual.Win2D` para executar raymarch na GPU e manter integracao com Win2D.
2. O renderer classico `vizzy-hyper-tunnel` foi preservado como fallback operacional para falhas de shader/device.
3. Foi criado um preset novo `spectrum-vizzy-hyper-tunnel-shader` (`Hyper Tunnel`) e o preset existente foi mantido como `Hyper Tunnel Classic`.
4. O target framework dos projetos de UI/smoke foi alinhado para `net8.0-windows10.0.22621.0`, necessario para consumo do pacote WinUI do ComputeSharp.
5. Auto-qualidade dinamica foi aplicada no renderer shader (tiers alto/medio/baixo) para proteger fluidez.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\validate-shader-toolchain.ps1 -> OK

dotnet build src/Visual.Win2D/Visual.Win2D.csproj -c Debug -> OK

dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK

dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer" --no-restore -> OK (6 testes)
```

## Riscos e rollback

- Risco principal: regressao visual/performance em GPUs antigas ao usar shader de raymarch.
- Como reverter:
  1. alterar preset ativo para `Hyper Tunnel Classic`;
  2. remover registro de `VizzyHyperTunnelShaderRenderer` em `VisualizerEngine`;
  3. remover preset `spectrum-vizzy-hyper-tunnel-shader` em `DefaultPresets`.

## Proximos passos

1. Ajustar a curva visual do shader (fov/steam/color grading) com testes manuais no seu hardware alvo.
2. Adicionar teste dedicado de determinismo para `HyperTunnelAudioMapper` no projeto de testes de render.
3. Se necessario, reduzir warnings de analise relacionados a descarte no renderer shader (`CA1001`).
