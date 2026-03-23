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
- O catalogo ativo do app foi simplificado em `2026-03-23`: `Aurora Ribbon`, `Blob Neon`, `Launchpad Grid`, `Plasma Pulse`, `Polar Arcs`, `Pulse Aura`, `Spectrogram` e `Waterfall` foram aposentados do runtime e deixaram de aparecer como presets ou renderers selecionaveis.

## Preview HUB75 fiel ao device

- O canvas principal do `Visualizador` continua sendo o preview artistico completo do renderer Win2D.
- O preview HUB75 inferior continua vindo do simulador local e segue o payload real enviado ao device.
- O Studio agora vive em `VisualizerStudioPage` e abre em modo `Painel HUB75` por padrao.
- O preview principal do Studio reaproveita o mesmo caminho shipping do `Visualizador`: `AudioPipelineFrameProcessor` resolve `Bins128` ou outro transporte suportado e entrega o payload ao `SimulatorLedOutput` compartilhado.
- `MainPage` e Studio passaram a reutilizar `Hub75PreviewHelper` para o desenho do frame no app, evitando drift visual entre as duas superficies.
- O modo `Canvas` do Studio continua disponivel como referencia secundaria e fiel do renderer Win2D artistico em edicao.
- O modo `Painel HUB75` do Studio deixa de ser um frame dedicado do working copy e passa a significar exatamente o mesmo preview HUB75 shipping do `Visualizador`.

## Studio de presets

- O fluxo de Studio opera sobre `PresetDefinition.Palette` e sobre o nome exibido de cada preset.
- O modo `Canvas` responde imediatamente a rename, add/remove/move de `PaletteStop` e aplicacao de gradientes rapidos.
- O modo `Painel HUB75` segue a mesma interpretacao shipping do `Visualizador`, inclusive quando a familia `Bins128` nao projeta fielmente um gradiente arbitrario do working copy.
- Built-ins continuam protegidos pela convencao `user-{presetId}` para conteudo visual.
- Rename de built-ins usa override local de display name, sem tocar no preset builtin original.
- `Salvar como novo` continua criando clones locais totalmente editaveis depois de salvos.

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

## Referencias de codigo

- [VisualizerEngine](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L1)
- [RendererCapabilities](../../../src/Visual.Win2D/Engine/RendererCapabilities.cs#L1)
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L1)
- [WaveMirrorRenderer](../../../src/Visual.Win2D/Renderers/WaveMirrorRenderer.cs#L1)
- [DefaultPresets](../../../src/App.WinUI/Services/DefaultPresets.cs#L1)
- [Hub75VisualizerFrameRenderer](../../../src/App.WinUI/Services/Visualizer/Hub75VisualizerFrameRenderer.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
