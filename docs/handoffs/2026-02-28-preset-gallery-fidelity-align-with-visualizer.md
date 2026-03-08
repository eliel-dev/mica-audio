# Handoff - 2026-02-28 - preset-gallery-fidelity-align-with-visualizer

## Objetivo`r`n`r`nAlinhar a miniatura da galeria de presets ao mesmo pipeline real do visualizador, eliminando o `SpectrumFrame` sintetico direto e passando a usar `PcmFrame` sintetico + `SpectrumAnalyzer` real.`r`n`r`n## Escopo classificado

- Classificacao: estrutural.
- Area: `App.WinUI` (galeria de presets / preview), `Analyzer.Dsp` (reuso via configuracao), `tests`, `docs`.
- Objetivo: tornar a miniatura da galeria fiel ao visualizador real usando o mesmo pipeline (`PcmFrame` sintetico -> `SpectrumAnalyzer` -> `SpectrumFrame` -> `VisualizerEngine`).

## O que mudou

1. `PresetPreviewSignalFactory` substituiu o antigo factory de `SpectrumFrame` direto e agora gera `PcmFrame` sintetico deterministico.
2. `PresetPreviewThumbnailControl` passou a manter um `SpectrumAnalyzer` real por card, com warm-up e reaproveitamento de estado durante hover.
3. Foi criado `PresetPreviewSettingsSnapshot` para capturar a configuracao global relevante da UI.
4. Foi criado `VisualizerAnalyzerConfigFactory` para compartilhar a mesma montagem de `AnalyzerConfig` entre o canvas principal e as miniaturas.
5. `MainPage` passou a reconstruir o analyzer principal e a propagar o snapshot da galeria pelo mesmo fluxo de configuracao.
6. Os testes foram atualizados para validar a fonte PCM, a configuracao compartilhada e a geometria estrutural do preview.

## Arquivos alterados

- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/Controls/PresetPreviewThumbnailControl.cs`
- `src/App.WinUI/Views/Controls/PresetGalleryCardControl.cs`
- `src/App.WinUI/Services/Visualizer/PresetPreviewSignalFactory.cs`
- `src/App.WinUI/Services/Visualizer/PresetPreviewSettingsSnapshot.cs`
- `src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/PresetPreviewSignalFactoryTests.cs`
- `tests/Output.Tests/PresetPreviewPipelineTests.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/change-visualizer-settings.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

- O preview continua sem audio ao vivo.
- A fonte continua sintetica, mas agora no dominio PCM.
- O preview usa o mesmo pipeline do visualizador em vez de fabricar `SpectrumFrame` final.
- Apenas um card continua animando por vez.
- O hover nao altera o canvas principal; apenas clique seleciona o preset.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer|FullyQualifiedName~WinUiBootstrap"`

## Riscos e rollback

- Risco principal: custo extra por miniatura ao reconstruir analyzer quando largura/configuracao mudam.
- Mitigacao: apenas um card anima por vez; o analyzer do card e reaproveitado durante o hover.
- Rollback: reverter este commit e restaurar o fluxo anterior (`SpectrumFrame` sintetico direto).

## Proximos passos

1. Validar visualmente se `AudioMotion Clone` e os renderers abstratos ficaram realmente fieis em miniatura.
2. Se necessario, ajustar warm-up e limite de hops por draw para equilibrar fidelidade e custo.


## Ajuste posterior

O primeiro port da galeria para o pipeline real ainda concentrava energia sintetica em graves/medios, o que fazia o `AudioMotion Clone` parecer cortado e acender so no lado esquerdo. O sinal demo foi ajustado para distribuir componentes ao longo de toda a faixa `FrequencyMinHz..FrequencyMaxHz`, preservando determinismo por preset e tornando a miniatura mais fiel a largura espectral real.

## Ajuste posterior

O primeiro port da galeria para o pipeline real ainda concentrava energia sintetica em graves/medios, o que fazia o `AudioMotion Clone` parecer cortado e acender so no lado esquerdo. O sinal demo foi ajustado para distribuir componentes ao longo de toda a faixa `FrequencyMinHz..FrequencyMaxHz`, preservando determinismo por preset e tornando a miniatura mais fiel a largura espectral real.

## Tuning de velocidade

A taxa do preview em hover foi ajustada para 36 FPS e a modulacao do sinal demo foi acelerada para deixar as miniaturas mais vivas e reativas, sem alterar o pipeline principal do visualizador.

## Ajuste de densidade da galeria

Os presets extras ja estavam sendo carregados, mas o `GridView` usava `ItemsWrapGrid` sem `ItemWidth` e `ItemHeight`, o que fazia o layout parecer limitado a poucos cards. A galeria passou a usar celulas fixas para expor mais presets visiveis no mesmo painel.

## Ajuste de scroll da galeria

A galeria ainda parecia limitada a 4 cards porque o GridView continuava forçando 2 linhas com scroll horizontal. O layout passou a usar mais altura visivel, wrapping sem limite fixo de linhas e scroll vertical, o que permite navegar pelo restante dos presets sem manter a UI presa em duas linhas.
