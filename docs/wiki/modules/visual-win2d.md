# Modulo Visual.Win2D

## Objetivo

Renderizar `SpectrumFrame` no canvas com renderer selecionado por preset.

## Responsabilidades

- Resolver renderer por ID.
- Atualizar pico/hold para efeitos visuais.
- Desenhar frame atual com palette/preset.

## Fluxo de execucao

1. `MainPage.OnMainCanvasDraw` chama `VisualizerEngine.Render`.
2. Engine resolve renderer ativo.
3. Renderer desenha no `CanvasDrawingSession`.

## Pontos de alteracao frequente

- Adicionar renderer novo em `Renderers/` + registro na engine.
- Ajustar `RenderContext` para novos parametros.
- Ajustar `AudioMotionCloneRenderer` para paridade visual.

## Riscos e efeitos colaterais

- Efeitos pesados afetam FPS.
- Mudanca em renderer IDs quebra presets existentes.

## Checklist apos alteracao

- Trocar entre renderers em runtime sem crash.
- Medir frame-time com preset pesado.
- Validar fullscreen e resize.

## Referencias de codigo

- [VisualizerEngine](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L10) - assinatura: `public sealed class VisualizerEngine`
- [VisualizerEngine.Render](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L40) - assinatura: `void Render(...)`
- [IRenderer](../../../src/Visual.Win2D/Engine/IRenderer.cs#L3) - assinatura: `public interface IRenderer`
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L5) - assinatura: `public sealed class AudioMotionCloneRenderer`
- [AudioMotionCloneRenderer.Render](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L11) - assinatura: `void Render(RenderContext context)`
- [RendererIds](../../../src/Visual.Win2D/Engine/RendererIds.cs#L3) - assinatura: `public static class RendererIds`

## Backlinks no codigo

- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
