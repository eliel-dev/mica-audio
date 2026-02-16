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
2. Quando ha janela suficiente, roda analise FFT.
3. Agrega bandas de display/output.
4. Aplica smoothers independentes.
5. Retorna `SpectrumFrame`.

## Pontos de alteracao frequente

- Curvas de agregacao: `LogBandMapper`.
- Dinamica temporal: `EnvelopeSmoother`.
- Parametros de analise: `AnalyzerConfig`.

## Riscos e efeitos colaterais

- Quebrar regra `Bands64` causa divergencia entre preview e output remoto.
- Mudar escalas/normalizacao sem recalibrar presets muda UX drasticamente.

## Checklist apos alteracao

- Rodar `tests/Analyzer.Dsp.Tests`.
- Validar mudanca de escala (`Log/Mel/Bark`) visualmente.
- Confirmar `Bands64` continua coerente com espectro do frame.

## Referencias de codigo

- [IAnalyzer](../../../src/Analyzer.Dsp/Analysis/IAnalyzer.cs#L5) - assinatura: `public interface IAnalyzer`
- [SpectrumAnalyzer](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L9) - assinatura: `public sealed class SpectrumAnalyzer`
- [SpectrumAnalyzer.Process](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L78) - assinatura: `SpectrumFrame? Process(in PcmFrame frame)`
- [SpectrumAnalyzer.AnalyzeCurrentWindow](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L91) - assinatura: `SpectrumFrame AnalyzeCurrentWindow(long timestampQpc)`
- [LogBandMapper.CreateMode0Ranges](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L7) - assinatura: `Mode0BandLayout CreateMode0Ranges(...)`
- [LogBandMapper.CreateRanges](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L83) - assinatura: `BandRange[] CreateRanges(...)`
- [LogBandMapper.AggregateBandsPeak](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L215) - assinatura: `float[] AggregateBandsPeak(...)`
- [EnvelopeSmoother.Process](../../../src/Analyzer.Dsp/Analysis/EnvelopeSmoother.cs#L21) - assinatura: `float[] Process(float[] input)`
- [AnalyzerConfig](../../../src/MicaAudio.Core/Config/AnalyzerConfig.cs#L5) - assinatura: `public sealed class AnalyzerConfig`

## Backlinks no codigo

- `src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs`
