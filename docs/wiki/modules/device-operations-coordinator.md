# Modulo DeviceOperationsCoordinator

## Objetivo

Centralizar estado operacional da pagina Dispositivos e Servidor: refresh continuo, comandos tracked, progresso, logs e build/export.

## Responsabilidades

- Manter snapshot de dispositivos para UI.
- Controlar timer de refresh e diff de lista.
- Executar comandos tracked com timeout.
- Executar build/export com progresso em tempo real.
- Armazenar logs de operacao com limite.

## Fluxo de execucao

1. `SetDevicesPageVisible(true)` liga polling.
2. `RefreshDevicesAsync` coleta snapshot e publica eventos.
3. `RunCommandAsync` atualiza estado (`queued -> sent -> progress -> final`).
4. `BuildAndExportAsync` executa toolchain e publica progresso/log.

## Pontos de alteracao frequente

- `RefreshInterval`, `CommandTimeout`, `OtaCommandTimeout`.
- Politica de filtro de dispositivos online.
- Mensagens de status e de log.
- Estagios de build e parser de progresso.

## Riscos e efeitos colaterais

- Timer muito agressivo pode elevar CPU.
- Mudanca em timeout pode gerar falso timeout.
- Nao preservar estado ao navegar quebra UX.

## Checklist apos alteracao

- Navegar entre paginas sem perder progresso/build/log.
- Confirmar update de lista sem clique manual.
- Confirmar timeout com mensagem clara.

## Referencias de codigo

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L5) - assinatura: `internal sealed class DeviceOperationsCoordinator : IDisposable`
- [GetStateSnapshot](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L50) - assinatura: `public DeviceOperationsState GetStateSnapshot()`
- [RunCommandAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L104) - assinatura: `public async Task<CommandDispatchResult> RunCommandAsync(...)`
- [BuildAndExportAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L371) - assinatura: `public async Task<string?> BuildAndExportAsync(...)`
- [RefreshDevicesAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L569) - assinatura: `private async Task RefreshDevicesAsync(bool forcePublish)`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/ServerPage.xaml.cs`