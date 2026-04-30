# 01 - System Overview

## Objetivo

Descrever o fluxo principal do sistema e onde cada modulo participa.

## Direcao oficial

- `server` = control plane + storage + catalogo + estado duravel + relay visual LAN.
- `cliente Windows` = controlador/fonte local de dados, incluindo captura de audio.
- `ESP32` = runtime de execucao/render com ownership explicito por device.
- `visualizador` e `Paineis` usam o server como fronteira oficial para o ESP:
  - `visualizador`: captura/processamento local no cliente, envio ao server e UDP visual servidor->ESP;
  - `Paineis`: WinUI edita/ativa, o server persiste assets/config e compoe widgets `server` em runtime autonomo.

## Baseline atual / transicao

- O app desktop preserva caminhos embedded e remote usando a mesma fronteira `IDeviceFrameTransport` para visualizador e compatibilidade local.
- `Device.Server` ainda participa do fluxo operacional atual de pareamento, snapshots, comandos tracked e batches `WebP`.
- O hot path visual remoto mediado por server e a topologia alvo atual de baixa latencia, com UDP apenas no trecho servidor->ESP.
- Em modo Remote, paineis server-owned continuam depois que o WinUI fecha enquanto o `MicaAudio.Server` permanecer ligado.

## Pipeline principal oficial

```text
WASAPI loopback -> PcmFrame -> SpectrumAnalyzer -> SpectrumFrame -> VisualizerEngine -> WinUI -> Server -> ESP32
```

## Fluxo por modulo

1. `Audio.Loopback` captura audio e publica `PcmFrame`.
2. `Analyzer.Dsp` transforma PCM em espectro e bandas.
3. `Visual.Win2D` renderiza no canvas a partir de `SpectrumFrame`.
4. `Output` serializa payload visual e o cliente envia ao simulador local ou ao servidor.
5. `Device.Server` fica responsavel por control plane, assets, pairing, estado de device, catalogo, runtime autonomo de paineis server-owned e entrega visual ao ESP.

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
