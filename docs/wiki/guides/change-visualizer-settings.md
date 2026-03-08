# Guia - Mudar configuracao do visualizador

## Objetivo

Explicar como uma alteracao de controle na UI vira mudanca real de analise/render.

## Politica atual

- A faixa de dB do visualizador e fixa para todas as visualizacoes.
- Referencia canonica:
  - `Min dB (fundo) = -85`
  - `Max dB (topo) = -25`
- A sensibilidade nao e mais configuravel pela UI.
- Nao existe mais lista ou galeria visivel de presets.
- A troca de preset acontece por teclado:
  - `Left` = preset anterior
  - `Right` = proximo preset
- A cada troca, o visualizador mostra um HUD curto no topo no formato `NN. Nome do preset`.

## Passos

1. Garanta que o painel de configuracoes esteja fechado.
2. Use `Left` ou `Right` para trocar o preset atual.
3. Ajuste o controle desejado na `MainPage` (escala, barras, smoothing, etc).
4. `MainPage` atualiza estado local e recria o analyzer com `CreateAnalyzer`.
5. `MainPage` persiste `appSettings` com `settingsDomainService.Copy`.
6. Valide persistencia reabrindo o app e confirmando que o ultimo preset continua ativo.

## Controles configuraveis

- `Quantidade de barras` (quando suportado pelo renderer ativo)
- `FFT Smoothing`
- `Weighting Filter`
- `Escala de frequencia`
- `Faixa de frequencia`

## Exemplo de trilha real

- Aceleradores de teclado em `MainPage.xaml`.
- Handler em `MainPage.xaml.cs`.
- Conversao final em `CreateAnalyzer`, com `FFT Size` canonico fixo em `2048`.
- HUD temporario sobre o canvas com numero + nome do preset atual.

## Referencias de codigo

- [MainPage.CreateAnalyzer](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1) - assinatura: `private IAnalyzer CreateAnalyzer(...)`
- [MainPage.OnFrequencyScaleChanged](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1) - assinatura: `private void OnFrequencyScaleChanged(...)`
- [VisualizerAnalyzerConfigFactory](../../../src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs#L1) - assinatura: `internal static class VisualizerAnalyzerConfigFactory`
- [PresetNavigationHelper](../../../src/App.WinUI/Services/Visualizer/PresetNavigationHelper.cs#L1) - assinatura: `internal static class PresetNavigationHelper`
- [AppSettingsDomainService](../../../src/App.WinUI/Services/AppSettingsDomainService.cs#L1) - assinatura: `internal sealed class AppSettingsDomainService`
- [AnalyzerConfig](../../../src/MicaAudio.Core/Config/AnalyzerConfig.cs#L1) - assinatura: `public sealed class AnalyzerConfig`

## Checklist rapido

- Config mudou visualmente em runtime.
- Config persistiu entre sessoes.
- `Left/Right` muda o preset com o painel fechado.
- Sem regressao de FPS perceptivel.
