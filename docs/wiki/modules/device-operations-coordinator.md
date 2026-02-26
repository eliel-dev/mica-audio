# Modulo DeviceOperationsCoordinator

## Objetivo

Centralizar estado operacional da aba `Dispositivos`: refresh continuo, comandos tracked, progresso e logs.

## Responsabilidades

- Manter snapshot de dispositivos para UI.
- Controlar timer de refresh e diff de lista.
- Executar comandos tracked com timeout.
- Permitir concorrencia por dispositivo: 1 comando por device, em paralelo entre devices diferentes.
- Armazenar logs de operacao com limite.

## Fluxo de execucao

1. `SetDevicesPageVisible(true)` liga polling.
2. `RefreshDevicesAsync` coleta snapshot e publica eventos.
3. `RunCommandAsync` valida o device e reserva slot por `deviceId`.
4. Progresso chega por evento do host e atualiza `CommandByDevice`.
5. Conclusao remove o slot do device e publica estado final.

## Pontos de alteracao frequente

- `RefreshInterval` e `CommandTimeout`.
- Politica de filtro de dispositivos online.
- Mensagens de status e log por dispositivo.

## Riscos e efeitos colaterais

- Timer muito agressivo pode elevar CPU.
- Mudanca em timeout pode gerar falso timeout.
- Nao preservar estado ao navegar quebra UX.
- Bloqueio global acidental pode regredir concorrencia multi-device.

## Checklist apos alteracao

- Navegar entre paginas sem perder progresso/log.
- Confirmar update de lista sem clique manual.
- Executar comando simultaneo em 2 devices online.
- Confirmar que 2 comandos no mesmo device continuam bloqueados.

## Referencias de codigo

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1) - assinatura: `internal sealed class DeviceOperationsCoordinator : IDisposable`
- [DeviceOperationsState](../../../src/App.WinUI/Services/Devices/DeviceOperationsState.cs#L1) - assinatura: `internal sealed class DeviceOperationsState`
- [DeviceCommandExecutionState](../../../src/App.WinUI/Services/Devices/DeviceCommandExecutionState.cs#L1) - assinatura: `internal sealed class DeviceCommandExecutionState`
- [RunCommandAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L88) - assinatura: `public Task<CommandDispatchResult> RunCommandAsync(...)`
- [RefreshDevicesAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L301) - assinatura: `private async Task RefreshDevicesAsync(bool forcePublish)`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
