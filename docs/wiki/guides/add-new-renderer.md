# Guia - Adicionar novo renderer

## Objetivo

Adicionar renderer novo no Win2D e disponibilizar no app sem quebrar presets existentes.

## Passos

1. Criar classe em `src/Visual.Win2D/Renderers` implementando `IRenderer`.
2. Registrar renderer em `VisualizerEngine`.
3. Adicionar ID em `RendererIds`.
4. (Opcional) criar preset builtin em `DefaultPresets`.
5. Testar troca em runtime e fullscreen.

## Referencias de codigo

- [IRenderer](../../../src/Visual.Win2D/Engine/IRenderer.cs#L3) - assinatura: `public interface IRenderer`
- [VisualizerEngine ctor](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L14) - assinatura: `public VisualizerEngine()`
- [RendererIds](../../../src/Visual.Win2D/Engine/RendererIds.cs#L3) - assinatura: `public static class RendererIds`
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L5) - assinatura: exemplo de renderer existente

## Checklist rapido

- Renderer aparece na UI.
- Nao trava ao alternar preset.
- Nao quebra HUB75 preview.
