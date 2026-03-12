# Modulo Firmware HUB75 (DevKitC-1 128x64)

## Fluxo de execucao

1. conecta ao servidor local
2. recebe `StreamFrameV2` tipo `1` (`bins128`) ou tipo `2` (`frame 128x64 RGB565`)
3. renderiza `drawBars` ou `drawFrame128x64`
4. conecta o control plane MQTT para `presence`, `status`, `stats`, `logs` e `commands`
5. reporta `boardModel = esp32s3_devkitc1` e `panelType = hub75_p2_5_128x64_smd2121_scan32`

## Perfil oficial

1. O unico firmware ativo da base e `dma_exp`.
2. O antigo perfil `stable` foi removido do fluxo oficial.
3. O build oficial do DevKitC-1 agora usa um board local do projeto:
   - `mica_esp32_s3_devkitc1_n16r8`
   - `QIO 80MHz`
   - `16MB flash`
   - `OPI PSRAM` via `memory_type = qio_opi`
   - particao local `3MB APP / 9.9MB FATFS`

## Atualizacao 2026-03 - Perfil oficial N16R8 para DevKitC-1

- O board padrao `esp32-s3-devkitc-1` do PlatformIO instalado no ambiente local estava definido como `N8` sem PSRAM.
- Para eliminar esse drift, o env oficial `esp32s3_devkitc1_dma_exp` passou a usar um board local versionado no repositorio:
  - `boards/mica_esp32_s3_devkitc1_n16r8.json`
- O pinout continua o do `variant = esp32s3`, preservando compatibilidade com o DevKitC-1 usado no projeto.
- A particao oficial deixou de depender de alias do framework e passou a usar um CSV local versionado:
  - `partitions/mica_app3M_fat9M_16MB.csv`
- O pacote precompilado oficial continua com o mesmo nome logico:
  - `esp32s3-devkitc1-128x64-dma_exp_merged.bin`
  - `esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- Requisito operacional:
  - na primeira gravacao apos migrar de um layout/configuracao anterior, fazer erase total do flash antes do upload.
- A variante `2MB APP / 12.5MB FATFS` permanece apenas como alternativa documentada, nao como baseline oficial.

## Pontos de alteracao frequente

1. `platformio.ini` para largura, altura e o unico env oficial
2. `boards/mica_esp32_s3_devkitc1_n16r8.json` para o perfil oficial da placa
3. `partitions/mica_app3M_fat9M_16MB.csv` para o layout oficial `3MB APP / 9.9MB FATFS`
4. `main.cpp` para parsing do stream e desenho
5. `scripts/build-precompiled-firmware.ps1` para gerar `BIN + manifesto` embarcados no app

## Atualizacao 2026-03 - Buffer WS para frame 128x64

- O build do firmware define `WEBSOCKETS_MAX_DATA_SIZE=32768` em `platformio.ini`.
- O objetivo e suportar com margem payloads binarios grandes do stream `frame 128x64 RGB565` sem queda de conexao por limite de frame no cliente WS.

## Atualizacao 2026-03 - MQTT cutover do control plane

- O firmware passou a usar MQTT para controle e telemetria:
  - `mica/v1/devices/{deviceId}/commands`
  - `mica/v1/devices/{deviceId}/command-events`
  - `mica/v1/devices/{deviceId}/status`
  - `mica/v1/devices/{deviceId}/presence`
  - `mica/v1/devices/{deviceId}/stats`
  - `mica/v1/devices/{deviceId}/logs`
- `presence` publica `online` no birth e `offline` no will/saida graciosa.
- `status` continua no heartbeat de `2s`, agora como mensagem MQTT retained.
- `stats` publica identidade/capacidade do firmware no boot logico do MQTT e a cada reconexao do broker.
- `logs` publica eventos estruturados das categorias `wifi`, `mqtt`, `portal`, `ws`, `stream` e `command`.
- `WStype_BIN` foi preservado intacto como hot path visual; WS-texto virou apenas compatibilidade passiva.
- O firmware persiste `mqttHost`, `mqttPort` e `mqttRootTopic` em `Preferences`.
- O pacote oficial entregue pelo app agora inclui:
  - `esp32s3-devkitc1-128x64-dma_exp_merged.bin`
  - `esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- O onboarding valida esse manifesto antes do flash e rejeita pacotes sem `controlPlane = mqtt`.
- Quando o servidor de pareamento ainda nao informar campos MQTT, o firmware faz fallback para:
  - `mqttHost = host`
  - `mqttPort = 5273`
  - `mqttRootTopic = mica/v1/devices`

## Atualizacao 2026-03 - `loopLoadPercent` como carga util do app

- `loopLoadPercent` deixou de representar ocupacao bruta do loop bare-metal.
- A amostra agora mede apenas trabalho util:
  - renderizacao (`drawBars` / `drawFrame128x64`);
  - telemetria e publish MQTT;
  - manutencao de WS/MQTT e tarefas operacionais do app.
- Esperas deliberadas ficam fora da conta:
  - principalmente `delay(120)` no ramo sem Wi-Fi.
- O objetivo e tornar o card `Uso do processador` util no dashboard WebView, sem saturar artificialmente em `99%`.

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
- O fallback empacotado atual esta em `v2026.03.12-untagged-c2ba150`.

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

## Atualizacao 2026-03 - Hotfix de ruido WS na conectividade

- `lastWifiEvent` continua no payload MQTT `status`, mas agora fica restrito a eventos de `Wi-Fi/provisioning`.
- Eventos `ws_connecting`, `ws_connected` e `ws_disconnected` permanecem apenas em serial/debug local.
- O firmware agrega flaps WS em janela local e emite `[ws_diag]` quando detectar repeticao de desconexoes, sem poluir a telemetria operacional.
- `presence` MQTT retained + will continuam sendo a fonte oficial de disponibilidade do device.

## Atualizacao 2026-03 - Rollback onboarding para COM+flash + AP

- Fluxo oficial voltou para:
  - app faz somente `COM -> flash -> exibe pair code`;
  - provisioning de rede/pair ocorre no portal AP do firmware.
- O firmware abre AP de setup imediatamente no boot quando detectar configuracao incompleta.
- O portal AP voltou a expor um campo editavel `Servidor`, aceitando `http://host:porta`, `host:porta` ou `host`.
- Quando o campo `Servidor` vier vazio ou invalido, o firmware preserva um host salvo valido e registra o motivo em serial/`lastWifiEvent`.
- O contrato serial `mica.serial.v1` permanece no codigo apenas para compatibilidade futura e diagnostico.

## Referencias de codigo

- [main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1)
- [board local N16R8](../../../firmware/esp32s3-devkitc1/boards/mica_esp32_s3_devkitc1_n16r8.json#L1)
- [particao local 3MB APP / 9.9MB FATFS](../../../firmware/esp32s3-devkitc1/partitions/mica_app3M_fat9M_16MB.csv#L1)
- [build-precompiled-firmware.ps1](../../../scripts/build-precompiled-firmware.ps1#L1)
