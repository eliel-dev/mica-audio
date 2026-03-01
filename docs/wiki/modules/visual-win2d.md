# Modulo Visual.Win2D

## Objetivo

Renderizar `SpectrumFrame` no canvas com renderer selecionado por preset.

## Politica canonica

- O modulo visual e 2D-only.
- O caminho oficial e CPU/Win2D.
- Nao existe mais caminho suportado de shader GPU ou visualizacao 3D/pseudo-3D.
- Novas visualizacoes devem priorizar boa leitura em HUB75.
- Todas as visualizacoes usam faixa fixa de dB: -85 (fundo) e -25 (topo).

## Responsabilidades

- Resolver renderer por ID.
- Atualizar pico/hold para efeitos visuais.
- Desenhar frame atual com palette/preset.
- Expor renderers plugaveis para presets sem alterar pipeline de audio.

## Fluxo de execucao

1. `MainPage.OnMainCanvasDraw` chama `VisualizerEngine.Render`.
2. Engine resolve renderer ativo via `preset.RendererId`.
3. Renderer desenha no `CanvasDrawingSession` com `RenderContext`.

## Navegacao de presets

- O fluxo principal nao usa mais `PresetCombo`, galeria ou miniaturas.
- `MainPage` mantem uma ordem linear de todos os presets carregados pelo `PresetRepository`.
- `Left` seleciona o preset anterior e `Right` seleciona o proximo, com wrap-around.
- A troca mostra um HUD temporario sobre o canvas principal no formato `NN. Nome do preset`.
- O canvas principal continua sendo o unico preview real da visualizacao.

## Renderers 2D ativos

### AudioMotion Clone

- Renderer 2D de referencia para reatividade forte.
- Base canonica para presets legiveis em HUB75.
- Consome `BandsDisplay` diretamente, sem smoothing visual extra no renderer.
- Usa o envelope padrao do analyzer, alinhado ao branch main.

### `vizzy-blob-neon`

- Blob organico central com deformacao por bandas + LFO.
- Fill central com glow em multiplos passes.

### `vizzy-orbit-rings`

- Anel central + orbitas deformadas por audio.
- Fases diferentes por anel para movimento continuo.

### `polar-arcs`

- Renderer 2D classico em Win2D, sem shader GPU.
- Opera no modo apenas-barras, com arcos espelhados em torno do centro.
- Reatividade por bandas low->high derivadas de `SpectrumFrame.BandsDisplay`.

## Pontos de alteracao frequente

- Adicionar renderer novo em `Renderers/` + registro na engine.
- Ajustar `RenderContext` para novos parametros.
- Ajustar `DefaultPresets` para novos presets render-driven.
- Manter migracao de presets em `PresetRepository` quando um renderer for aposentado.

## Riscos e efeitos colaterais

- Efeitos pesados afetam FPS.
- Mudanca em renderer IDs quebra presets existentes se nao houver migracao.
- Parametros sem clamp podem explodir custo de frame.
- Remover seletores visuais aumenta dependencia de navegacao por teclado.

## Checklist apos alteracao

- Trocar entre renderers em runtime sem crash.
- Medir frame-time com preset pesado.
- Validar fullscreen e resize.
- Validar fallback para renderer padrao quando ID nao existe.
- Validar legibilidade em HUB75 preview.
- Validar `Left/Right` com o painel de configuracoes fechado.

## Referencias de codigo

- [VisualizerEngine](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L10) - assinatura: `public sealed class VisualizerEngine`
- [VisualizerEngine.Render](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L40) - assinatura: `void Render(...)`
- [IRenderer](../../../src/Visual.Win2D/Engine/IRenderer.cs#L3) - assinatura: `public interface IRenderer`
- [RendererIds](../../../src/Visual.Win2D/Engine/RendererIds.cs#L3) - assinatura: `public static class RendererIds`
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L1) - assinatura: `public sealed class AudioMotionCloneRenderer`
- [VizzyBlobNeonRenderer](../../../src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs#L1) - assinatura: `public sealed class VizzyBlobNeonRenderer`
- [VizzyOrbitRingsRenderer](../../../src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs#L1) - assinatura: `public sealed class VizzyOrbitRingsRenderer`
- [PolarArcsRenderer](../../../src/Visual.Win2D/Renderers/PolarArcsRenderer.cs#L1) - assinatura: `public sealed class PolarArcsRenderer`
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1) - assinatura: `internal static class DefaultPresets`
- [PresetRepository](../../../src/App.WinUI/Services/PresetRepository.cs#L1) - assinatura: `internal sealed class PresetRepository`
- [VisualizerAnalyzerConfigFactory](../../../src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs#L1) - assinatura: `internal static class VisualizerAnalyzerConfigFactory`
- [PresetNavigationHelper](../../../src/App.WinUI/Services/Visualizer/PresetNavigationHelper.cs#L1) - assinatura: `internal static class PresetNavigationHelper`
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1) - navegacao por teclado e HUD de presets

## Backlinks no codigo

- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs`
- `src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs`
- `src/Visual.Win2D/Renderers/PolarArcsRenderer.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `src/App.WinUI/Services/PresetRepository.cs`
- `src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs`
- `src/App.WinUI/Services/Visualizer/PresetNavigationHelper.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`

## Contrato de capacidades e reatividade compartilhada (iteracao 1)

- A bridge desta iteracao entra por `IRendererCapabilitiesProvider`, sem quebrar `IRenderer`.
- `VisualizerEngine.GetCapabilities(...)` expoe capacidades explicitas para renderers migrados e fallback `LegacyAssumed` para renderers legados.
- `ReactiveBandSampler` e `ReactiveEnvelopeState` concentram o baseline de reatividade (normalizacao, smoothing e metricas `Low/Mid/High/GlobalLevel`).
- `AudioMotionCloneRenderer` e `PolarArcsRenderer` sao os dois renderers de referencia migrados nesta fase.
- `AudioMotion Clone` marca `Quantidade de barras` como indisponivel no contrato atual, porque a geometria continua dependente da largura do layout.

## Referencias adicionais da bridge

- [IRendererCapabilitiesProvider](../../../src/Visual.Win2D/Engine/IRendererCapabilitiesProvider.cs#L1)
- [RendererCapabilities](../../../src/Visual.Win2D/Engine/RendererCapabilities.cs#L1)
- [ReactiveBandSampler](../../../src/Visual.Win2D/Engine/ReactiveBandSampler.cs#L1)
- [ReactiveEnvelopeState](../../../src/Visual.Win2D/Engine/ReactiveEnvelopeState.cs#L1)
