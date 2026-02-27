# Guia - Adicionar novo renderer

## Objetivo

Adicionar renderer novo no Win2D e disponibilizar no app sem quebrar presets existentes.

## Passos

1. Criar classe em `src/Visual.Win2D/Renderers` implementando `IRenderer`.
2. Registrar renderer em `VisualizerEngine`.
3. Adicionar ID em `RendererIds`.
4. Criar preset builtin em `DefaultPresets`.
5. Validar migracao de presets em `PresetRepository` para nao apagar customizacoes.
6. Testar troca em runtime e fullscreen.
7. Se o renderer for pesado, implementar clamp de complexidade e modo de degradacao.

## Referencias de codigo

- [IRenderer](../../../src/Visual.Win2D/Engine/IRenderer.cs#L3) - assinatura: `public interface IRenderer`
- [VisualizerEngine ctor](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L14) - assinatura: `public VisualizerEngine()`
- [RendererIds](../../../src/Visual.Win2D/Engine/RendererIds.cs#L3) - assinatura: `public static class RendererIds`
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1) - assinatura: `internal static class DefaultPresets`
- [PresetRepository](../../../src/App.WinUI/Services/PresetRepository.cs#L1) - assinatura: `internal sealed class PresetRepository`
- [VizzyBlobNeonRenderer](../../../src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs#L1) - assinatura: exemplo de renderer novo
- [VizzyOrbitRingsRenderer](../../../src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs#L1) - assinatura: exemplo de renderer novo
- [VizzyHyperTunnelRenderer](../../../src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs#L1) - assinatura: exemplo de renderer com auto-qualidade

## Checklist rapido

- Renderer aparece na UI.
- Preset novo aparece sem resetar presets custom.
- Nao trava ao alternar preset.
- Nao quebra HUB75 preview.
- Em hardware mais fraco, renderer reduz custo sem stutter severo.
