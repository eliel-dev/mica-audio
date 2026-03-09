# Modulo VisualWin2D

## Fluxo de execucao

1. renderers seguem CPU Win2D
2. renderers com `HubTransportMode = Frame128x64` sao renderizados offscreen em `128x64` e enviados ao HUB75 a 30 FPS
3. preview HUB75 no app replica o snapshot `128x64` do simulador
4. `AudioMotion Clone` pode continuar no caminho `Bins128` quando o throughput baixo e mais importante que a paridade visual
5. presets builtin passam a ser calibrados tendo `128x64` como alvo principal

## Hub75 frame transport

- O render offscreen usa um `VisualizerEngine` dedicado para evitar acoplar o estado do preview principal ao frame autoritativo do painel.
- O payload final para device/simulador segue `LedPayload.Frame128x64`, sem exigir mudanca de protocolo.
- O modo e resolvido por renderer via `RendererCapabilities.HubTransportMode`.

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
- [AuroraRibbonRenderer](../../../src/Visual.Win2D/Renderers/AuroraRibbonRenderer.cs#L1)
- [LaunchpadGridRenderer](../../../src/Visual.Win2D/Renderers/LaunchpadGridRenderer.cs#L1)
- [PlasmaPulseRenderer](../../../src/Visual.Win2D/Renderers/PlasmaPulseRenderer.cs#L1)
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1)
- [Hub75VisualizerFrameRenderer](../../../src/App.WinUI/Services/Visualizer/Hub75VisualizerFrameRenderer.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
