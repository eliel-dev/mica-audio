# Modulo Audio.Loopback

## Objetivo

Capturar audio do dispositivo de saida padrao via WASAPI loopback e entregar `PcmFrame` normalizado para o pipeline.

## Responsabilidades

- Inicializar/encerrar captura.
- Monitorar troca de device padrao e recuperar captura.
- Publicar frames em canal com politica `DropOldest` para evitar backlog.

## Fluxo de execucao

1. `StartAsync` cria canal e inicia captura low-latency.
2. Callback de dados converte formato para mono float.
3. `Frames` entrega `PcmFrame` para o coordinator.
4. `RestartCaptureAsync` recupera falhas de device.

## Pontos de alteracao frequente

- Latencia/buffer: `CaptureConfig.BufferMilliseconds`.
- Politica de canal: `BoundedChannelOptions`.
- Conversao PCM: `PcmConversion.cs`.

## Riscos e efeitos colaterais

- Buffer muito baixo aumenta risco de drop/glitch.
- Reinicio agressivo pode gerar status flap na UI.

## Checklist apos alteracao

- Reproduzir audio por 10 min sem travar.
- Trocar device de saida e validar recuperacao automatica.
- Confirmar ausencia de backlog infinito no canal.

## Referencias de codigo

- [ILoopbackCapture](../../../src/Audio.Loopback/Capture/ILoopbackCapture.cs#L6) - assinatura: `public interface ILoopbackCapture`
- [WasapiLoopbackCaptureService](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L10) - assinatura: `public sealed class WasapiLoopbackCaptureService`
- [StartAsync](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L36) - assinatura: `Task StartAsync(CaptureConfig, CancellationToken)`
- [RestartCaptureAsync](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L101) - assinatura: `Task RestartCaptureAsync(string reason)`
- [CaptureConfig](../../../src/Audio.Loopback/Capture/CaptureConfig.cs#L3) - assinatura: `public sealed class CaptureConfig`

## Backlinks no codigo

- `src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs`
