# Troubleshooting Matrix

| Sintoma | Diagnostico rapido | Causa comum | Acao recomendada |
|---|---|---|---|
| App abre sem visualizacao | Ver `MainPage` + estado de sessao | pipeline pausado apos navegacao | validar `ActivateVisualizerSessionAsync` e `PauseVisualizerSession` |
| Comando device timeout | Ver status em DevicesPage/ServerPage | device offline ou WS sem resposta | confirmar online, repetir comando, revisar timeout |
| OTA falha com HTTP error | Ver logs de OTA no coordinator e firmware | host/token/sessao invalida | revisar host publico, token e endpoint `/firmware/download` |
| Build falha sem artifacts | Ver logs do `FirmwareBuildService` | toolchain incompleta ou profile quebrado | rodar `EnsureToolchainAsync` e validar `platformio.ini` |
| texto ilegivel em tema | comparar tema sistema e brushes | style sem recurso semantico | revisar Fluent2 tokens e bindings |

## Referencias de codigo

- [DeviceOperationsCoordinator logs](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L785)
- [FirmwareBuildService errors](../../../src/App.WinUI/Services/Devices/FirmwareBuildService.cs#L117)
- [ServerPage.UpdateLogs](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L75)
- [DevicesPage.ApplyState](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L139)