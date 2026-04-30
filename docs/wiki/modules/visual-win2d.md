# Modulo VisualWin2D

## Fluxo de execucao

1. renderers seguem CPU Win2D
2. o shipping mode do desktop resolve `HubTransportMode = Bins128` por default, deixando `Frame128x64` como infraestrutura preservada e nao como caminho normal
3. preview HUB75 no app replica o snapshot `128x64` do simulador
4. o preview WinUI continua refletindo o renderer completo, mesmo quando o HUB75 fisico esta no caminho `Bins128`
5. presets builtin continuam calibrados para leitura boa em `128x64`, mesmo aceitando divergencia entre preview e device no shipping mode atual

## Hub75 transport policy

- O modo de transporte continua resolvido por renderer via `RendererCapabilities.HubTransportMode`.
- No entanto, o shipping/default atual do desktop prioriza `Bins128`:
  - `RendererCapabilities.CreateLegacyAssumed()` agora assume `Bins128`;
  - renderers explicitos do catalogo shipping tambem foram alinhados para `Bins128`.
- `Frame128x64` continua implementado para cenarios forcados e para infraestrutura preservada, mas sai do caminho normal de execucao.

## Atualizacao 2026-03 - familias fisicas `Bins128` no HUB75

- O desktop deixou de tratar todos os presets `Bins128` como equivalentes no device.
- O runtime agora resolve `presetId + rendererId` para um conjunto pequeno de familias nativas no firmware:
  - `wave-mirror`
  - `mirror-lines`
  - `mirror-blocks`
  - `classic-bars`
  - `flow-line`
  - `history-scan`
  - `radial-orbit`
  - `atmosphere`
  - `launchpad-grid`
- Cada preset builtin continua com sua identidade forte no preview WinUI, mas o HUB75 passa a receber uma familia fisica reconhecivel em `Bins128`, em vez de colapsar tudo no mesmo visual legado.
- A paleta-base tambem vai no `flags` do pacote tipo `1`, com familias como `rainbow`, `sunset`, `arctic`, `neon`, `aurora`, `plasma` e `canonical`.

## AudioMotion Clone

- Continua com a mesma identidade visual no canvas principal.
- No shipping mode atual, o HUB75 volta a receber `Bins128`.
- O objetivo passa a ser throughput e estabilidade no device, aceitando divergencia em relacao ao preview completo.
- O preset builtin `audiomotion-clone` segue os defaults do renderer para a geometria-base do preview, em especial `heightScale = 0.78`, `lineThickness = 3` e `minHalfHeight = 0`, evitando drift entre o visual “clone” padrao e o codigo do renderer.

## Wave Mirror

- Recria a leitura do efeito antigo de ondas espelhadas no pipeline moderno do app, mas no shipping mode atual o device continua usando `Bins128`.
- Mantem uma linha central continua e luminosa, com envelope espelhado acima e abaixo do eixo horizontal.
- Usa arco-iris fixo por posicao horizontal como identidade propria do visual, sem depender da paleta do preset.
- Entra como preset builtin `spectrum-wave-mirror` e tambem fica disponivel no combo tecnico de renderers.

## Aurora Ribbon

- Visual de fitas largas com glow e bloom central.
- Foi calibrado para o HUB75 com formas largas, poucas curvas e leitura forte em `128x64`.

## Plasma Pulse

- Campo chunked por celulas, com warp e shockwave guiados por graves.
- Foi pensado para baixa resolucao, evitando detalhe fino demais e priorizando massa luminosa.

## Launchpad Grid

- Replica a linguagem de performance de um Launchpad: grade `8x8` apagada por padrao, botoes superiores/laterais e acentos fortes por grupos de pads.
- Usa `RendererBarCountMode.Fixed` com `64` pads, travando a UI no mesmo contrato visual do painel.
- Graves avancam linhas e blocos nas fileiras inferiores, medios disparam colunas e cruzes no miolo e agudos acendem taps rapidos nas linhas superiores e nos botoes de cena.
- O renderer mantem hold curto e decaimento rapido para parecer um sequenciador vivo, em vez de uma malha inteira respirando ao mesmo tempo.

## Referencias de codigo

- [VisualizerEngine](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L1)
- [RendererCapabilities](../../../src/Visual.Win2D/Engine/RendererCapabilities.cs#L1)
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L1)
- [WaveMirrorRenderer](../../../src/Visual.Win2D/Renderers/WaveMirrorRenderer.cs#L1)
- [AuroraRibbonRenderer](../../../src/Visual.Win2D/Renderers/AuroraRibbonRenderer.cs#L1)
- [LaunchpadGridRenderer](../../../src/Visual.Win2D/Renderers/LaunchpadGridRenderer.cs#L1)
- [PlasmaPulseRenderer](../../../src/Visual.Win2D/Renderers/PlasmaPulseRenderer.cs#L1)
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1)
- [Hub75VisualizerFrameRenderer](../../../src/App.WinUI/Services/Visualizer/Hub75VisualizerFrameRenderer.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
