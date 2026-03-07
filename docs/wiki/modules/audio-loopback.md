# Modulo Audio.Loopback

## Objetivo

Capturar audio do dispositivo de saida padrao via WASAPI loopback e entregar `PcmFrame` normalizado para o pipeline.

## Responsabilidades

- Inicializar/encerrar captura.
- Monitorar troca de device padrao e recuperar captura.
- Publicar frames em canal com politica `DropOldest` para evitar backlog.

## Fluxo de execucao

1. `StartAsync` normaliza `CaptureConfig` via `LoopbackCaptureRuntimeConfig`, cria canal e inicia captura low-latency.
2. O callback de dados delega a criacao do `PcmFrame` para `LoopbackFrameFactory`.
3. `Frames` entrega `PcmFrame` para o coordinator com politica `DropOldest`.
4. `RestartCaptureAsync` recupera falhas de device sem mudar o contrato publico de captura.

## Pontos de alteracao frequente

- Latencia/buffer: `CaptureConfig.BufferMilliseconds`.
- Politica de canal: `BoundedChannelOptions`.
- Conversao PCM: `PcmConversion.cs`.
- Normalizacao de limites de runtime: `LoopbackCaptureRuntimeConfig`.
- Materializacao de frame e timestamp: `LoopbackFrameFactory`.

## Riscos e efeitos colaterais

- Buffer muito baixo aumenta risco de drop/glitch.
- Reinicio agressivo pode gerar status flap na UI.

## Checklist apos alteracao

- Reproduzir audio por 10 min sem travar.
- Trocar device de saida e validar recuperacao automatica.
- Confirmar ausencia de backlog infinito no canal.
- Validar `LoopbackCaptureRuntimeConfig` com configs extremas/invalidas.
- Validar `LoopbackFrameFactory` com PCM `float32` e `int16`.

## Referencias de codigo

- [ILoopbackCapture](../../../src/Audio.Loopback/Capture/ILoopbackCapture.cs#L6) - assinatura: `public interface ILoopbackCapture`
- [WasapiLoopbackCaptureService](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L10) - assinatura: `public sealed class WasapiLoopbackCaptureService`
- [StartAsync](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L36) - assinatura: `Task StartAsync(CaptureConfig, CancellationToken)`
- [RestartCaptureAsync](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L101) - assinatura: `Task RestartCaptureAsync(string reason)`
- [CaptureConfig](../../../src/Audio.Loopback/Capture/CaptureConfig.cs#L3) - assinatura: `public sealed class CaptureConfig`
- [LoopbackCaptureRuntimeConfig](../../../src/Audio.Loopback/Capture/LoopbackCaptureRuntimeConfig.cs#L1) - normalizacao interna de capacidade/buffer
- [LoopbackFrameFactory](../../../src/Audio.Loopback/Capture/LoopbackFrameFactory.cs#L1) - criacao interna de `PcmFrame`

## Backlinks no codigo

- `src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs`
