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
- O hotfix de conectividade trata `ws_*` como diagnostico de stream, nao como evento principal de conectividade:
  - logs por device continuam mostrando apenas transicoes de `Wi-Fi/provisioning`;
  - churn de refresh causado por `ws_connected/ws_disconnected` legado deixa de repintar a lista.

## Atualizacao 2026-03 - Fase 9 Wave 1, coordenador decomposto

- O `DeviceOperationsCoordinator` continua com a mesma API publica do app, mas agora atua como fachada fina sobre colaboradores internos fixos:
  - `DeviceRefreshCoordinator`
  - `DeviceCommandDispatcher`
  - `DeviceCommandTracker`
  - `DeviceLogBook`
  - `DeviceLifecycleThresholdProvider`
- A separacao reduziu concentracao de estado e deixou coberturas focadas em:
  - gate de refresh;
  - timeout e tracking de comando por device;
  - cap de logs;
  - fallback lazy de thresholds.
- O shape de `DeviceOperationsState`, os textos operacionais e o wire com `Device.Server` permaneceram inalterados.

## Observabilidade tecnica

- `RunCommandCoreAsync` agora abre o span raiz de operacao de device no lado app (`AppObservability.DeviceIntegrationComponent`).
- O coordinator replica no scope estruturado as mesmas chaves de correlacao usadas no span:
  - `deviceId`
  - `commandId`
  - `appId` quando o comando carrega esse parametro
  - `commandType`
- O objetivo e deixar o path `acao de UI -> coordinator -> host embutido -> ACK` navegavel por correlacao, sem depender apenas do texto livre dos logs.
- O comportamento funcional do coordinator nao mudou:
  - continua 1 comando por device;
  - continua permitindo paralelismo entre devices distintos;
  - continua usando `DeviceCommandTracker` para refletir progresso na UI.
