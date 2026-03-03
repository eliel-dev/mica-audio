# Modulo VisualWin2D

## Fluxo de execucao

1. renderers seguem CPU Win2D
2. preview HUB75 no app replica o snapshot `128x64` do simulador
3. presets builtin passam a ser calibrados tendo `128x64` como alvo principal

## Referencias de codigo

- [VisualizerEngine](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
