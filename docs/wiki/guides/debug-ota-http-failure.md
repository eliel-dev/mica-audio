# Guia - Debug OTA HTTP failure

## Objetivo

Diagnosticar falha de OTA quando status mostra erro HTTP no download do firmware.

## Passos

1. Confirmar host publico ativo no app (`ServerBaseAddress`).
2. Confirmar que `matrixportal-s3_merged.bin` existe na pasta exportada.
3. Verificar token do device e sessao OTA com TTL valido.
4. Testar endpoint `GET /api/v1/device/firmware/latest` e `download`.
5. Revisar logs do coordinator e do firmware para `ota-download` e `ota-failed`.
6. Repetir OTA apos refresh/reconnect do device.

## Referencias de codigo

- [DeviceOperationsCoordinator.StartOtaForDeviceAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L307) - assinatura: `Task<CommandDispatchResult> StartOtaForDeviceAsync(...)`
- [DeviceServerHost.Advanced.HandleFirmwareLatestAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L203) - assinatura: `IResult HandleFirmwareLatestAsync(HttpContext ctx)`
- [DeviceServerHost.Advanced.HandleFirmwareDownloadAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L244) - assinatura: `IResult HandleFirmwareDownloadAsync(HttpContext ctx)`
- [Firmware startOta](../../../firmware/matrixportal-s3/src/main.cpp#L233) - assinatura: `void startOta(const String& commandId)`

## Checklist rapido

- [ ] Endpoint latest retorna metadata valida.
- [ ] Endpoint download retorna binario para token/sessao validos.
- [ ] Device recebe progresso OTA > 0.
- [ ] Em falha, status final explicita erro e nao trava UI.