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

- Firmware continua enviando telemetria a cada 2s.
- Presenca online depende de atividade recente (timeout atual de 15s).
- Snapshot exposto para UI contem ultimo visto e status.
- Lista operacional prioriza `Online`, mas mantem devices `Offline` visiveis para reduzir flapping e preservar o cadastro.

## Referencias de codigo

- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L37) - assinatura: `Task StartAsync(ServerConfig, CancellationToken)`
- [DeviceServerHost.GetDevicesSnapshot](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L156) - assinatura: `IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot()`
- [DeviceServerHost.Advanced handlers](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L63) - assinatura: `Task HandleIncomingWsTextAsync(...)`
- [RemoteDeviceServerClient.StartAsync](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L54) - assinatura: `Task StartAsync(CancellationToken)`
- [DeviceOperationsCoordinator.ApplyDevices path](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L160) - assinatura: `private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)`

## Backlinks no codigo

- `src/Device.Client.Remote/RemoteDeviceServerClient.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`

## Atualizacao 2026-03 - Lifecycle de Device Leve

- `DeviceStatus` continua representando conectividade operacional.
- `Pairing` e tratado apenas como compatibilidade legada e mapeado para `Aguardando provisionamento` na UI.
- Os thresholds de lifecycle agora sao configuraveis via `AppSettings`:
  - `DeviceFreshThresholdSeconds`
  - `DeviceStaleThresholdMinutes`
  - `DeviceDormantThresholdHours`
- A coercao de config garante `Fresh < Stale < Dormant`.
- O limiar de `Fresh` continua sendo a base do timeout operacional `Online -> Offline` no servidor.
- `Dormant` nao afirma perda de configuracao; ele apenas eleva o estado para `Configuracao incerta`.
