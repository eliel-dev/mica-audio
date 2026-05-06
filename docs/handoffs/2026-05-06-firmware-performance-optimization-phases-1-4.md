# Firmware Performance Optimization — Phases 1-4

## Objetivo

Otimizar performance do loop principal do firmware ESP32-S3 para atingir 60fps no HUB75 128x64. Antes das mudancas: `hub75_fps=0-8`, `serial_max_us=11-15ms`, `net_max_us=14-164ms`, DRAM livre=18KB.

## Escopo classificado

Estrutural — altera contratos de inicializacao (PSRAM buffers), fluxo de rede (MQTT/WS connect), constantes de tempo e arquitetura de buffers.

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `firmware/esp32s3-devkitc1/src/mica_types.h` | `kSessionLeaseTickMs` 250→1000, `kMqttConnectSocketTimeoutSeconds` 5→1, size constants para PSRAM |
| `firmware/esp32s3-devkitc1/src/mica_globals.h` | Declaracoes de buffer como ponteiros PSRAM, novos globals `gMqttPostConnectPending`, `gWsAutoReconnectInitialized`, `gLastBinsStyleId`, `gLaunchpadTopLevels`, `gLaunchpadSideLevels`, `initializePsramBuffers()` |
| `firmware/esp32s3-devkitc1/src/mica_globals.cpp` | Buffers como nullptr + `initializePsramBuffers()` com `heap_caps_malloc(MALLOC_CAP_SPIRAM)`, novas definicoes de globals |
| `firmware/esp32s3-devkitc1/src/main.cpp` | Chamada `initializePsramBuffers()` no setup, sizeof→constantes |
| `firmware/esp32s3-devkitc1/src/mica_network.h` | Declaracao `processMqttPostConnect()` |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp` | `connectMqtt()` com post-connect adiado, `processMqttPostConnect()` novo, `processNetworkPoll()` refatorado com budget gate em connect, WS auto-reconnect |
| `firmware/esp32s3-devkitc1/src/mica_session.cpp` | `processClientSessionRuntime()` publica shadow apenas quando dirty |
| `firmware/esp32s3-devkitc1/src/mica_visuals.cpp` | sizeof→constantes |
| `firmware/esp32s3-devkitc1/src/mica_display.cpp` | sizeof→constantes |
| `firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp` | `kMaxUdpPacketsPerPoll` 4→2 |
| `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp` | `sendSerialHello()` com `MICA_SERIAL_TELEMETRY` |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp:842-895` | `reportPerfMetrics()` com `MICA_SERIAL_TELEMETRY` |
| `firmware/esp32s3-devkitc1/platformio.ini` | `-DMICA_SERIAL_TELEMETRY` em `dma_diag` apenas |
| `src/MicaAudio.Server/ServerPanelRuntimeService.cs` | Batch dispatch fire-and-forget, preload lead 250→500ms, 3 initial batches, loggers 2102-2104 |

## Decisoes tomadas

1. **PSRAM para buffers de frame/shadow/bins** — Libera ~73KB de DRAM. Acesso PSRAM fora de spinlocks e seguro; o memcpy de frame acontece ANTES do `portENTER_CRITICAL` (validado em mica_network.cpp:1136 vs 1138).
2. **MQTT socket timeout 5s→1s** — PubSubClient nao suporta async connect; reduzir timeout limita o bloqueio maximo a ~1s em vez de 5s.
3. **Post-connect MQTT adiado** — As 5 publicacoes pos-connect (presence, log, stats, shadow, telemetry) agora rodam no loop seguinte via `processMqttPostConnect()`, espalhando o custo.
4. **WS auto-reconnect** — Apos `connectWebSocket()` inicial, `gWs.setReconnectInterval(2000)` cuida de reconexoes sem bloqueio adicional no loop. A flag `gWsAutoReconnectInitialized` evita reconectar manualmente.
5. **Session tick 250→1000ms + dirty-only publish** — Reduz publicacoes MQTT de shadow de ~4/s para ~1/s (ou zero se nada mudou).
6. **UDP packets per poll 4→2** — HMAC-SHA256 custa ~0.5-1ms por pacote; reduzir de 4 para 2 corta 1-2ms do poll.
7. **Budget gate em connect** — `shouldRunNetworkStep(shouldReconnect && !networkBudgetExhausted)` evita iniciar connect bloqueante quando ja esta acima do orçamento.
8. **Server batch dispatch fire-and-forget** — `QueueNextBatchAsync` publica o comando MQTT mas nao aguarda o ACK do ESP32. O `DispatchBatchCommandAsync` roda em background com `CancellationToken.None`. O batch fica registrado em `InMemoryPanelsBatchStore` (max 4 por device) e o ESP32 faz download no seu ritmo via slow command queue.
9. **BatchPreloadLead 250→500ms + 3 initial batches** — Mais margem para o preload cobrir a latencia de download do ESP32 (~132-1110ms por batch).

## Validacoes executadas

| Comando | Resultado |
|---------|-----------|
| `pio run -e esp32s3_devkitc1_dma_exp` | SUCCESS (RAM 16.1%, Flash 45.6%) |
| `pio run -e esp32s3_devkitc1_dma_diag` | SUCCESS (RAM 16.1%, Flash 46.0%) |
| `dotnet build MicaAudio.sln -c Debug` | SUCCESS (0 warnings, 0 errors) |
| `scripts/docs-validate.ps1` | OK |
| `scripts/ai-governance-check.ps1` | OK |

## Riscos e rollback

- **PSRAM fallback para DRAM**: Se PSRAM falhar, `initializePsramBuffers()` tenta DRAM. Se DRAM tambem falhar, firmware para com mensagem critica no Serial.
- **Timeout MQTT 1s**: Em redes com latencia alta (>1s round-trip), MQTT pode falhar mais frequentemente. Reverter `kMqttConnectSocketTimeoutSeconds` para 5 resolve.
- **WS auto-reconnect**: Se a biblioteca WebSocketsClient nao reconectar corretamente apos perda de WiFi, `gWsAutoReconnectInitialized` sera limpo no path `!wifiConnected`, forçando nova chamada `connectWebSocket()` na reconexao WiFi.
- **Session tick 1000ms**: Reduz responsividade de expiracao de lease. Lease expiry ainda funciona, apenas e checado a cada 1s em vez de 250ms.
- **Batch fire-and-forget**: O servidor nao aguarda ACK do ESP32 para avancar o estado do batch. Se o ESP32 falhar em processar um batch (offline, erro), o servidor continuara produzindo batches que ficam no `InMemoryPanelsBatchStore` ate o max de 4, depois os mais antigos sao descartados. O state check a cada 1s detecta mudanca de estado e encerra o loop.
- **CancellationToken.None no dispatch**: O `DispatchBatchCommandAsync` usa `CancellationToken.None` para nao cancelar o envio MQTT se o loop principal for cancelado. Isso garante que o comando chegue ao ESP32 mesmo durante shutdown.

## Proximos passos

1. **Flash + testar** no hardware com monitor serial — validar fps e net_max_us.
2. **Fase 5 (Render)**: Deferir shadow copy em `drawFrame128x64()`, batch bins write-back.
3. Se fps ainda estiver abaixo de 30, considerar mover MQTT publish para Core 0 via queue.
