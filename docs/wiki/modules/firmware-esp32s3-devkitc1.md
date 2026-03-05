# Modulo Firmware HUB75 (DevKitC-1 128x64)

## Fluxo de execucao

1. conecta ao servidor local
2. recebe `StreamFrameV2` tipo `1` (`bins128`) ou tipo `2` (`frame 128x64 RGB565`)
3. renderiza `drawBars` ou `drawFrame128x64`
4. reporta `boardModel = esp32s3_devkitc1` e `panelType = hub75_p2_5_128x64_smd2121_scan32`

## Perfil oficial

1. O unico firmware ativo da base e `dma_exp`.
2. O antigo perfil `stable` foi removido do fluxo oficial.

## Pontos de alteracao frequente

1. `platformio.ini` para largura, altura e o unico env oficial
2. `main.cpp` para parsing do stream e desenho
3. artefato BIN embarcado no app

## Atualizacao 2026-03 - Buffer WS para frame 128x64

- O build do firmware define `WEBSOCKETS_MAX_DATA_SIZE=32768` em `platformio.ini`.
- O objetivo e suportar com margem payloads binarios grandes do stream `frame 128x64 RGB565` sem queda de conexao por limite de frame no cliente WS.

## Atualizacao 2026-03 - Auth WS por header (RSK-002)

- O handshake WS oficial passou a usar path fixo `/ws/v1/stream` sem token na query string.
- O firmware envia `X-Device-Id` e `X-Device-Token` via `setExtraHeaders(...)`.
- Versao de release desta mudanca: `v2026.03.03-rsk002-ws-header`.

## Atualizacao 2026-03 - Brilho seguro + teste de LED padrao por pulso

- Controle de brilho por dispositivo com limites seguros: `30..160` (escala interna `0..255`).
- Comando `set_brightness` atualiza `brightnessCap` e persiste no `Preferences`.
- `test_led` voltou a ser primariamente pulso curto (modo operacional padrao):
  - usa LED onboard WS2812 quando disponivel;
  - usa LED auxiliar por GPIO quando disponivel;
  - pode acionar ambos no mesmo pulso quando ambos existem.
- Compatibilidade legado:
  - `test_led` com `parameters.enabled=true|false` e aceito como compatibilidade;
  - o hotfix nao depende mais de modo continuo na UI.
- Telemetria expoe `telemetrySequence`, `brightnessCap`, `brightnessRequested`, `brightnessApplied`, `testLedEnabled`, `testLedDuty` e `testLedAvailable`.

## Atualizacao 2026-03 - RSK-004 versionamento automatico do firmware

- `kFirmwareVersion` agora usa macro `MICA_FIRMWARE_VERSION`.
- Fallback estatico: `src/firmware_version.h`.
- Build precompilado gera `src/firmware_version.auto.h` com carimbo `UTC date + tag + short commit`.
- O arquivo auto-gerado e temporario (limpo ao final do script de build).

## Atualizacao 2026-03 - Hotfix P0 Wi-Fi/AP + LED auxiliar seguro

- O pino do LED auxiliar deixou de usar fallback automatico para `LED_BUILTIN/PIN_LED`.
- O pino auxiliar agora e explicito por build flag:
  - `MICA_TEST_LED_GPIO=-1` por default no `platformio.ini` (modo seguro).
- LED onboard do ESP32-S3 e tratado por backend dedicado (`neopixelWrite`) em vez de LEDC em pseudo-pin.
- Em runtime, o firmware valida o pino auxiliar:
  - faixa fisica (`0..SOC_GPIO_PIN_COUNT-1`);
  - sem conflito com pinos HUB75;
  - sem conflito com serial critica (`RX0/TX0`).
- Quando nenhum LED de teste esta disponivel, o firmware retorna `test_led_unavailable`.
- Provisioning foi estabilizado para incidente de campo:
  - sem `ESP.restart()` na falha de `autoConnect`;
  - `WiFiManager` com portal sem timeout (`setConfigPortalTimeout(0)`);
  - abertura imediata do AP no boot quando faltar host/porta ou credencial de device;
  - fallback automatico para provisioning apos queda continua de Wi-Fi;
  - desconexao de WS agora dispara reconexao de WS sem abrir portal automaticamente.
- Telemetria ganhou observabilidade de conectividade:
  - `wifiState`, `provisioningPortalActive`, `auxLedAvailable`, `testLedAvailable`, `lastWifiEvent`.

## Atualizacao 2026-03 - Rollback onboarding para COM+flash + AP

- Fluxo oficial voltou para:
  - app faz somente `COM -> flash -> exibe pair code`;
  - provisioning de rede/pair ocorre no portal AP do firmware.
- O firmware abre AP de setup imediatamente no boot quando detectar configuracao incompleta.
- O contrato serial `mica.serial.v1` permanece no codigo apenas para compatibilidade futura e diagnostico.

## Referencias de codigo

- [main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1)
