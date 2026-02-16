# 05 - Device Session and Reconnect

## Objetivo

Explicar ciclo de sessao dos dispositivos (pareamento, online, heartbeat, reconnect e expiracao de presenca).

## Ciclo de sessao

1. Device pareia via endpoint HTTP v1.
2. Servidor entrega credenciais e token.
3. Device conecta no WS e passa a receber stream/comandos.
4. Heartbeat/telemetria atualiza snapshot.
5. Em perda de conexao, servidor marca stale e depois offline.
6. Em reconnect, device volta para online sem recriar cadastro.

## Regras de estado

- Presenca online depende de atividade recente.
- Snapshot exposto para UI contem ultimo visto e status.
- Lista operacional prioriza `Online` com firmware identificado.

## Referencias de codigo

- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L37) - assinatura: `Task StartAsync(ServerConfig, CancellationToken)`
- [DeviceServerHost.GetDevicesSnapshot](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L156) - assinatura: `IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot()`
- [DeviceServerHost.Advanced handlers](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L63) - assinatura: `Task HandleIncomingWsTextAsync(...)`
- [DeviceIntegrationService.StartAsync](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L45) - assinatura: `Task StartAsync(CancellationToken)`
- [DeviceOperationsCoordinator.ApplyDevices path](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L160) - assinatura: `private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`