# 06 - Errors, Timeouts and Recovery

## Objetivo

Consolidar estrategia de erro, timeout e recuperacao para visualizador e camada de dispositivos.

## Taxonomia de falhas

1. Erro de ambiente (runtime/politica de seguranca).
2. Erro de rede (host inacessivel, timeout de comando, offline).
3. Erro de arquivo local (firmware pre-compilado ausente, falha de escrita).
4. Erro de protocolo (comando invalido, payload inconsistente).

## Politica de timeout

- Comandos tracked: timeout curto (resposta rapida de UX).
- UI sempre mostra estado terminal (`success`, `timeout`, `failed`).

## Recuperacao recomendada

- Em timeout: manter app aberto e permitir retry.
- Em reconnect: ressincronizar snapshot e continuar stream.
- Em erro de salvamento local: preservar log e permitir novo destino.

## Referencias de codigo

- [DeviceOperationsCoordinator.CommandTimeout](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1) - assinatura: `private static readonly TimeSpan CommandTimeout`
- [DeviceOperationsCoordinator.BuildFinalCommandStatus](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1) - assinatura: `private static string BuildFinalCommandStatus(CommandDispatchResult result)`
- [PrecompiledFirmwareService.CopyToAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1) - assinatura: `Task CopyToAsync(string optionId, string destinationPath, CancellationToken)`
- [App startup crash capture](../../../src/App.WinUI/App.xaml.cs#L1) - assinatura: `protected override void OnLaunched(...)`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
