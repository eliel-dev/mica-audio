# Handoff: FreeRTOS — OTA Background Task no Core 0

## Objetivo

Mover o download+flash OTA de firmware para uma task FreeRTOS no Core 0 do ESP32-S3,
eliminando o bloqueio de minutos do loop principal (Core 1) durante atualizacoes OTA.
Render HUB75, MQTT e serial continuam funcionando durante a atualizacao.

## Escopo classificado

- Tipo: estrutural (firmware, 1 arquivo C++)
- Criterio de aceite: OTA roda no Core 0. HUB75 continua exibindo progresso em tempo real.
  MQTT progress reportado a cada ~5%. Build dma_exp + dma_diag sem erros.

## Arquivos alterados

### firmware/esp32s3-devkitc1/src/main.cpp

#### Segmento 1 — Constantes, enum, struct e globals

- `kOtaDownloadTaskStackSize = 8192` e `kOtaDownloadTaskPriority = 1` (linha 86-87)
- `enum class OtaTaskResult` (Idle/Running/Success/Failed) — movido para antes dos globals (linha 313)
- `struct OtaTaskParams` — parametros copiados por valor antes do spawn (linha 460)
- Globals de bridge: `gOtaTaskResult` (volatile), `gOtaDownloadTaskHandle`,
  `gOtaBridgeCommandId`, `gOtaBridgeTargetVersion`, `gOtaBridgeLastPercent` (linhas 319-324)
- Forward declarations: `otaDownloadTaskFn`, `processOtaProgressBridge`, `publishPresence` (linhas 406-408)

#### Segmento 2 — `otaDownloadTaskFn()` (linha 2131)

Task FreeRTOS que roda no Core 0. Contem logica de download HTTP + validacao SHA-256 +
flash write extraida de `performFirmwareOta()`. Comunica progresso via globals volateis
(`gOtaProgressPercent`, `gOtaProgressStage`, `gOtaTaskResult`). Nao toca MQTT, matrix,
Preferences, ou WebSocket — tudo delegado para a bridge no Core 1.

#### Segmento 3 — `processOtaProgressBridge()` (linha 2310)

Funcao chamada no loop principal (Core 1). Le globals do task e:
- Running: envia MQTT progress a cada 5% de incremento
- Success: `persistPendingOtaContext()` + `publishPresence("offline")` + `ESP.restart()`
- Failed: limpa estado + reporta erro via MQTT

#### Segmento 4 — Render continuo durante OTA (linha 4945)

`processRenderFrame()` alterado para redesenhar continuamente quando
`gHub75FallbackState == Updating` (sem depender de `gHub75FallbackDirty`).

#### Segmento 5 — Command handler `update_firmware` refatorado (linha ~3993)

Substitui chamada sincrona de `performFirmwareOta()` por spawn de task via
`xTaskCreatePinnedToCore(otaDownloadTaskFn, ..., core=0)`. Inclui guarda de
concorrencia e setup de estado da bridge.

#### Segmento 6 — Integracao no loop() (linha 5087)

`processOtaProgressBridge()` inserido entre `processPendingOtaSafeUpdate()` e
`processNetworkPoll()`.

#### `performFirmwareOta()` antigo

Mantido como dead code para rollback rapido. Remover em commit separado apos validacao E2E.

## Arquitetura cross-core

```
Core 0 (task OTA)                    Core 1 (loop principal)
------------------                   -----------------------
otaDownloadTaskFn()                  processOtaProgressBridge()
  | HTTP GET + SHA256 + flash          | le gOtaProgressPercent
  | escreve gOtaProgressPercent        | le gOtaProgressStage
  | escreve gOtaProgressStage         | le gOtaTaskResult
  | escreve gOtaTaskResult             | envia MQTT progress
  | vTaskDelete(nullptr)               | Success: persist + restart
                                       | Failed: cleanup + error report
                                     
                                     processRenderFrame()
                                       | le gOtaProgressPercent
                                       | le gOtaProgressStage
                                       | desenha tela de progresso (continuo)
```

**Thread safety:**
- `gOtaProgressPercent` (uint8_t) e `gOtaProgressStage` (const char* literal) sao atomicos
  em ESP32 (single-issue in-order). Erro strings escritos ANTES de `gOtaTaskResult = Failed`.
- HTTPClient, `Update.begin/write/end`, WiFi stack — todos thread-safe (mutex ESP-IDF interno).
- PubSubClient (MQTT) NAO e thread-safe — por isso fica exclusivamente no Core 1 via bridge.

## Decisoes tomadas

1. **Apenas OTA no Core 0.** Mover MQTT/render completo exigiria mutex e refatoracao massiva.
   O OTA e o unico subsistema que bloqueia por minutos. Beneficio/custo maximo.

2. **Bridge pattern via globals volateis.** Mais simples que queue/semaphore. Overhead zero
   quando OTA nao esta ativo (uma comparacao uint8_t por iteracao do loop).

3. **Render continuo durante Updating.** Antes, `drawOtaProgressScreen()` era chamado de
   dentro do loop de download. Agora o render loop redesenha a cada frame tick (~60fps)
   consumindo os globals atualizados.

4. **Dead code mantido.** `performFirmwareOta()` antigo permite reverter para modo sincrono
   alterando apenas o command handler. Sera removido apos validacao E2E.

5. **WS desconectado antes do spawn** (linha ~4010). Task nao toca WebSocket.

## Validacoes executadas

```text
pio run -e esp32s3_devkitc1_dma_exp     -> SUCCESS (25s, 38% Flash, 37.8% RAM)
pio run -e esp32s3_devkitc1_dma_diag    -> SUCCESS (29s, 38.3% Flash, 37.8% RAM)
dotnet build MicaAudio.sln -c Debug     -> 0 Erro(s), 35 Aviso(s) (Magick.NET pre-existentes)
powershell docs-validate.ps1            -> OK
powershell ai-governance-check.ps1      -> OK
build-precompiled-firmware.ps1          -> OK (merged 1261120 bytes, OTA 1195584 bytes)
```

## Riscos e rollback

| Risco | Severidade | Mitigacao |
|-------|------------|-----------|
| Stack overflow no task (8KB) | Media | buffer[4096] + sha256 + HTTP ~5KB. 8KB da margem. Monitorar `uxTaskGetStackHighWaterMark` no diag. |
| Race em error strings | Baixa | Strings escritos antes de result flag. ESP32 in-order garante store order. |
| `beginHttpWithDeviceAuth()` le globals do Core 0 | Baixa | `gDeviceId/gToken/gServerHost` setados uma vez no `setup()`, nunca modificados. |
| Dead code `performFirmwareOta()` | Nenhuma | Removido em commit separado apos validacao E2E. |

- **Rollback:** Reverter command handler para chamada sincrona de `performFirmwareOta()`,
  remover bridge e task. Tudo no mesmo arquivo.

## Proximos passos

1. Testar OTA completo com dispositivo fisico: spawn no Core 0, HUB75 mostrando progresso,
   MQTT reportando, restart + validacao.
2. Verificar stack highwater mark no build diag.
3. Testar OTA concorrente (segundo comando rejeitado).
4. Testar falha de rede durante OTA (task reporta erro, estado limpo).
5. Remover `performFirmwareOta()` dead code apos validacao E2E bem-sucedida.
