# 01 - System Overview

## Objetivo

Descrever o fluxo principal do sistema e onde cada modulo participa.

## Pipeline principal

```text
WASAPI loopback -> PcmFrame -> SpectrumAnalyzer -> SpectrumFrame -> VisualizerEngine -> ILedOutput
```

## Fluxo por modulo

1. `Audio.Loopback` captura audio e publica `PcmFrame`.
2. `Analyzer.Dsp` transforma PCM em espectro e bandas.
3. `Visual.Win2D` renderiza no canvas a partir de `SpectrumFrame`.
4. `Output` envia `bins64 + level` para simulador e servidor de dispositivos.
5. `Device.Server` distribui stream em WebSocket para firmware.

## Referencias de codigo

- [AudioPipelineCoordinator (classe)](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L10) - assinatura esperada: `internal sealed class AudioPipelineCoordinator`
- [AudioPipelineCoordinator.StartAsync](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L43) - assinatura esperada: `Task StartAsync(...)`
- [AudioPipelineCoordinator.PipelineLoopAsync](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L74) - assinatura esperada: `Task PipelineLoopAsync(...)`
- [SpectrumAnalyzer.Process](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L78) - assinatura esperada: `SpectrumFrame? Process(in PcmFrame frame)`
- [VisualizerEngine.Render](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L40) - assinatura esperada: `void Render(...)`
- [Esp32S3LedOutput.Send](../../../src/Output/Led/Esp32S3LedOutput.cs#L42) - assinatura esperada: `void Send(LedPayload payload)`

## Backlinks no codigo

Procure por `DOCS:` nestes arquivos:
- `src/App.WinUI/Services/AudioPipelineCoordinator.cs`
- `src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Output/Led/Esp32S3LedOutput.cs`
