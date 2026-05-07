# Command Execution Diagnostic Logging

## Objetivo

Adicionar logging Serial.printf nos pontos criticos do caminho de execucao de comandos MQTT no firmware para diagnosticar por que `activate_app` e `queue_panels_batch` chegam ao gate de session mas nao produzem efeito visivel no display HUB75.

## Escopo classificado

Funcional — altera apenas logging no firmware, sem mudanca de contrato ou arquitetura.

## Contexto do problema

Logs diagnosticos anteriores confirmaram que:
1. Comandos MQTT chegam e passam pelo gate de session (ADOPT/STEAL/RENEW funcionam).
2. `activate_app` com epoch=1 causa STEAL (epoch salta de 2 para 3).
3. `queue_panels_batch` e `session_heartbeat` com epoch=3 fazem RENEW corretamente.
4. Servidor da timeout de 10s — nenhum command_progress ACK recebido de volta.

Mas **nao ha logs** mostrando se `handleControlCommandMessageCore` executa, se `sendCommandProgress` publica com sucesso, ou se o batch download inicia/falha.

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `firmware/esp32s3-devkitc1/src/mica_network.cpp:466` | `sendCommandProgress` — log SKIP (mqtt desconectado/commandId vazio) e resultado do publish |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:500` | `activate_app` — log appId e displayName |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:542` | `queue_panels_batch` — log session, batchSeq, frames, duration, URL |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:260` | `schedulePanelsBatchDownload` — log slow domain busy + deferring |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:738` | `processPanelsBatchSlowCommand` — log START, DOWNLOAD OK/FAIL, VALIDATE OK/FAIL, QUEUE OK/FAIL |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:369` | `handleControlCommandMessageCore` — log command e commandId |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:1201` | `controlWorkerTask` — log kind e commandId antes do switch |

## Validacoes executadas

| Comando | Resultado |
|---------|----------|
| `docs-validate` | OK — nenhuma falha |
| `dotnet build MicaAudio.sln -c Debug` | OK — 0 erros, 0 avisos |

## Decisoes tomadas

1. Logging via `Serial.printf` com prefixos `[cmd_progress]`, `[cmd]`, `[cmd_core]`, `[batch]`, `[worker]` consistentes com tags ja existentes.
2. `sendCommandProgress` agora loga quando pula (mqtt desconectado ou commandId vazio) e quando publica (resultado bool).
3. Pontos de log cobrem todo o fluxo: recebimento MQTT → gate de session → `handleControlCommandMessageCore` → handler especifico → `sendCommandProgress` → publicacao MQTT.
4. Fluxo lento (batch download) coberto em todas as fases: START, DOWNLOAD, VALIDATE, QUEUE.

## Riscos e rollback

- Risco baixo: apenas Serial.printf diagnostico, sem mudanca de logica.
- Rollback: remover as linhas `Serial.printf` adicionadas em cada ponto.

## Proximos passos

- Reflash firmware no ESP32-S3 com `esp32s3_devkitc1_dma_exp` (producao, sem `MICA_SERIAL_TELEMETRY`).
- Ativar painel pelo WinUI/server e observar no serial monitor os logs `[cmd_progress]`, `[cmd_core]`, `[batch]`, `[worker]`.
- Diagnosticar especificamente: (a) se `sendCommandProgress` esta publicando MQTT, (b) se o batch download inicia, (c) se o servidor recebe o ACK via `command-events`.