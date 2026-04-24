# 01 - System Overview

## Objetivo

Descrever o fluxo principal do sistema e onde cada modulo participa.

## Direcao oficial

- `server` = control plane + storage + catalogo + estado duravel.
- `cliente Windows` = edge client local e primeiro data plane LAN oficial.
- `ESP32` = runtime de execucao/render com ownership explicito por device.
- `visualizador` e `Paineis` sao oficialmente `client-driven`:
  - `visualizador`: captura/processamento local no cliente e envio direto ao ESP;
  - `Paineis`: sync/cache de assets/config no cliente e push local ao ESP.

## Baseline atual / transicao

- O app desktop ainda preserva caminhos embedded e transporte via server para compatibilidade.
- `Device.Server` ainda participa do fluxo operacional atual de pareamento, snapshots, comandos tracked e batches `WebP`.
- O hot path visual mediado por server continua existindo como baseline legado, nao como topologia alvo de baixa latencia.

## Pipeline principal oficial

```text
WASAPI loopback -> PcmFrame -> SpectrumAnalyzer -> SpectrumFrame -> VisualizerEngine -> Cliente LAN -> ESP32
```

## Fluxo por modulo

1. `Audio.Loopback` captura audio e publica `PcmFrame`.
2. `Analyzer.Dsp` transforma PCM em espectro e bandas.
3. `Visual.Win2D` renderiza no canvas a partir de `SpectrumFrame`.
4. `Output` serializa payload visual e o cliente decide se envia localmente ao ESP, ao simulador ou ao caminho legado.
5. `Device.Server` fica responsavel por control plane, assets, pairing, estado de device e catalogo.

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
