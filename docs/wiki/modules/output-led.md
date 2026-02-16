# Modulo Output (LED)

## Objetivo

Concentrar saidas do pipeline para simulador local e dispositivos remotos, mantendo contrato unico `ILedOutput`.

## Responsabilidades

- Receber `LedPayload` com `bins64 + level`.
- Aplicar brilho e enviar para destino.
- Disponibilizar fallback no-op.

## Fluxo de execucao

1. `AudioPipelineCoordinator` cria `LedPayload` por frame.
2. Envia para `MatrixPortalLedOutput` e opcionalmente `SimulatorLedOutput`.
3. `MatrixPortalLedOutput` serializa `StreamFrameV1` e usa `DeviceServerHost.BroadcastFrame`.

## Pontos de alteracao frequente

- Ajuste de brilho/clamp de envio.
- Estrategia de fan-out entre outputs.
- Simulador HUB75 e conversao de bins em pixels.

## Riscos e efeitos colaterais

- Divergencia entre simulador e stream remoto se `bins64` nao for usado de forma consistente.
- Mudanca de payload pode quebrar firmware.

## Checklist apos alteracao

- Rodar `tests/Output.Tests`.
- Validar HUB75 preview local.
- Validar stream para device conectado.

## Referencias de codigo

- [ILedOutput](../../../src/Output/Led/ILedOutput.cs#L6) - assinatura: `public interface ILedOutput`
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L6) - assinatura: `public sealed class SimulatorLedOutput`
- [SimulatorLedOutput.Send](../../../src/Output/Led/SimulatorLedOutput.cs#L30) - assinatura: `void Send(LedPayload payload)`
- [MatrixPortalLedOutput](../../../src/Output/Led/MatrixPortalLedOutput.cs#L9) - assinatura: `public sealed class MatrixPortalLedOutput`
- [MatrixPortalLedOutput.Send](../../../src/Output/Led/MatrixPortalLedOutput.cs#L42) - assinatura: `void Send(LedPayload payload)`
- [StreamFrameV1.Create](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L11) - assinatura: `public static byte[] Create(...)`

## Backlinks no codigo

- `src/Output/Led/MatrixPortalLedOutput.cs`
