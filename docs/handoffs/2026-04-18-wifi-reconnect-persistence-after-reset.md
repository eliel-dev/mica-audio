# Handoff - 2026-04-18 - wifi-reconnect-persistence-after-reset

## Objetivo

Impedir que um ESP32-S3 ja provisionado volte para `SEM WIFI`/offline apos `power cycle` ou botao `reset` apenas porque o STA ainda nao reconectou dentro da janela curta de boot.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui: helper compartilhado de provisioning incompleto, reconnect cooperativo explicito com credenciais salvas, ajuste do fallback AP e documentacao operacional.
- Nao inclui: mudanca de wire/protocol, alteracao do host `Device.Protocol`/`Device.Server`, troca da stack `Arduino WiFi`, ou novo fluxo de onboarding.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_globals.h`
- `firmware/esp32s3-devkitc1/src/mica_globals.cpp`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.h`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/handoffs/2026-04-18-wifi-reconnect-persistence-after-reset.md`

## Decisoes tomadas

1. `isProvisioningIncomplete()` virou helper compartilhado em `mica_provisioning.cpp`, eliminando drift entre boot e runtime.
2. Device ja provisionado nao abre mais portal AP automaticamente por queda prolongada de Wi-Fi; o portal automatico fica restrito a provisioning incompleto.
3. O boot com credenciais salvas manteve grace curta (`kWifiBootConnectGraceMs = 5000`) e passou a registrar `wifi_waiting_saved_config` quando a conexao nao sobe nessa janela.
4. O runtime ganhou reconnect cooperativo explicito a cada `5 s` com `WiFi.reconnect()`, incluindo religar `WIFI_STA` + `WiFi.begin()` quando o STA nao estiver ativo.
5. `WiFi.setAutoReconnect(true)` passou a ser reforcado explicitamente no boot e no provisioning serial/manual, mesmo com o core Arduino atual ja vindo com auto-reconnect habilitado por default.

## Validacoes executadas

```text
platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1 -> PENDENTE
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> PENDENTE
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> PENDENTE
dotnet build MicaAudio.sln -c Debug -> PENDENTE
```

## Riscos e rollback

- Risco principal: o reconnect explicito entrar em disputa com o estado interno do `Arduino WiFi` em cenarios de flap severo.
- Mitigacao: retry fixo de `5 s`, uso preferencial de `WiFi.reconnect()` e fallback para `WiFi.begin()` apenas quando o STA nao estiver operacional.
- Rollback:
  1. recolocar `isProvisioningIncomplete()` como helper local em `main.cpp`;
  2. remover `gLastWifiReconnectAttemptMs` e o retry cooperativo de `processNetworkPoll()`;
  3. voltar `shouldStartProvisioningFallback` ao comportamento anterior.

## Proximos passos

1. Flashear em hardware real e validar `power cycle` sem reabertura do portal AP.
2. Validar reset fisico traseiro confirmando `wifi_waiting_saved_config -> wifi_reconnected`.
3. Testar roteador indisponivel com creds salvas para garantir que o device fica offline tentando reconectar, sem entrar em `SETUP WIFI`.
