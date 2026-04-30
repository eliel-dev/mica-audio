# Handoff - 2026-04-17 - firmware-control-worker-hardening

## Objetivo

Reduzir o risco de stall/reset no firmware ESP32-S3 ao tirar trabalho bloqueante de callbacks MQTT/WS e do caminho quente do `loop()`, mover jobs lentos para `Core 0`, endurecer o runtime de `Paineis` e expor observabilidade suficiente para diferenciar travamento, watchdog e backlog do plano de controle.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui:
  - fila de ingress do plano de controle e worker dedicado no `Core 0`;
  - `queue_panels_batch` assicrono com download + validacao fora do callback MQTT;
  - migracao do playback `Paineis` para `Core 0`;
  - agendamento assicrono do portal de provisioning fora de `processNetworkPoll()`;
  - `esp_task_wdt` em `loopTask`, control worker, worker de playback e OTA;
  - novos campos opcionais de telemetria/runtime no firmware, protocolo e host.
- Nao inclui:
  - migracao grande para APIs nativas do ESP-IDF (`esp_mqtt_client`, `esp_http_client`);
  - mudanca do wire atual de `commands`, `command-events` ou stream WS binario;
  - persistencia cross-reboot da duracao do ultimo job lento.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_commands.cpp`
- `firmware/esp32s3-devkitc1/src/mica_commands.h`
- `firmware/esp32s3-devkitc1/src/mica_globals.cpp`
- `firmware/esp32s3-devkitc1/src/mica_globals.h`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_ota.cpp`
- `firmware/esp32s3-devkitc1/src/mica_panels.cpp`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Server/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceSession.cs`
- `tests/Output.Tests/DeviceTelemetryMessageTests.cs`
- `tests/Output.Tests/DeviceSessionTests.cs`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/handoffs/2026-04-17-firmware-control-worker-hardening.md`

## Decisoes tomadas

1. O runtime ficou explicitamente hibrido:
   - `loopTask` continua no `Core 1` para MQTT, WS, render e ponte de OTA;
   - `control worker` e playback `Paineis` ficam no `Core 0`.
2. `onMqttMessage()` e WS-texto agora so fazem validacao minima + enqueue de `ControlCommandEnvelope`; o parse/dispatch real acontece fora do callback.
3. `queue_panels_batch` deixou de fazer HTTP + SHA + validacao `WebP` no callback e passou a usar o worker do `Core 0`, preservando ordem por diferimento de um envelope quando outro batch ainda esta em andamento.
4. `update_firmware` continua com o download/gravação OTA em task dedicada no `Core 0`, mas a descoberta/validacao do release oficial saiu do caminho do callback e entrou no `control worker`.
5. O fallback de provisioning manteve a semantica AP-first existente, mas a abertura do portal saiu de `processNetworkPoll()` e virou request separado no loop.
6. O host/protocolo aceitaram novos campos opcionais de runtime (`resetReason`, estados dos workers, profundidade da fila e ultimo slow command) sem quebrar payload legado.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~DeviceSessionTests" -> OK
platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (warnings NU190x preexistentes de Magick.NET-Q8-AnyCPU 14.11.1)
```

## Riscos e rollback

- Risco principal: a fila de controle e o diferimento de `queue_panels_batch` preservam estabilidade do loop, mas podem introduzir latencia adicional se o host gerar batches mais rapido que o worker consegue baixar/validar.
- Risco secundario: `startProvisioningPortal()` continua bloqueante por natureza do `WiFiManager`; o hardening atual apenas tirou essa abertura do `processNetworkPoll()` e suspende o `task watchdog` do loop durante o portal.
- Mitigacoes:
  - observabilidade nova em telemetria para backlog/estado dos workers;
  - `esp_task_wdt` nos tasks principais do runtime;
  - exclusao mutua explicita entre OTA, provisioning e batches lentos.
- Rollback:
  1. recolocar `handleControlCommandMessage()` no callback MQTT/WS;
  2. remover o `control worker` e voltar `queue_panels_batch` ao fluxo sincrono anterior;
  3. mover `panelsBatchPlaybackTask` de volta para `Core 1`;
  4. remover os campos opcionais novos do payload/status e do host.

## Proximos passos

1. Validar em hardware real por pelo menos `30 min` com alternancia de stream bruto, `queue_panels_batch` repetido e mudancas de app para observar `controlQueueDepth`, `controlWorkerState` e `panelsWorkerState`.
2. Forcar um cenario controlado de watchdog/stall para confirmar se `resetReason` e o ultimo `slow command` ajudam a distinguir starvation do loop versus worker.
3. Se o host passar a produzir batches mais rapido do que o worker consome, evoluir do envelope diferido unico para uma fila dedicada de jobs `Paineis` no `Core 0`.
