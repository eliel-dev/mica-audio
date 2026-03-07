# Modulo Analyzer.Dsp

## Objetivo

Transformar audio PCM em espectro util para visualizacao (`BandsDisplay`) e output (`Bands64`) com regras de smoothing e weighting.

## Responsabilidades

- FFT com janela Hann.
- Mapeamento de bandas (fixed e mode0).
- Smoothing temporal e weighting por bin.
- Reuso de buffers por instancia e janela PCM circular.
- Calculo de `Level` e producao de `SpectrumFrame`.

## Fluxo de execucao

1. `SpectrumAnalyzer.Process` acumula samples.
2. `SpectrumSampleWindow` garante append/slide/copia da janela de analise.
3. `SpectrumAnalyzer` escreve a janela diretamente no buffer da FFT, sem array intermediario.
4. `SpectrumPowerProcessor` executa FFT com plano cacheado, smoothing por bin, weighting e `Level`.
5. `SpectrumBandLayout` agrega bandas de display/output com pesos fracionarios precomputados.
6. `SpectrumAnalyzer` retorna `SpectrumFrame` mantendo o contrato publico original.

## Otimizacao 2026-03 - Fase DSP 1, buffers, planos e output-only

- O hot path do analyzer deixou de alocar buffers transitórios por frame:
  - `SpectrumAnalyzer` agora mantem buffers fixos por instancia para FFT, power spectrum e bandas raw.
  - `SpectrumSampleWindow` virou ring buffer e nao desloca mais memoria a cada hop.
  - `EnvelopeSmoother`, `LogBandMapper` e `FftUtility` ganharam caminhos baseados em `Span<T>`.
- A FFT complexa passou a usar `ComplexFftPlan`, com cache por `FftSize` de bit-reversal e twiddles por estagio.
- O projeto ganhou `RealFftFloatPlan` para validar paridade e preparar a trilha de FFT em `float` sem trocar o default ainda.
- `AnalyzerConfig` ganhou `AnalyzerOutputMode`:
  - `DisplayAndOutput` continua sendo o default;
  - `OutputOnly` pula bandas/geometry de display e preserva `Bands64` + `Level`.
- O baseline de performance passou a existir no `BenchmarkSuite1` com `SpectrumAnalyzerProcessBenchmark`.

## Pontos de alteracao frequente

- Curvas de agregacao: `LogBandMapper`.
- Dinamica temporal: `EnvelopeSmoother`.
- Parametros de analise: `AnalyzerConfig`.
- Janela/buffer interno: `SpectrumSampleWindow`.
- FFT + weighting + level: `SpectrumPowerProcessor`, `ComplexFftPlan` e `RealFftFloatPlan`.
- Layout de bandas e `Mode0`: `SpectrumBandLayout`.

## Riscos e efeitos colaterais

- Quebrar regra `Bands64` causa divergencia entre preview e output remoto.
- Mudar escalas/normalizacao sem recalibrar presets muda UX drasticamente.

## Checklist apos alteracao

- Rodar `tests/Analyzer.Dsp.Tests`.
- Validar mudanca de escala (`Log/Mel/Bark`) visualmente.
- Confirmar `Bands64` continua coerente com espectro do frame.
- Confirmar `OutputOnly` continua emitindo `Bands64` e `Level` sem bandas de display.
- Confirmar `SpectrumSampleWindow` preserva hop/window sem drift.
- Confirmar `SpectrumPowerProcessor` mantem `Level` e smoothing com o mesmo comportamento externo.
- Rodar o benchmark dedicado do analyzer para acompanhar `Mean` e `Allocated`.

## Referencias de codigo

- [IAnalyzer](../../../src/Analyzer.Dsp/Analysis/IAnalyzer.cs#L5) - assinatura: `public interface IAnalyzer`
- [SpectrumAnalyzer](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L9) - assinatura: `public sealed class SpectrumAnalyzer`
- [SpectrumAnalyzer.Process](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L78) - assinatura: `SpectrumFrame? Process(in PcmFrame frame)`
- [SpectrumAnalyzer.AnalyzeCurrentWindow](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L91) - assinatura: `SpectrumFrame AnalyzeCurrentWindow(long timestampQpc)`
- [SpectrumSampleWindow](../../../src/Analyzer.Dsp/Analysis/SpectrumSampleWindow.cs#L1) - helper interno da janela PCM
- [SpectrumPowerProcessor](../../../src/Analyzer.Dsp/Analysis/SpectrumPowerProcessor.cs#L1) - helper interno de FFT/power/weighting
- [SpectrumBandLayout](../../../src/Analyzer.Dsp/Analysis/SpectrumBandLayout.cs#L1) - helper interno de layout/agregacao
- [BandAggregationRange](../../../src/Analyzer.Dsp/Analysis/BandAggregationRange.cs#L1) - plano precomputado de pesos por banda
- [ComplexFftPlan](../../../src/Analyzer.Dsp/Math/ComplexFftPlan.cs#L1) - plano cacheado de FFT complexa
- [RealFftFloatPlan](../../../src/Analyzer.Dsp/Math/RealFftFloatPlan.cs#L1) - backend em float para paridade e benchmark
- [LogBandMapper.CreateMode0Ranges](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L7) - assinatura: `Mode0BandLayout CreateMode0Ranges(...)`
- [LogBandMapper.CreateRanges](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L83) - assinatura: `BandRange[] CreateRanges(...)`
- [LogBandMapper.AggregateBandsPeak](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L215) - assinatura: `float[] AggregateBandsPeak(...)`
- [EnvelopeSmoother.Process](../../../src/Analyzer.Dsp/Analysis/EnvelopeSmoother.cs#L21) - assinatura: `float[] Process(float[] input)`
- [AnalyzerConfig](../../../src/MicaAudio.Core/Config/AnalyzerConfig.cs#L5) - assinatura: `public sealed class AnalyzerConfig`
- [SpectrumAnalyzerProcessBenchmark](../../../BenchmarkSuite1/SpectrumAnalyzerProcessBenchmark.cs#L1) - benchmark dedicado de throughput e memoria

## Backlinks no codigo

- `src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs`
