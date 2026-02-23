# Modulo Output (LED)

## Objetivo

Concentrar saidas do pipeline para simulador local e dispositivos remotos, mantendo contrato unico `ILedOutput`.

## Responsabilidades

- Receber `LedPayload` com `bins64 + level`.
- Receber opcionalmente `LedPayload.Frame64x32` para envio de frame completo HUB75.
- Aplicar brilho e enviar para destino.
- Disponibilizar fallback no-op.
- Atender tanto pipeline de audio quanto runtime de apps de catalogo (`gifhub75`).

## Fluxo de execucao

1. `AudioPipelineCoordinator` cria `LedPayload` por frame.
2. `GifCatalogAppRuntimeService` (AppsPage) tambem envia `LedPayload.Frame64x32` em `12 FPS`.
3. `MatrixPortalLedOutput` prioriza `Frame64x32` quando presente e serializa `StreamFrameV1` tipo `2` (RGB565).
4. Sem `Frame64x32`, o caminho legado continua via `StreamFrameV1` tipo `1` (`bins64`).
5. Ao parar runtime GIF, e enviado frame legado `bins64` zerado para forcar retorno imediato do firmware ao modo barras.

## Pontos de alteracao frequente

- Ajuste de brilho/clamp de envio.
- Estrategia de fan-out entre outputs.
- Simulador HUB75 e conversao de bins/frame em pixels.

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
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1) - assinatura: `internal sealed class GifCatalogAppRuntimeService`
- [StreamFrameV1.Create](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L11) - assinatura: `public static byte[] Create(...)`
- [StreamFrameV1.CreateFrame64x32Rgb565](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L43) - assinatura: `public static byte[] CreateFrame64x32Rgb565(...)`

## Backlinks no codigo

- `src/Output/Led/MatrixPortalLedOutput.cs`
