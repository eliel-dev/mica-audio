# Modulo Analyzer.Dsp

## Objetivo

Transformar audio PCM em espectro util para visualizacao (`BandsDisplay`) e output (`Bands64`) com regras de smoothing e weighting.

## Responsabilidades

- FFT com janela Hann.
- Mapeamento de bandas (fixed e mode0).
- Smoothing temporal e weighting por bin.
- Calculo de `Level` e producao de `SpectrumFrame`.

## Fluxo de execucao

1. `SpectrumAnalyzer.Process` acumula samples.
2. `SpectrumSampleWindow` garante append/slide/copia da janela de analise.
3. `SpectrumPowerProcessor` executa FFT, smoothing por bin, weighting e `Level`.
4. `SpectrumBandLayout` agrega bandas de display/output com o layout normalizado.
5. `SpectrumAnalyzer` retorna `SpectrumFrame` mantendo o contrato publico original.

## Pontos de alteracao frequente

- Curvas de agregacao: `LogBandMapper`.
- Dinamica temporal: `EnvelopeSmoother`.
- Parametros de analise: `AnalyzerConfig`.
- Janela/buffer interno: `SpectrumSampleWindow`.
- FFT + weighting + level: `SpectrumPowerProcessor`.
- Layout de bandas e `Mode0`: `SpectrumBandLayout`.

## Riscos e efeitos colaterais

- Quebrar regra `Bands64` causa divergencia entre preview e output remoto.
- Mudar escalas/normalizacao sem recalibrar presets muda UX drasticamente.

## Checklist apos alteracao

- Rodar `tests/Analyzer.Dsp.Tests`.
- Validar mudanca de escala (`Log/Mel/Bark`) visualmente.
- Confirmar `Bands64` continua coerente com espectro do frame.
- Confirmar `SpectrumSampleWindow` preserva hop/window sem drift.
- Confirmar `SpectrumPowerProcessor` mantem `Level` e smoothing com o mesmo comportamento externo.

## Referencias de codigo

- [IAnalyzer](../../../src/Analyzer.Dsp/Analysis/IAnalyzer.cs#L5) - assinatura: `public interface IAnalyzer`
- [SpectrumAnalyzer](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L9) - assinatura: `public sealed class SpectrumAnalyzer`
- [SpectrumAnalyzer.Process](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L78) - assinatura: `SpectrumFrame? Process(in PcmFrame frame)`
- [SpectrumAnalyzer.AnalyzeCurrentWindow](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L91) - assinatura: `SpectrumFrame AnalyzeCurrentWindow(long timestampQpc)`
- [SpectrumSampleWindow](../../../src/Analyzer.Dsp/Analysis/SpectrumSampleWindow.cs#L1) - helper interno da janela PCM
- [SpectrumPowerProcessor](../../../src/Analyzer.Dsp/Analysis/SpectrumPowerProcessor.cs#L1) - helper interno de FFT/power/weighting
- [SpectrumBandLayout](../../../src/Analyzer.Dsp/Analysis/SpectrumBandLayout.cs#L1) - helper interno de layout/agregacao
- [LogBandMapper.CreateMode0Ranges](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L7) - assinatura: `Mode0BandLayout CreateMode0Ranges(...)`
- [LogBandMapper.CreateRanges](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L83) - assinatura: `BandRange[] CreateRanges(...)`
- [LogBandMapper.AggregateBandsPeak](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L215) - assinatura: `float[] AggregateBandsPeak(...)`
- [EnvelopeSmoother.Process](../../../src/Analyzer.Dsp/Analysis/EnvelopeSmoother.cs#L21) - assinatura: `float[] Process(float[] input)`
- [AnalyzerConfig](../../../src/MicaAudio.Core/Config/AnalyzerConfig.cs#L5) - assinatura: `public sealed class AnalyzerConfig`

## Backlinks no codigo

- `src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs`
