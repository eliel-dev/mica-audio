# WiFi Reconnect: WebSocket Resume + Stream Diagnostics

## Objetivo

Corrigir bug critico onde o ESP nao conectava WebSocket apos reconexao Wi-Fi (display preso em NoServer/SEM SERV). Adicionar diagnosticos de stream para depurar frames rejeitados.

## Escopo classificado

Funcional — altera comportamento de rede no firmware sem mudar contratos wire ou arquitetura.

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `firmware/esp32s3-devkitc1/src/mica_network.cpp` | WiFi reconnect: reset `gWsAutoReconnectInitialized=false` + chama `connectWebSocket()` imediatamente |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp:1062-1063` | Log `owner_epoch_stale/missing` com detalhes do epoch |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp:1107` | Log `bins_accepted` com epoch |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp:1151` | Log `frame_accepted` com epoch |

## Decisoes tomadas

1. **WiFi reconnect WS fix**: ao detectar `wifiWasDisconnected`, reseta `gWsAutoReconnectInitialized=false` e chama `connectWebSocket()` diretamente, eliminando dependencia do retry periodico de 60s.
2. **Stream diagnostics**: adicionados `publishDeviceLog` em pontos criticos do `applyStreamBinaryFrame` para checkpoint de epoch rejeitado/aceito.

## Validacoes executadas

| Comando | Resultado |
|---------|----------|
| `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` | OK - nenhuma falha |
| `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` | OK - governanca IA valida |
| `dotnet build MicaAudio.sln -c Debug` | OK - 0 erros, 0 avisos |

## Riscos e rollback

- Risco baixo: a chamada `connectWebSocket()` na reconexao Wi-Fi segue o mesmo path do boot inicial.
- Rollback: reverter as 2 linhas adicionadas (1388-1389) no bloco `if (wifiWasDisconnected)`.

## Proximos passos

- Reflash firmware no ESP32-S3 e teste completo: boot sem Wi-Fi → reconexao → verificar WebSocket conectado.
- Validar stream diagnostics no serial monitor para confirmar behavior de epochs.