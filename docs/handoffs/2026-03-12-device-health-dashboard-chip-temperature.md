# Handoff - Device Health Dashboard + Chip Temperature

## Objetivo

Substituir a metrica oficial de `Uso do processador` por saude do device baseada em latencia do `loop()` e adicionar `Temperatura do chip` no dashboard HTML/WebView2.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - firmware publica `loopHealthyPercent` em janela fixa de `5 s` usando threshold de `25 ms` por iteracao;
  - firmware publica `chipTemperatureCelsius` apenas quando a leitura do sensor interno for valida;
  - host faz pass-through dos novos campos para `DeviceRecord`, `DeviceSnapshot` e `DeviceDashboardDto`;
  - dashboard HTML renderiza card, barra e historico com `loopHealthyPercent`;
  - dashboard HTML renderiza card `Temperatura do chip` sem depender de `loopLoadPercent`.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Server/Hosting/DeviceSession.cs`
- `src/Device.Server/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs`
- `src/Device.Server/wwwroot/dashboard/index.html`
- `src/Device.Server/wwwroot/dashboard/dashboard.css`
- `src/Device.Server/wwwroot/dashboard/dashboard.js`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/App.WinUI/Services/Devices/DeviceRefreshCoordinator.cs`
- `tests/Output.Tests/DeviceTelemetryMessageTests.cs`
- `tests/Output.Tests/DeviceSessionTests.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/DeviceServerHostDashboardTests.cs`
- `tests/Integration.Smoke/DashboardAssetSmokeTests.cs`
- `docs/wiki/reference/device-observability-dashboard.md`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

1. `loopHealthyPercent` passou a ser a metrica oficial do dashboard porque mede latencia percebida do `loop()` em vez de carga util estimada do app.
2. `loopLoadPercent` foi preservado apenas como compatibilidade de contrato e round-trip local, evitando quebra com payloads legados sem manter dependencia visual no dashboard novo.
3. A saude foi classificada em tres faixas fixas (`>= 90`, `>= 75`, `< 75`) para manter coerencia entre valor, subtitulo, barra e tom do card.
4. `chipTemperatureCelsius` ficou restrito ao valor atual do sensor interno, sem historico ou estado termico adicional, para manter a entrega minima e coerente com a telemetria hoje disponivel.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~DeviceSessionTests|FullyQualifiedName~DeviceServerHostMqttTests|FullyQualifiedName~DeviceServerHostDashboardTests" -> OK (23 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DashboardAssetSmokeTests" -> OK (1 teste)
C:\Users\eliels\AppData\Local\Programs\Python\Python313\Scripts\pio.exe run -e esp32s3_devkitc1_dma_exp -> OK
```

## Riscos e rollback

- Risco principal:
  - a metrica de saude depende do comportamento real do `loop()` em hardware; thresholds podem precisar de ajuste fino apos observacao em campo.
- Como reverter:
  - restaurar o emissor oficial de `loopLoadPercent` no firmware;
  - recolocar o dashboard HTML para consumir `loopLoadPercent`;
  - remover `loopHealthyPercent` e `chipTemperatureCelsius` do DTO e do snapshot.

## Proximos passos

1. Validar a classificacao de saude em hardware real com carga visual e com device sem Wi-Fi.
2. Se houver ruido termico ou leituras inconsistentes, definir filtro ou faixa valida para `chipTemperatureCelsius` antes de promover qualquer alerta termico.
