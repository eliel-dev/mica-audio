# Guia - Mudar configuracao do visualizador

## Objetivo

Explicar como uma alteracao de controle na UI vira mudanca real de analise/render.

## Politica atual

- A faixa de dB do visualizador e fixa para todas as visualizacoes.
- Referencia canonica:
  - `Min dB (fundo) = -85`
  - `Max dB (topo) = -25`
- A sensibilidade nao e mais configuravel pela UI.
- A selecao de preset agora acontece pela galeria visual em grid.
- Hover/foco em um card so anima a miniatura; apenas clique seleciona de fato.

## Passos

1. Selecione o preset clicando no card correspondente na galeria.
2. Ajuste o controle desejado na `MainPage` (`fft`, `boost`, escala, etc).
3. `MainPage` atualiza estado local e recria o analyzer com `CreateAnalyzer`.
4. `MainPage` persiste `appSettings` com `settingsDomainService.Copy`.
5. Valide persistencia reabrindo o app e confirmando que o mesmo card continua selecionado.

## Controles configuraveis

- `Linear Boost`
- `Quantidade de barras` (quando suportado pelo renderer ativo)
- `FFT Size`
- `FFT Smoothing`
- `Weighting Filter`
- `Escala de frequencia`
- `Faixa de frequencia`

## Exemplo de trilha real

- Card de preset em `MainPage.xaml` + controles restantes na lateral.
- Handler em `MainPage.xaml.cs`.
- Conversao final em `CreateAnalyzer`.
- Miniatura demo em `PresetPreviewThumbnailControl` usando `PresetPreviewSignalFactory` + `SpectrumAnalyzer` real com a mesma configuracao atual do visualizador.

## Referencias de codigo

- [MainPage.CreateAnalyzer](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1) - assinatura: `private IAnalyzer CreateAnalyzer(...)`
- [MainPage.OnFftSizeChanged](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1) - assinatura: `private void OnFftSizeChanged(...)`
- [MainPage.OnFrequencyScaleChanged](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1) - assinatura: `private void OnFrequencyScaleChanged(...)`
- [PresetPreviewSignalFactory](../../../src/App.WinUI/Services/Visualizer/PresetPreviewSignalFactory.cs#L1) - assinatura: `internal static class PresetPreviewSignalFactory`
- [VisualizerAnalyzerConfigFactory](../../../src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs#L1) - assinatura: `internal static class VisualizerAnalyzerConfigFactory`
- [PresetPreviewThumbnailControl](../../../src/App.WinUI/Views/Controls/PresetPreviewThumbnailControl.cs#L1) - assinatura: `internal sealed class PresetPreviewThumbnailControl`
- [AppSettingsDomainService](../../../src/App.WinUI/Services/AppSettingsDomainService.cs#L1) - assinatura: `internal sealed class AppSettingsDomainService`
- [AnalyzerConfig](../../../src/MicaAudio.Core/Config/AnalyzerConfig.cs#L1) - assinatura: `public sealed class AnalyzerConfig`

## Checklist rapido

- Config mudou visualmente em runtime.
- Config persistiu entre sessoes.
- Sem regressao de FPS perceptivel.



## Observacao sobre a galeria

- Alterar FFT Size, FFT Smoothing, Linear Boost, Weighting Filter, Escala de frequencia ou Faixa de frequencia afeta tambem as miniaturas da galeria.

