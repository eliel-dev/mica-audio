# Guia - Mudar configuracao do visualizador

## Objetivo

Explicar como uma alteracao de controle na UI vira mudanca real de analise/render.

## Passos

1. Localize o handler do controle na `MainPage`.
2. Atualize estado local (`sensitivity`, `fft`, escala, etc).
3. Recrie analyzer com `CreateAnalyzer`.
4. Atualize `appSettings` com `settingsDomainService.Copy`.
5. Valide persistencia reabrindo a app.

## Exemplo de trilha real

- UI slider/combo em `MainPage.xaml`.
- Handler em `MainPage.xaml.cs`.
- Conversao final em `CreateAnalyzer`.

## Referencias de codigo

- [MainPage.CreateAnalyzer](../../../src/App.WinUI/Views/MainPage.xaml.cs#L912) - assinatura: `private IAnalyzer CreateAnalyzer(PresetDefinition preset)`
- [MainPage.OnFftSizeChanged](../../../src/App.WinUI/Views/MainPage.xaml.cs#L712) - assinatura: `private void OnFftSizeChanged(...)`
- [MainPage.OnFrequencyScaleChanged](../../../src/App.WinUI/Views/MainPage.xaml.cs#L764) - assinatura: `private void OnFrequencyScaleChanged(...)`
- [AnalyzerConfig](../../../src/MicaAudio.Core/Config/AnalyzerConfig.cs#L5) - assinatura: `public sealed class AnalyzerConfig`

## Checklist rapido

- Config mudou visualmente em runtime.
- Config persistiu entre sessoes.
- Sem regressao de FPS perceptivel.
