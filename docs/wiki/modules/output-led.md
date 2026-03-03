# Modulo Output LED

## Fluxo de execucao

1. receber `LedPayload.Frame128x64` para frame completo nativo
2. ou receber `LedPayload.Bins128` para barras
3. `MatrixPortalLedOutput` serializa `StreamFrameV2`
4. `SimulatorLedOutput` mantem snapshot local nativo `128x64`

## Referencias de codigo

- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [MatrixPortalLedOutput](../../../src/Output/Led/MatrixPortalLedOutput.cs#L1)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L1)
- [StreamFrameV2](../reference/ws-protocol-v2.md)
- [StreamFrameV1 legado](../reference/ws-protocol-v1.md)
