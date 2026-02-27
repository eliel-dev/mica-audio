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

### `vizzy-hyper-tunnel`

- Tunel neon em profundidade com anelado procedural.
- Drift de eixo, deformacao radial por audio e camada de fog.
- Auto-qualidade por frame time para manter fluidez.
- Parametros principais:
  - `tunnelBaseRadius`
  - `tunnelDepth`
  - `tunnelSpeed`
  - `tunnelWarp`
  - `tunnelTwist`
  - `tunnelSliceCount`
  - `tunnelSegmentCount`
  - `tunnelGlowPasses`
  - `tunnelFogAmount`

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
- [VizzyHyperTunnelRenderer](../../../src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs#L1) - assinatura: `public sealed class VizzyHyperTunnelRenderer`
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1) - assinatura: `internal static class DefaultPresets`

## Backlinks no codigo

- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs`
