# Modulo Visual.Win2D

## Objetivo

Renderizar `SpectrumFrame` no canvas com renderer selecionado por preset.

## Responsabilidades

- Resolver renderer por ID.
- Atualizar pico/hold para efeitos visuais.
- Desenhar frame atual com palette/preset.
- Expor renderers plugaveis para presets sem alterar pipeline de audio.

## Fluxo de execucao

1. `MainPage.OnMainCanvasDraw` chama `VisualizerEngine.Render`.
2. Engine resolve renderer ativo via `preset.RendererId`.
3. Renderer desenha no `CanvasDrawingSession` com `RenderContext`.

## Renderers Vizzy inspirados

### `vizzy-blob-neon`

- Blob organico central com deformacao por bandas + LFO.
- Fill central com glow em multiplos passes.
- Parametros principais:
  - `blobBaseRadius`
  - `blobAudioDepth`
  - `blobLfoDepth`
  - `blobLfoSpeed`
  - `blobPointCount`
  - `blobGlowPasses`
  - `blobStrokeWidth`

### `vizzy-orbit-rings`

- Anel central + orbitas deformadas por audio.
- Fases diferentes por anel para movimento continuo.
- Parametros principais:
  - `orbitRingCount`
  - `orbitBaseRadius`
  - `orbitRingSpacing`
  - `orbitAudioDepth`
  - `orbitLfoDepth`
  - `orbitRotationSpeed`
  - `orbitPointCount`
  - `orbitGlowPasses`

### `vizzy-hyper-tunnel-shader`

- Caminho principal com shader GPU (ComputeSharp D2D1 + Win2D).
- Port do shader Hyper Tunnel com `hash12`, `noise_3`, `fbm`, `yC`, `map`, `trace` (sphere tracing) e steam volumetrico.
- Reatividade de audio via `HyperTunnelAudioMapper` (`bass/mid/high/level + band32/band128`).
- Auto-qualidade dinamica por frame time:
  - Alto: `iterations=100`, `steamSteps=24`, `scale=1.0`
  - Medio: `iterations=72`, `steamSteps=16`, `scale=0.75`
  - Baixo: `iterations=56`, `steamSteps=10`, `scale=0.5`
- Fallback automatico para `vizzy-hyper-tunnel` em falha de shader/device.

### `vizzy-hyper-tunnel`

- Renderer classico CPU com anelado procedural 2D.
- Mantido para fallback operacional quando shader GPU estiver indisponivel.

## Pontos de alteracao frequente

- Adicionar renderer novo em `Renderers/` + registro na engine.
- Ajustar `RenderContext` para novos parametros.
- Ajustar `DefaultPresets` para novos presets render-driven.

## Riscos e efeitos colaterais

- Efeitos pesados afetam FPS.
- Mudanca em renderer IDs quebra presets existentes.
- Parametros sem clamp podem explodir custo de frame.

## Checklist apos alteracao

- Trocar entre renderers em runtime sem crash.
- Medir frame-time com preset pesado.
- Validar fullscreen e resize.
- Validar fallback para renderer padrao quando ID nao existe.

## Referencias de codigo

- [VisualizerEngine](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L10) - assinatura: `public sealed class VisualizerEngine`
- [VisualizerEngine.Render](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L42) - assinatura: `void Render(...)`
- [IRenderer](../../../src/Visual.Win2D/Engine/IRenderer.cs#L3) - assinatura: `public interface IRenderer`
- [RendererIds](../../../src/Visual.Win2D/Engine/RendererIds.cs#L3) - assinatura: `public static class RendererIds`
- [VizzyBlobNeonRenderer](../../../src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs#L1) - assinatura: `public sealed class VizzyBlobNeonRenderer`
- [VizzyOrbitRingsRenderer](../../../src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs#L1) - assinatura: `public sealed class VizzyOrbitRingsRenderer`
- [VizzyHyperTunnelShaderRenderer](../../../src/Visual.Win2D/Renderers/VizzyHyperTunnelShaderRenderer.cs#L1) - assinatura: `public sealed class VizzyHyperTunnelShaderRenderer`
- [VizzyHyperTunnelRenderer](../../../src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs#L1) - assinatura: `public sealed class VizzyHyperTunnelRenderer`
- [HyperTunnelShadertoyShader](../../../src/Visual.Win2D/Shaders/HyperTunnelShadertoyShader.cs#L1) - assinatura: `internal readonly partial struct HyperTunnelShadertoyShader`
- [HyperTunnelAudioMapper](../../../src/Visual.Win2D/Shaders/HyperTunnelAudioMapper.cs#L1) - assinatura: `internal static class HyperTunnelAudioMapper`
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1) - assinatura: `internal static class DefaultPresets`

## Backlinks no codigo

- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyHyperTunnelShaderRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs`
- `src/Visual.Win2D/Shaders/HyperTunnelShadertoyShader.cs`
