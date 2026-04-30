# Handoff - Device Observability Dashboard

## Objetivo

Implementar observabilidade nativa por device no app WinUI, mantendo o control plane MQTT no host embutido e, na iteracao atual, expondo `Logs` na `SettingsPage` enquanto `Estatisticas` ficam fora da UI para curadoria posterior.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - `stats/logs` MQTT aceitos no host e persistidos no snapshot;
  - `DevicesPage` com dashboard seguro sem tabs de observabilidade;
  - `SettingsPage` com `ComboBox` de device e `Expander` `Logs`, com degradacao explicita para firmware legado;
  - historico curto em memoria por device;
  - handoff e wiki atualizados.

## Arquivos alterados

- `src/App.WinUI/Views/SettingsPage.Observability.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.Selection.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Devices/DeviceLogBook.cs`
- `src/App.WinUI/Services/Devices/DeviceTelemetryHistoryBook.cs`
- `src/Device.Protocol/Models/DeviceStatsMessage.cs`
- `src/Device.Protocol/Models/DeviceLogMessage.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/reference/device-observability-dashboard.md`

## Decisoes tomadas

1. A UI continua 100% nativa em WinUI 3; `WebView2` segue fora porque nao houve bloqueio real de plataforma.
2. A `DevicesPage` ficou focada no dashboard seguro ja existente para evitar regressao funcional em `offline`, `legacy-only` e ausencia de telemetria.
3. `Logs` foram realocados para `Configuracoes`, com `ComboBox` local de device e `Expander` colapsado por padrao; `Estatisticas` continuam disponiveis no backend, mas ficaram ocultas da UI.
4. `Logs` continuam consolidados em `DeviceLogEntry` tipado, com merge de eventos locais e do firmware por device.
5. O firmware publica `stats` como retained e `logs` como nao retained; historico e logs continuam apenas em memoria da sessao do app.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -m:1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~DeviceObservabilityMessageTests|FullyQualifiedName~DeviceTelemetryHistoryBookTests|FullyQualifiedName~DeviceLogBookTests|FullyQualifiedName~DeviceOperationsCoordinatorDeviceLogsTests|FullyQualifiedName~DeviceServerHostMqttTests" -> OK (24 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-build --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~SettingsPageSmokeTests" -> OK (15 testes)
```

## Riscos e rollback

- Risco principal:
  - o firmware agora emite mais eventos MQTT; ruido excessivo de log pode exigir ajuste fino de categorias e thresholds.
- Como reverter:
  - mover `Logs/Estatisticas` de volta para a `DevicesPage`;
  - desabilitar `stats/logs` no firmware mantendo apenas `status/presence`.

## Proximos passos

1. Validar smoke manual com um firmware MQTT moderno e um firmware legado.
2. Ajustar volume de logs do firmware se houver ruido operacional em sessao longa.
3. Se o v1 se provar estavel, considerar export ou persistencia opcional de logs de sessao.
