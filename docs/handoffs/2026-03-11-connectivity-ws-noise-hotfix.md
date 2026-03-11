# Handoff de mudanca estrutural

## Objetivo

Reduzir o ruido operacional de conectividade causado por eventos `ws_*` do stream HUB75 sem mudar o wire MQTT/WS, mantendo MQTT como fonte oficial de presenca e tratando WS apenas como diagnostico de stream.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - `lastWifiEvent` deixa de carregar `ws_connecting/ws_connected/ws_disconnected` no firmware;
  - a UI para de registrar `Evento conectividade: ws_disconnected` como log principal por device;
  - churn de refresh causado por `ws_*` legado nao repinta a lista de devices;
  - o firmware passa a agregar flaps WS em log serial local sem poluir a telemetria operacional.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `src/App.WinUI/Services/Devices/DeviceConnectivityEventClassifier.cs`
- `src/App.WinUI/Services/Devices/DeviceLogBook.cs`
- `src/App.WinUI/Services/Devices/DeviceRefreshCoordinator.cs`
- `tests/Output.Tests/DeviceLogBookTests.cs`
- `tests/Output.Tests/DeviceRefreshCoordinatorTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `docs/wiki/modules/device-operations-coordinator.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

1. MQTT retained presence + will continuam sendo a autoridade de online/offline; nenhum criterio novo de disponibilidade foi baseado em WS.
2. O firmware preserva logs `ws_*` apenas no serial/debug local e para de publicalos em `lastWifiEvent`.
3. A app faz normalizacao defensiva de `LastWifiEvent` para lidar com firmware legado ainda emitindo `ws_*`, evitando spam de log e churn de refresh.
4. O diagnostico minimo de flap WS foi mantido local ao firmware via agregacao por janela, sem expor novo campo no contrato publico.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> sucesso
dotnet build MicaAudio.sln -c Debug -> sucesso
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-build -> sucesso
platformio run -e esp32s3_devkitc1_dma_exp -> sucesso
```

## Riscos e rollback

- Risco principal: firmware legado em campo ainda pode enviar `ws_*`, mas a app passa a sanitizar isso localmente para UI/log.
- Risco secundario: a janela de diagnostico `[ws_diag]` pode mascarar flaps esporadicos abaixo do threshold configurado.
- Como reverter:
  - restaurar o comportamento anterior de `setConnectivityState(..., publishEvent: true)` nos call sites WS do firmware;
  - remover a normalizacao de `LastWifiEvent` em `DeviceRefreshCoordinator` e `DeviceLogBook`;
  - remover o helper `DeviceConnectivityEventClassifier`.

## Proximos passos

1. Se o WS continuar flapping em hardware real, instrumentar motivo de fechamento no host e no firmware antes de alterar backoff/reconnect.
2. Considerar expor saude de stream em trilha separada de diagnostico, distinta de `Wi-Fi/provisioning`, caso suporte de campo continue precisando desse sinal na UI.
