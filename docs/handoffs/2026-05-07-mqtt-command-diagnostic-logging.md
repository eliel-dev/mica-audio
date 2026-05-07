# Diagnostico MQTT: Comandos Nao Chegam ao Firmware

## Objetivo

Adicionar logging diagnostico (Serial.printf) nos pontos criticos do path MQTT comando → firmware para identificar por que `activate_app` e `queue_panels_batch` nao sao processados pelo ESP32-S3.

## Escopo classificado

Funcional — altera logging diagnostico no firmware sem mudar contratos wire ou arquitetura.

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `firmware/esp32s3-devkitc1/src/mica_network.cpp:1165-1180` | `onMqttMessage`: log de toda mensagem MQTT recebida (topic + len), log de topic mismatch (esperado vs recebido), log de null guards, log de enqueue result |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp:1276-1278` | `connectMqtt`: log do topico exato de subscricao commands |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:892-923` | `enqueueIncomingControlCommand`: log de command name, source (MQTT/WS), clientId, ownerEpoch; log de parse JSON falho e command vazio |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:592-720` | `handleSessionAwareControlCommand`: log de entrada com command name + session-aware flag; log de ownership path (ADOPT/STEAL/RENEW); log de stale_owner_epoch rejection |
| `firmware/esp32s3-devkitc1/src/mica_session.cpp:200-211` | `adoptActiveClientOwner`: Serial.printf com clientId, epoch, lease duration |
| `firmware/esp32s3-devkitc1/src/mica_session.cpp:213-222` | `renewActiveClientOwner`: Serial.printf com clientId e epoch |
| `firmware/esp32s3-devkitc1/src/mica_session.cpp:159-165` | `expireSessionLeases`: log quando owner lease expira (6s timeout) |

## Decisoes tomadas

1. **Serial.printf sempre-on**: logs nao dependem de `MICA_SERIAL_TELEMETRY` porque sao diagnostico critico para o path de comandos. Disparam apenas quando mensagens MQTT chegam (baixa frequencia).
2. **Tags padronizadas**: `[mqtt_msg]`, `[cmd]`, `[session]` para filtragem no serial monitor.
3. **Nao usar publishDeviceLog como substituto**: o path de falha pode ser MQTT nao entregando, entao publishDeviceLog (que usa MQTT) seria inutil. Serial.printf e imediato e independente.

## Validacoes executadas

| Comando | Resultado |
|---------|----------|
| `docs-validate` | OK - nenhuma falha |
| `dotnet build MicaAudio.sln -c Debug` | OK - 0 erros, 0 avisos |

## Riscos e rollback

- Risco minimo: logs Serial.printf sao sincronos mas so disparam quando mensagens chegam.
- Rollback: reverter as 6 edicoes em mica_network.cpp, mica_commands.cpp e mica_session.cpp.

## Proximos passos

1. Build firmware (`pio run -e esp32s3_devkitc1_dma_exp -t upload`) com clean previo se necessario.
2. Monitorar serial e observar logs `[mqtt_msg]`, `[cmd]`, `[session]`.
3. Cenarios de teste:
   - Boot com Wi-Fi conectado → verificar `[mqtt_msg] recebido` e `[cmd] enqueue`.
   - Ativar painel via servidor → verificar `activate_app` no `[cmd]` e `[session] owner_adopted`.
   - Se `[mqtt_msg] DROPPED topic mismatch` aparece, investigar diferenca de topicos.
   - Se `[mqtt_msg]` nunca aparece, problema e no broker MQTTnet (InjectApplicationMessage).