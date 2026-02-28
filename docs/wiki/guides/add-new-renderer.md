# Guia - Adicionar novo renderer

## Objetivo

Adicionar renderer novo no Win2D e disponibilizar no app sem quebrar presets existentes.

## Politica oficial

1. O modulo visual e 2D-only.
2. Novos renderers devem ser compativeis com HUB75.
3. Nao usar `ComputeSharp`, shader GPU ou pipeline 3D/pseudo-3D.
4. O baseline de reatividade compartilhada continua em `ReactiveBandSampler`.

## Passos

1. Criar classe em `src/Visual.Win2D/Renderers` implementando `IRenderer`.
2. Implementar `IRendererCapabilitiesProvider` com `RendererCapabilities` explicitas.
3. Usar `ReactiveBandSampler` em vez de smoothing local ad-hoc.
4. Registrar renderer em `VisualizerEngine`.
5. Adicionar ID em `RendererIds`.
6. Criar preset builtin em `DefaultPresets`.
7. Validar migracao de presets em `PresetRepository` para nao apagar customizacoes.
8. Testar troca em runtime e fullscreen.
9. Preferir geometria vetorial simples (`CanvasPathBuilder`/`CanvasGeometry`) e clamps de complexidade quando o renderer for pesado.

## Referencias de codigo

- [IRenderer](../../../src/Visual.Win2D/Engine/IRenderer.cs#L3) - assinatura: `public interface IRenderer`
- [IRendererCapabilitiesProvider](../../../src/Visual.Win2D/Engine/IRendererCapabilitiesProvider.cs#L1) - assinatura: `public interface IRendererCapabilitiesProvider`
- [VisualizerEngine ctor](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L14) - assinatura: `public VisualizerEngine()`
- [RendererIds](../../../src/Visual.Win2D/Engine/RendererIds.cs#L3) - assinatura: `public static class RendererIds`
- [ReactiveBandSampler](../../../src/Visual.Win2D/Engine/ReactiveBandSampler.cs#L1) - assinatura: baseline de reatividade compartilhada
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1) - assinatura: `internal static class DefaultPresets`
- [PresetRepository](../../../src/App.WinUI/Services/PresetRepository.cs#L1) - assinatura: `internal sealed class PresetRepository`
- [VizzyBlobNeonRenderer](../../../src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs#L1) - assinatura: exemplo de renderer 2D decorativo
- [VizzyOrbitRingsRenderer](../../../src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs#L1) - assinatura: exemplo de renderer 2D decorativo
- [PolarArcsRenderer](../../../src/Visual.Win2D/Renderers/PolarArcsRenderer.cs#L1) - assinatura: exemplo de renderer 2D classico
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L1) - assinatura: referencia de reatividade forte

## Checklist rapido

- Renderer aparece na UI.
- Preset novo aparece sem resetar presets custom.
- Nao trava ao alternar preset.
- Nao quebra HUB75 preview.
- Nao depende de shader GPU ou `ComputeSharp`.
- Mantem boa legibilidade em resolucao baixa/HUB75.

## Requisitos obrigatorios para renderers reativos novos

1. Implementar `IRendererCapabilitiesProvider`.
2. Declarar `RendererCapabilities` explicitas, incluindo `BarCountMode`.
3. Usar `ReactiveBandSampler` em vez de smoothing local ad-hoc.
4. Adicionar teste de contrato do renderer.
5. Adicionar teste de reatividade deterministica do sampler.
6. Validar a integracao com o estado da lateral de configuracao em `MainPage`.
