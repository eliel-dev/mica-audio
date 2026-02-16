# 04 - Threading and Concurrency

## Objetivo

Documentar como o app usa concorrencia para manter UI responsiva, render estavel e operacoes longas (build/comandos) sem bloquear a thread principal.

## Modelo de concorrencia

1. UI WinUI executa em thread unica com marshaling via `DispatcherQueue`.
2. Captura e pipeline de audio rodam em tasks dedicadas.
3. Refresh de dispositivos roda por timer leve e publica snapshot.
4. Build de firmware roda fora da UI e reporta progresso incremental.

## Regras praticas

- Nunca atualizar controles XAML fora da thread de UI.
- Em operacao longa, atualizar estado central e notificar eventos.
- Usar politica `latest-frame-wins` para evitar backlog.
- Em timeout, retornar estado explicito em vez de bloqueio indefinido.

## Referencias de codigo

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L5) - assinatura: `internal sealed class DeviceOperationsCoordinator : IDisposable`
- [DeviceOperationsCoordinator.RefreshDevicesAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L569) - assinatura: `private async Task RefreshDevicesAsync(bool forcePublish)`
- [DeviceOperationsCoordinator.OnBuildProgress](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L646) - assinatura: `private void OnBuildProgress(BuildProgressUpdate update)`
- [MainPage.ActivateVisualizerSessionAsync](../../../src/App.WinUI/Views/MainPage.xaml.cs#L244) - assinatura: `private async Task ActivateVisualizerSessionAsync()`
- [AudioPipelineCoordinator.PipelineLoopAsync](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L74) - assinatura: `private async Task PipelineLoopAsync(CancellationToken token)`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`