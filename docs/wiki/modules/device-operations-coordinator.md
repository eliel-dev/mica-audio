# Modulo DeviceOperationsCoordinator

## Objetivo

Centralizar estado operacional da aba `Dispositivos`: refresh continuo, comandos tracked, progresso e logs.

## Responsabilidades

- Manter snapshot de dispositivos para UI.
- Manter devices conhecidos visiveis mesmo quando estao offline.
- Controlar timer de refresh e diff de lista.
- Executar comandos tracked com timeout.
- Permitir concorrencia por dispositivo: 1 comando por device, em paralelo entre devices diferentes.
- Armazenar logs de operacao com limite.

## Fluxo de execucao

- A lista da UI nao e mais online-only: devices offline continuam no snapshot e aparecem abaixo dos online.

1. `SetDevicesPageVisible(true)` liga polling.
2. `RefreshDevicesAsync` coleta snapshot e publica eventos.
3. `RunCommandAsync` valida o device e reserva slot por `deviceId`.
4. Progresso chega por evento do host e atualiza `CommandByDevice`.
5. Conclusao remove o slot do device e publica estado final.

## Pontos de alteracao frequente

- `RefreshInterval` e `CommandTimeout`.
- Politica de visibilidade de dispositivos online/offline.
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

## Atualizacao 2026-03 - Lifecycle Leve

- A lista de devices nao depende mais apenas de `DeviceStatus` bruto.
- A `DevicesPage` aplica uma politica local de lifecycle (`DeviceLifecyclePolicy`) para distinguir:
  - `Online | Configurado`
  - `Offline | Configurado`
  - `Offline | Configuracao incerta`
  - `Registrado | Nunca conectado`
  - `Registrado | Aguardando provisionamento` (compatibilidade legada de `Pairing`)
- `Offline` nao significa automaticamente `nao configurado`.
- A ordenacao continua priorizando devices ativos, mas o snapshot mantem devices offline visiveis.


## Atualizacao 2026-03 - Render Estavel na DevicesPage

- `DeviceListChanged` e a fonte principal do refresh da lista na `DevicesPage` apos a carga inicial.
- `StateChanged` continua atualizando estado geral, mas nao deve disparar rebuild da lista de devices.
- A UI reaproveita a arvore visual existente e aplica diff incremental para reduzir flicker.