# 03 - Data Contracts

## Objetivo

Consolidar os contratos que conectam modulos e nao devem quebrar sem migracao explicita.

## Contratos centrais

### Audio
- `PcmFrame`
- `SpectrumFrame`
- `IAnalyzer`

### Output
- `LedPayload`
- `ILedOutput`

### Device protocol
- `DeviceCommandRequest`
- `DeviceCommandProgressMessage`
- `StreamFrameV1`

## Regra critica

`Bands64` deve ser derivado do mesmo espectro do frame (sem segunda FFT).

## Referencias de codigo

- [PcmFrame](../../../src/MicaAudio.Core/Audio/PcmFrame.cs#L3) - assinatura esperada: `public sealed class PcmFrame`
- [SpectrumFrame](../../../src/MicaAudio.Core/Audio/SpectrumFrame.cs#L3) - assinatura esperada: `public sealed class SpectrumFrame`
- [IAnalyzer](../../../src/Analyzer.Dsp/Analysis/IAnalyzer.cs#L5) - assinatura esperada: `public interface IAnalyzer`
- [ILedOutput](../../../src/Output/Led/ILedOutput.cs#L6) - assinatura esperada: `public interface ILedOutput`
- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L5) - assinatura esperada: `public sealed class LedPayload`
- [DeviceCommandRequest](../../../src/Device.Protocol/Models/DeviceCommandRequest.cs#L3) - assinatura esperada: `public sealed class DeviceCommandRequest`
- [DeviceCommandProgressMessage](../../../src/Device.Protocol/Models/DeviceCommandProgressMessage.cs#L3) - assinatura esperada: `public sealed class DeviceCommandProgressMessage`
- [StreamFrameV1.Create](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L11) - assinatura esperada: `public static byte[] Create(...)`

## Backlinks no codigo

Os contratos acima devem conter referencias via paginas de modulo/guia quando houver mudanca.
