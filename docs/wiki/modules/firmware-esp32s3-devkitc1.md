# Modulo Firmware HUB75 (DevKitC-1 128x64)

## Referencias oficiais obrigatorias

- Para qualquer integracao do `ESP32-S3` neste repositorio, consultar primeiro:
  - `https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/index.html`
  - `https://github.com/espressif/esp-idf/blob/v5.5.4/docs/en/index.rst`
- Essa exigencia vale para decisoes de implementacao, configuracao, OTA, networking, boot, particoes e uso de APIs de baixo nivel do SoC.

## Fluxo de execucao

1. sobe Wi-Fi salvo ou abre portal AP quando ainda nao ha Wi-Fi configurado
2. descobre o `MicaAudio.Server` por UDP LAN e registra/reutiliza o device sem codigo de pareamento
3. conecta HTTP/WS/MQTT usando `deviceId`, token e endpoints recebidos do discovery
4. recebe `StreamFrameV2` tipo `1` (`bins128`) por WS ou UDP LAN opt-in, ou tipo `2` (`frame 128x64 RGB565`) por WS
5. renderiza `drawBars`, `drawFrame128x64` ou o fallback local de conectividade
6. conecta o control plane MQTT para `presence`, `status`, `stats`, `logs` e `commands`
7. reporta `boardModel = esp32s3_devkitc1` e `panelType = hub75_p2_5_128x64_smd2121_scan32`

## Direcao oficial

- O firmware do ESP32-S3 passa a ser runtime de execucao/render com ownership explicito por `device`.
- `MQTT` vira o plano canonico de sessao: `shadow`, `takeover`, `lock lease` e heartbeat.
- `WS/UDP` continuam no data plane visual, mas subordinados ao `ownerEpoch` atual.
- Quando o owner expira, o fallback oficial do painel passa a ser `relogio + cliente desconectado`.

## Baseline atual / transicao

- `StreamFrameV2` continua aceito como wire legado.
- `StreamFrameV3` entra como wire owner-bound para clientes session-aware.
- O firmware continua aceitando o caminho legado enquanto nao houver owner ativo, para nao quebrar a transicao.

## Atualizacao 2026-04 - Zero-Code LAN Onboarding

- O portal AP fica responsavel por Wi-Fi, nome do dispositivo e campo `Servidor` opcional apenas para fallback tecnico.
- O caminho normal nao pede mais codigo de pareamento: depois que o Wi-Fi conecta, o firmware envia broadcast UDP `mica.discovery.v1` na porta `5275`.
- O payload discovery inclui `deviceMac`, `deviceName`, `firmwareVersion`, `boardModel`, `panelType` e `profile`.
- A resposta `MicaDiscoveryResponseV1` persiste `deviceId`, `token`, `httpBase`, `mqttHost`, `mqttPort`, `mqttRootTopic`, `wsPath` e `visualUdpPort` em `Preferences`.
- Com Wi-Fi salvo, ausencia de `deviceId/token/host` nao abre AP automaticamente; o device permanece vivo e tenta discovery com backoff.
- O endpoint `/api/v1/pair` e o parser de pair code continuam no firmware apenas como compatibilidade tecnica.
- `processNetworkPoll()` executa discovery, MQTT e WS de forma cooperativa; chamadas HTTP/MQTT receberam timeouts explicitos e pontos de `feedTaskWatchdog()` para evitar que `loopTask` ultrapasse o budget do TWDT.
- Logs seriais curtos marcam os limites do boot/conexao:
  - `discovery_started`
  - `discovery_broadcast`
  - `discovery_registered`
  - `mqtt_connecting` / `mqtt_connected`
  - `ws_begin` / `ws_begin_done`
- A decisao preserva o task watchdog ativo, alinhada com a funcao do TWDT de detectar tarefas que deixam de ceder execucao por tempo prolongado.

## Ownership, shadow e lock lease

- `mica_session.h/.cpp` concentra o estado efemero de sessao do device.
- O topico retained `.../shadow` passa a ser a fonte oficial de ownership para observacao multi-cliente.
- `clientId`, `ownerEpoch` e `lockToken` entram no envelope de comandos session-aware.
- `session_heartbeat`, `session_lock_acquire` e `session_lock_release` passam a ser comandos canonicos de sessao.
- Quando ha owner ativo, o firmware passa a exigir `ownerEpoch` valido no stream owner-bound e descarta payload stale.
- Quando o owner expira, o fallback do HUB75 passa a usar `ClientDisconnected` com relogio local e mensagem de desconexao.

## Perfil oficial

1. O unico firmware ativo da base e `dma_exp`.
2. O antigo perfil `stable` foi removido do fluxo oficial.
3. O build oficial do DevKitC-1 agora usa um board local do projeto:
   - `mica_esp32_s3_devkitc1_n16r8`
   - `QIO 80MHz`
   - `16MB flash`
   - `OPI PSRAM` via `memory_type = qio_opi`
   - `ARDUINO_USB_MODE=1`
   - `ARDUINO_USB_CDC_ON_BOOT=1`
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
- O manifesto oficial do pacote embarcado no app subiu para `schemaVersion = 2`:
  - `firmwareVersion`
  - `sha256`
  - `fileSizeBytes`
  - metadados de compatibilidade (`boardModel`, `panelType`, `profile`, `controlPlane`)
- Requisito operacional:
  - na primeira gravacao apos migrar de um layout/configuracao anterior, fazer erase total do flash antes do upload.
- A variante `2MB APP / 12.5MB FATFS` permanece apenas como alternativa documentada, nao como baseline oficial.

## Pontos de alteracao frequente

1. `platformio.ini` para largura, altura e o unico env oficial
2. `boards/mica_esp32_s3_devkitc1_n16r8.json` para o perfil oficial da placa
3. `partitions/mica_app3M_fat9M_16MB.csv` para o layout oficial `3MB APP / 9.9MB FATFS`
4. `main.cpp` para orquestracao de `setup()`, `loop()` e render
5. `mica_types.h` para enums, structs e constantes compartilhadas
6. `mica_globals.h/.cpp` para declaracao/definicao de globals
7. `mica_display.h/.cpp` para HUB75, LEDs, fallback e pacing
8. `mica_visuals.h/.cpp` para estilos visuais nativos de `Bins128`
9. `mica_network.h/.cpp` para MQTT, WebSocket, HTTP e telemetria
10. `mica_visual_udp.h/.cpp` para receiver UDP LAN opcional de `Bins128`
11. `mica_ota.h/.cpp` para OTA context, download e progress bridge
12. `mica_panels.h/.cpp` para panels batch: download, validacao e playback
13. `mica_commands.h/.cpp` para parser de comandos tracked e gate central de ownership
14. `mica_session.h/.cpp` para shadow retained, leases, owner epoch e lock de sessao
15. `mica_provisioning.h/.cpp` para serial provisioning, WiFiManager e pairing
16. `firmware/esp32s3-devkitc1/scripts/patch_websockets_max_data_size.py` para preservar o override de payload WS no build oficial
17. `firmware/esp32s3-devkitc1/scripts/patch_hub75_bulk_rgb565.py` para expor o writer bulk RGB565 na dependencia HUB75 pinada
18. `scripts/build-precompiled-firmware.ps1` para gerar `BIN + manifesto` embarcados no app

## Atualizacao 2026-04 - Rollback para AP-first estavel

- Nota de evolucao: no fluxo zero-code LAN, essa regra fica restrita a Wi-Fi ausente ou entrada manual em provisioning. Faltar `deviceId/token/host` com Wi-Fi ja salvo aciona discovery LAN, nao AP automatico.
- Quando faltam `host/porta/deviceId/token` no boot, o firmware volta a abrir o AP `MicaAudio-Setup-xxxx` imediatamente.
- O `setup()` chama `startProvisioningPortal(...)` direto no boot incompleto e recarrega `Preferences` apos o portal fechar.
- O hotfix de `startConfigPortal()` direto foi preservado:
  - sem voltar para `autoConnect()`;
  - sem timeout de portal;
  - com timeout explicito apenas para a tentativa STA do submit.
- A janela serial-first de `60 s` deixou de fazer parte do caminho oficial.
- `mica.serial.v1` continua no codigo apenas como compatibilidade/diagnostico.
- O fallback por queda prolongada de Wi-Fi permanece ativo apenas para devices com provisioning incompleto.
- Depois que o display estiver inicializado, o fallback HUB75 continua priorizando `SETUP WIFI`.

## Atualizacao 2026-04 - Reconnect apos reset com credenciais salvas

- O firmware oficial passou a separar claramente dois estados:
  - `provisioning incompleto`: pode abrir o portal AP automaticamente;
  - `device ja provisionado`: nao reabre o portal AP sozinho so porque o Wi-Fi ainda nao voltou.
- A regra canonica de `provisioning incompleto` saiu do `main.cpp` e passou a morar em `mica_provisioning.cpp`, virando fonte unica de verdade para:
  - boot `AP-first`;
  - fallback automatico para provisioning no runtime.
- No boot de device ja provisionado:
  - o firmware fixa `WiFi.setAutoReconnect(true)` antes de `WiFi.begin()`;
  - usa uma grace curta `kWifiBootConnectGraceMs = 5000`;
  - se o STA ainda nao conectou ao fim dessa janela, segue o boot normal em modo de reconexao, sem reclassificar o device como incompleto.
- O runtime agora aplica reconnect cooperativo explicito para credenciais salvas:
  - `kWifiReconnectRetryMs = 5000`;
  - cada retry usa `WiFi.reconnect()`;
  - se o STA nao estiver ativo, o firmware religa `WIFI_STA` e reaplica `WiFi.begin()` com a configuracao salva.
- Logs seriais oficiais desta fase:
  - `wifi_waiting_saved_config`
  - `wifi_reconnect_retry`
  - `wifi_reconnected`
- Consequencia operacional:
  - `power cycle` e botao `reset` devem reconectar com as credenciais ja persistidas;
  - se o roteador estiver fora do ar, o device permanece offline tentando reconectar;
  - o portal AP automatico fica reservado a ausencia real de `host/porta/deviceId/token` ou entrada manual em provisioning.

## Atualizacao 2026-04 - AP-first com HUB75 adiado no boot limpo

- No baseline `Arduino-ESP32 v3.3.8` sobre `ESP-IDF v5.5.4`, os buffers RX estaticos do Wi-Fi continuam sendo alocados dentro de `esp_wifi_init()` em memoria DMA interna.
- Para evitar `ESP_ERR_NO_MEM` no flash limpo:
  - o `setup()` agora abre `Preferences`;
  - carrega o estado minimo de provisioning;
  - se faltarem `host/porta/deviceId/token`, chama `startProvisioningPortal(...)` antes de `initMatrixDisplay()`;
  - so inicializa `MatrixPanel_I2S_DMA` e o restante do runtime depois que a decisao de provisioning termina.
- Consequencia operacional:
  - o AP `MicaAudio-Setup-xxxx` passa a ter prioridade sobre a mensagem imediata no HUB75;
  - o primeiro portal bloqueante pode aparecer sem `SETUP WIFI` na matriz;
  - o fallback HUB75 continua valido para portal/falha de rede quando o display ja estiver ativo.
- Leituras de `Preferences` no boot/provisioning/OTA passaram a usar `isKey()` antes de `get*()`:
  - flash limpo deixa de gerar cascata `NOT_FOUND`;
  - chaves ausentes usam defaults seguros;
  - o boot registra no maximo um resumo de chaves ausentes quando a configuracao minima ainda nao existe.

## Atualizacao 2026-04 - Boot provisionado sobe STA antes do HUB75

- O hotfix AP-first do boot limpo nao era suficiente para devices ja provisionados:
  - o `setup()` ainda carregava o runtime pesado do HUB75 antes do primeiro `WiFi.begin()`;
  - isso podia disputar RAM interna/DMA com `esp_wifi_init()` e gerar `ESP_ERR_NO_MEM`, `Expected to init 4 rx buffer, actual is 3/0` e `STA enable failed`.
- O fluxo oficial do boot provisionado agora foi quebrado em duas fases:
  - `boot leve` antes do primeiro `WiFi.begin()`: `Preferences`, regra de provisioning, brilho/LEDs auxiliares, OTA boot state e filas minimas do loop;
  - `boot HUB75` so depois da grace inicial de reconnect: `resetMatrixShadowState()`, `initMatrixDisplay()` e `initializePanelsBatchRuntime()`.
- Consequencia operacional:
  - o device provisionado tenta subir o STA salvo antes de inicializar `MatrixPanel_I2S_DMA`;
  - o painel pode aparecer alguns segundos depois do reset/power cycle;
  - o portal AP automatico continua restrito a provisioning incompleto.
- Observabilidade oficial de boot:
  - `boot_mem stage=before_saved_wifi_begin`
  - `boot_mem stage=after_saved_wifi_grace`
  - `boot_mem stage=before_hub75_init`
  - `boot_mem stage=after_hub75_init`
  - cada log registra `freeHeapBytes`, `largestHeapBlockBytes` e `largestDmaBlockBytes`.
- O runtime de controle tambem deixou de reservar heap interno desnecessario no boot:
  - `gSlowCommandQueue` voltou a nascer apenas sob demanda junto do `control worker`;
  - `initializeControlCommandRuntime()` cria apenas as filas minimas de ingress/async.
- O setup do `esp_task_wdt` foi ajustado para reduzir ruido de boot:
  - primeiro tenta `esp_task_wdt_reconfigure(...)`;
  - so chama `esp_task_wdt_init(...)` quando o TWDT ainda nao existe;
  - isso remove o log repetido `TWDT already initialized` sem desabilitar o watchdog.

## Atualizacao 2026-05 - Rollback para AP manual

- O caminho experimental de `config.json` no FATFS foi removido do boot oficial.
- O firmware volta a abrir o AP `MicaAudio-Setup-xxxx` sempre que o provisioning estiver incompleto.
- O portal AP permanece o caminho oficial para preencher Wi-Fi, nome do dispositivo e campo `Servidor`.
- Depois do portal fechar com Wi-Fi conectado, o runtime segue com discovery LAN, auto-registro/reuso de device e conexoes MQTT/WS.
- O pacote oficial volta a ser apenas firmware generico; nao ha factory BIN local com credenciais Wi-Fi embutidas.
- DOCS: [main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- DOCS: [mica_provisioning.cpp](../../../firmware/esp32s3-devkitc1/src/mica_provisioning.cpp#L1)
- DOCS: [build-precompiled-firmware.ps1](../../../scripts/build-precompiled-firmware.ps1#L1)

## Atualizacao 2026-03 - HUB75 128x64 single-canvas mapping

- O contrato visual oficial continua sendo um unico canvas `128x64`, igual ao preview WinUI e ao stream `Frame128x64 RGB565`.
- Para o painel `hub75_p2_5_128x64_smd2121_scan32`, a linha `E` e obrigatoria no firmware oficial do DevKitC-1.
- O pinout operacional alinhado com a bancada validada ficou:
  - `RGB = {4, 5, 6, 7, 15, 16}`
  - `A/B/C/D/E = {18, 8, 3, 42, 17}`
  - `LAT = 40`, `OE = 2`, `CLK = 41`
- O firmware passou a falhar cedo se um painel `128x64` for inicializado sem `E` ou com conflito entre `E` e `CLK/LAT/OE`.
- O boot serial do display agora registra o pinout HUB75 efetivo para diagnostico rapido de campo.

## Atualizacao 2026-03 - Buffer WS para frame 128x64

- O build oficial passou a patchar a dependencia `WebSockets` via `extra_script` versionado para preservar `WEBSOCKETS_MAX_DATA_SIZE=32768`.
- O objetivo e suportar com margem payloads binarios grandes do stream `frame 128x64 RGB565` (`16400` bytes) sem queda de conexao por limite de frame no cliente WS.
- O firmware agora valida isso em build via `static_assert` e registra no boot o limite WS efetivo ao lado do tamanho do payload `Frame128x64`.

## Atualizacao 2026-03 - HUB75 anti-flicker com double buffer

- O firmware oficial do HUB75 passou a ativar `config.double_buff = true` no `MatrixPanel_I2S_DMA`.
- `commitMatrixFrame()` agora executa `flipDMABuffer()` de verdade no perfil `dma_exp`.
- O painel deixou de ser limpo e redesenhado continuamente sem necessidade:
  - o firmware marca o frame como `dirty` quando chegam bins/frame novos;
  - em `Frame128x64`, a apresentacao continua orientada a frame novo;
  - em `Bins128`, o firmware reapresenta continuamente o ultimo buffer na cadencia suportada pelo painel para recuperar fluidez visual;
  - a cadencia minima de flip deixou de ser um cap fixo e passou a ser derivada de `calculated_refresh_rate` da propria biblioteca.
- O timeout de silencio do stream (`15 s`) agora apaga o painel uma unica vez e evita clears repetidos em todo loop.
- `streamFramesApplied` passou a significar payload novo efetivamente exibido ao menos uma vez no painel, nao apenas payload recebido do stream.
- A taxa real de flip do HUB75 deixou de depender desse contador; ela agora e exposta separadamente por `hub75PresentFrames`.

## Atualizacao 2026-03 - HUB75 upstream baseline fluidity recovery

- O build oficial do firmware passou a fixar a dependencia `ESP32-HUB75-MatrixPanel-DMA` em `3.0.13` (`a6221865c71fd5aeba885c31b81fe41bd36c5705`) para evitar drift do `git` flutuante e manter o baseline upstream usado como referencia de estabilidade para paines `1/32`.
- O perfil oficial do painel `hub75_p2_5_128x64_smd2121_scan32` continua usando `PIXEL_COLOR_DEPTH_BITS=6`, em linha com a recomendacao da README da biblioteca para matrizes `64x64` ou maiores.
- O `HUB75_I2S_CFG` oficial agora assume explicitamente:
  - `driver = SHIFTREG`
  - `double_buff = true`
  - `i2sspeed = HZ_10M`
  - `clkphase = false`
  - `min_refresh_rate = 60`
- O firmware passou a aplicar `setLatBlanking(2)` no painel oficial:
  - a README da upstream recomenda esse ajuste quando houver ghosting com clone/deslocamento horizontal;
  - valores maiores so reduzem brilho e nao entram no baseline oficial.
- O boot serial agora registra os valores efetivos que importam para diagnostico de campo:
  - `driver`
  - `color depth`
  - `calculated_refresh_rate`
  - `latch blanking`
  - `clkphase`
  - `double buffer`
  - intervalo efetivo de apresentacao derivado do `calculated_refresh_rate`
- O antigo tuning `6-bit / 144 Hz` deixou de ser baseline oficial:
  - o projeto prioriza primeiro a fluidez e a integridade visual no hardware real;
  - retuning fino de refresh/driver so volta a ser considerado depois que o baseline upstream estiver estavel no painel.

## Atualizacao 2026-03 - HUB75 60 FPS com pacing fisico correto

- O firmware oficial deixou de limitar `flipDMABuffer()` com `1000 / calculated_refresh_rate` em milissegundos:
  - esse arredondamento podia permitir flips acima da taxa fisica real do painel;
  - o pacing agora e calculado em microssegundos com arredondamento para cima.
- O perfil oficial passou a usar tres intervalos explicitos:
  - `physical_present_interval_us = ceil(1_000_000 / calculated_refresh_rate)`
  - `target_present_interval_us = ceil(1_000_000 / 60)`
  - `effective_present_interval_us = max(target, physical)`
- Consequencia operacional:
  - o firmware tenta apresentar a `60 FPS` apenas quando o painel realmente suporta essa cadencia;
  - se o refresh fisico calculado for menor, o firmware respeita o limite do painel em vez de flipar acima dele.
- O boot serial agora registra:
  - `calculated_refresh_rate`
  - `physical_present_interval_us`
  - `target_present_interval_us`
  - `effective_present_interval_us`
  - `double_buffer`
  - `latch_blanking`
- O render path do HUB75 passou a usar caches em SRAM interna:
  - `hub75PresentFrames` conta cada `flipDMABuffer()` realmente apresentado;
  - `Bins128` usa diff por coluna/segmento, evitando redraw bruto da matriz inteira;
  - `Frame128x64` usa buffer sombra por DMA buffer + diff por linha/pixel, com LUTs para `RGB565 -> RGB888`.
- No host/dashboard:
  - `streamFramesApplied` continua representando payload novo efetivamente exibido ao menos uma vez;
  - `hub75Fps` passa a derivar de `hub75PresentFrames`, refletindo presents reais em vez de apenas payloads novos.
- PSRAM continua apenas como telemetria/capacidade do device:
  - o perfil oficial nao habilita `SPIRAM_FRAMEBUFFER` nem `SPIRAM_DMA_BUFFER`;
  - a decisao segue a documentacao da biblioteca upstream, que associa esse caminho a tradeoffs de clock/banda.

## Atualizacao 2026-03 - HUB75 diagnostic matrix envs

- A investigacao oficial de tearing/ghosting no painel ganhou uma trilha separada do build shipping:
  - `esp32s3_devkitc1_dma_diag` recompila o firmware oficial com `CORE_DEBUG_LEVEL=3`;
  - o env diagnostico preserva o fluxo Mica real (`writeFrameRGB565`, MQTT, WS, `flipDMABuffer()`), mas explicita no serial:
    - `driver`
    - `min_refresh_rate`
    - `calculated_refresh_rate`
    - `physical_present_interval_us`
    - `effective_present_interval_us`
    - `clkphase`
    - `double_buffer`
    - `latch_blanking`
- O baseline shipping passou a deixar `min_refresh_rate = 60` explicito em vez de depender apenas do default implicito da biblioteca.
- Para isolar a origem fora do runtime do app, o repositorio agora inclui dois envs-oracle da lib HUB75:
  - `esp32s3_devkitc1_dma_oracle_shiftreg`
  - `esp32s3_devkitc1_dma_oracle_fm6124`
- Esses envs-oracle usam o mesmo pinout oficial `128x64` do projeto e desenham apenas patterns estaticos/diagnosticos em double buffer, sem depender de `StreamFrameV2`, MQTT nem do runtime visual do app no caminho de uso.
- A investigacao desta fase trava a seguinte politica:
  - `120 Hz` nao vira baseline;
  - o alvo conservador continua sendo `60 FPS` de apresentacao;
  - `min_refresh_rate = 90` so entra como experimento posterior se a matriz de testes mostrar ganho real sem piora de cor/ghosting.

## Atualizacao 2026-03 - HUB75 fallback local de conectividade

- O firmware oficial passou a renderizar uma tela local no proprio ESP32-S3 quando nao houver conectividade operacional para o stream visual do painel.
- O fallback e deliberadamente minimalista e estatico, com copy ASCII curta para alta legibilidade em `128x64`.
- Estados oficiais desta fase:
  - `SEM WIFI`
  - `SEM SERV`
  - `SETUP WIFI`
- Precedencia:
  1. `SETUP WIFI` quando o portal de provisioning estiver ativo
  2. `SEM WIFI` quando nao houver Wi-Fi estavel
  3. `SEM SERV` quando houver Wi-Fi, mas nao houver sessao WebSocket ativa
- MQTT nao entra no criterio do fallback:
  - a tela representa apenas a conectividade que afeta o stream real do HUB75.
- Para evitar flicker em flap curto de rede, `SEM WIFI` e `SEM SERV` usam debounce fixo de `1000 ms`.
- Quando o stream visual normal volta, o fallback sai do painel sem exigir reboot nem reset do host.
- O timeout de stream sem queda de conectividade continua fora deste fallback:
  - se houver servidor conectado, mas faltar frame/sinal, o comportamento atual permanece.

## Atualizacao 2026-03 - familias nativas para `Bins128`

- O tipo `1` (`Bins128`) deixou de cair sempre no mesmo `drawBars()` do firmware.
- O byte `flags` do pacote passou a carregar:
  - `flags[7:3] = styleId`
  - `flags[2:0] = paletteFamilyId`
- `flags = 0` continua valido e preserva o desenho legado.
- O firmware agora usa um dispatcher `drawBinsVisual()` para escolher entre familias nativas leves e dirigidas apenas por bins/level/brightness:
  - `wave-mirror`
  - `mirror-lines`
  - `mirror-blocks`
  - `classic-bars`
  - `flow-line`
  - `history-scan`
  - `radial-orbit`
  - `atmosphere`
  - `launchpad-grid`
- A intencao desta fase e distinguir familias fisicas de preset no painel, sem tentar reproduzir com paridade total o preview WinUI.
- O estado temporal especifico de cada estilo e resetado quando `styleId` muda ou quando o stream expira, evitando carry-over visual entre presets.

## Atualizacao 2026-03 - HUB75 bulk RGB565 back-buffer fix

- A dependencia `ESP32-HUB75-MatrixPanel-DMA` `3.0.13` continua sem expor `getBackBuffer()` publico nem `drawPixelRGB565()`.
- O build oficial passou a aplicar um patch versionado sobre a lib pinada:
  - `firmware/esp32s3-devkitc1/scripts/patch_hub75_bulk_rgb565.py`
- Esse patch adiciona `writeFrameRGB565(const uint16_t* frame565)` como API publica minima da lib, sem expor `fb`, `frame_buffer` ou `back_buffer_id`.
- Na `3.0.13`, o destino real de escrita e o framebuffer apontado por `fb`:
  - `back_buffer_id` indica qual buffer nao esta sendo exibido antes do `flip`;
  - `flipDMABuffer()` entrega esse id ao DMA e so depois alterna `back_buffer_id`/`fb` para o proximo buffer gravavel;
  - por isso o writer bulk nao deve escolher o alvo por `getRowDataPtr(..., back_buffer_id)`, e sim escrever explicitamente no `target_fb` capturado antes do flip.
- O caminho `drawFrame128x64()` do firmware agora:
  - escreve o frame inteiro no back buffer BCM da lib em uma unica chamada;
  - evita `rgb565ToRgb888`, `drawMatrixPixel` e diff por pixel;
  - continua atualizando `gMatrixShadowFrames[...]` por `memcpy` para preservar a volta correta ao renderer de barras.
- O writer bulk agora registra em serial, de forma limitada, o `target_buffer_id`, o `back_buffer_id` observado e `ROWS_PER_FRAME` para auditoria do ownership do back buffer no hardware real.
- O mapeamento escolhido prioriza throughput:
  - `RGB565` e expandido para `6` bitplanes BCM com `R/B 5->6` via replicacao simples de bit;
  - nao usa `lumConvTab` nem replica a curva/gamma antiga da lib nesse caminho.
- O patch falha cedo se as assinaturas esperadas da header/cpp mudarem, evitando drift silencioso no build oficial.

## Atualizacao 2026-03 - HUB75 bulk RGB565 contraste e curva tonal

- O caminho `Frame128x64` do firmware permaneceu bulk/back-buffer, mas deixou de usar o mapper tonal simplificado que expandia `RGB565` apenas por replicacao de bits.
- O writer patchado `writeFrameRGB565()` agora segue a mesma resposta luminosa da upstream para preencher os bitplanes BCM:
  - reconstrucao `RGB565 -> intensidade efetiva`
  - uso da `lumConvTab` (`CIE 1931`) por canal
  - uso de `PIXEL_COLOR_MASK_BIT(..., MASK_OFFSET)` para respeitar o depth BCM configurado
- Para preservar throughput, o bulk writer usa LUTs locais pequenas derivadas da `lumConvTab`:
  - `R/B 5-bit -> luminancia 16-bit`
  - `G 6-bit -> luminancia 16-bit`
- O objetivo desta fase e alinhar o contraste/saturacao dos producers `Frame128x64` com o comportamento perceptivo do caminho upstream `drawPixelRGB888`, sem reabrir:
  - `commitMatrixFrame()`
  - `Bins128`
  - tuning de `brightnessCap`, `clkphase`, `latch_blanking` ou `min_refresh_rate`
- A auditoria de ownership do back buffer foi preservada:
  - `target_buffer_id`
  - `back_buffer_id`
  - `ROWS_PER_FRAME`
- Se ainda houver distorcao residual de cor apos esse fix, o proximo suspeito oficial passa a ser o mapeamento BCM/bitplane remanescente do writer bulk, nao o compositor WinUI nem o transporte de rede.

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

## Atualizacao 2026-04 - Playback Efemero De Batches WebP Para `Paineis`

- O firmware oficial ganhou um runtime dedicado para `queue_panels_batch`, inspirado no modelo de decode/playback desacoplado usado por projetos como Tronbyt:
  - o `loop()` principal continua dono de Wi-Fi, MQTT, WS e comandos;
  - uma task separada cuida do decode/playback do batch ativo.
- O payload do comando tracked inclui:
  - `panelsSessionId`
  - `batchSequence`
  - `downloadUrl`
  - `sha256`
  - `fileSizeBytes`
  - `contentType = image/webp`
  - `frameCount = 30`
  - `durationMs = 1000`
- Fluxo do device:
  1. recebe `queue_panels_batch` via MQTT;
  2. baixa o arquivo por `HTTPClient` usando a mesma autenticacao HTTP do device;
  3. valida `sha256`, `fileSizeBytes` e `contentType`;
  4. valida o lote `WebP` animado com `libwebp` (`WebPAnimDecoder`) antes de enfileirar;
  5. toca o batch uma vez em task dedicada, convertendo cada frame RGBA para o framebuffer `RGB565`/DMA ja existente.
- Politicas travadas do v1:
  - batches ficam apenas em RAM/PSRAM, sem `FFat`;
  - existe apenas `ativo + proximo`;
  - se o proximo nao chegar a tempo, o device mantem o ultimo frame valido e registra underrun;
  - stream WS bruto (`bins`/`frame`) cancela o playback WebP e volta a ser dono do painel imediatamente.
- O firmware agora declara `animatedWebpBatchSupported = true` na telemetria para que o host habilite esse caminho apenas quando suportado.
- O pacote oficial entregue pelo app agora inclui:
  - `esp32s3-devkitc1-128x64-dma_exp_merged.bin`
  - `esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- O onboarding valida esse manifesto antes do flash e rejeita pacotes sem `controlPlane = mqtt`.
- Quando o servidor de pareamento ainda nao informar campos MQTT, o firmware faz fallback para:
  - `mqttHost = host`
  - `mqttPort = 5273`
  - `mqttRootTopic = mica/v1/devices`

## Atualizacao 2026-04 - Hardening Hibrido Do Runtime

- O runtime oficial ficou explicitamente hibrido `Arduino + FreeRTOS`:
  - `loopTask` no `Core 1` permanece dono de `gMqtt.loop()`, `gWs.loop()`, render HUB75, ponte de OTA e manutencao leve de estado;
  - um `control worker` no `Core 0` virou o dono dos jobs lentos de `update_firmware` e `queue_panels_batch`, mas agora nasce sob demanda para nao cobrar heap interno fixo durante o boot do Wi-Fi;
  - a task `panelsBatchPlaybackTask` saiu do `Core 1` e passou para o `Core 0`.
- Callbacks MQTT e WS-texto deixaram de executar trabalho bloqueante:
  - agora so validam o envelope minimo e enfileiram `ControlCommandEnvelope`;
  - o parse/dispatch real acontece fora do callback;
  - `queue_panels_batch` deixou de fazer download + SHA + validacao WebP dentro do handler do MQTT.
- O fallback de provisioning continua com a mesma semantica funcional, mas a abertura efetiva do portal saiu do caminho sincrono de `processNetworkPoll()`.
- O dominio de job lento agora e explicito:
  - `enter_provisioning`, `update_firmware` e `queue_panels_batch` nao competem silenciosamente;
  - `queue_panels_batch` preserva ordem via diferimento de um envelope quando outro batch ainda esta em andamento;
  - OTA e provisioning continuam sendo mutuamente exclusivos.
- O firmware oficial passou a usar `esp_task_wdt` em:
  - `loopTask`
  - `control worker` apenas enquanto um job lento esta em execucao, sem manter a task ociosa inscrita enquanto espera fila
  - `panelsBatchPlaybackTask`
  - `otaDownloadTaskFn`
- A telemetria oficial ganhou campos de observabilidade operacional do runtime:
  - `resetReason`
  - `controlQueueDepth`
  - `controlWorkerState`
  - `panelsWorkerState`
  - `lastSlowCommand`
  - `lastSlowCommandDurationMs`

## Atualizacao 2026-04 - Otimizacao Conservadora Do Pipeline WebP De `Paineis`

- O hot path de playback WebP foi enxugado sem mudar o wire:
  - a espera entre frames deixou de pegar `mutex` a cada iteracao e agora consulta um sinal de cancelamento de leitura barata;
  - a conversao `RGBA -> RGB565` do frame apresentado passou a usar caminhada linear por ponteiro, reduzindo trabalho por pixel no batch task.
- O caminho de medicao local ganhou contadores leves para batches WebP:
  - `decode_max_us` e `present_max_us` entram no reporter de perf existente;
  - a emissao serial detalhada desses campos fica default-off por `kPanelsPerfLoggingEnabled = false`.
- A semantica operacional foi preservada:
  - sem mudanca em `queue_panels_batch`, `frameCount`, `durationMs` ou no fallback para stream WS bruto;
  - sem mover stacks ou buffers quentes para `PSRAM` como estrategia principal de performance.

## Atualizacao 2026-04 - Visual UDP LAN Opt-In

- O firmware passou a declarar `visualUdpSupported = true`, `visualUdpPort = 5274` e `visualUdpMode = bins128` na telemetria.
- `mica_visual_udp.cpp` abre um socket UDP nao bloqueante via BSD/lwIP somente quando o Wi-Fi esta conectado; ao cair Wi-Fi, o socket e fechado.
- O receiver valida envelope `VisualUdpFrameV1` (`MICA`, versao `1`, tamanho, sequencia e HMAC-SHA256 truncado pelo token do device).
- Somente `StreamFrameV2` tipo `1` (`Bins128`) e aceito por UDP; `Frame128x64 RGB565` continua no WebSocket para evitar fragmentacao.
- Pacotes invalidos incrementam o contador existente de frames invalidos e nao alteram o frame atual.
- O WebSocket segue como fallback e tambem cancela playback WebP quando um stream binario valido chega.
- O pacing do playback WebP agora usa deltas entre timestamps depois de apresentar o frame, evitando acelerar frames seguintes quando o primeiro decode for lento.

## Atualizacao 2026-03 - OTA autenticado por HTTP

- O firmware oficial voltou a aceitar o comando wire `update_firmware`.
- Fluxo OTA implementado:
  1. receber `update_firmware` via MQTT `commands`;
  2. consultar `GET /api/v1/device/firmware/latest` com `X-Device-Id` + `X-Device-Token`;
  3. validar `boardModel`, `panelType`, `profile`, `controlPlane`, `sha256` e `fileSizeBytes`;
  4. baixar `GET /api/v1/device/firmware/download?version=...`;
  5. gravar a imagem na particao OTA inativa via `Update`;
  6. persistir `commandId + sourceVersion + targetVersion` em `Preferences` antes do reboot;
  7. reiniciar o ESP32-S3;
  8. entrar em `Safe update mode` no primeiro boot da nova imagem;
  9. publicar `pending-verify` durante a janela local de validacao;
  10. concluir como `validated` ou `rolled-back`.
- O firmware interrompe o stream WS durante a OTA e limpa o painel antes de gravar a imagem.
- Falhas de metadata, tamanho, hash ou espaco OTA respondem `failed` no comando tracked e nao reiniciam o device.
- A implementacao passou a seguir a recomendacao oficial da Espressif para `Safe update mode` no `ESP32-S3`:
  - com rollback habilitado, a nova imagem pode iniciar em `ESP_OTA_IMG_PENDING_VERIFY`;
  - a confirmacao explicita usa `esp_ota_mark_app_valid_cancel_rollback()`;
  - rollback explicito usa `esp_ota_mark_app_invalid_rollback_and_reboot()`.
- Politica local desta base para confirmar a imagem:
  - self-test minimo de `10 s` apos o primeiro boot;
  - sem exigir Wi-Fi, MQTT ou WS;
  - falha de rede nao provoca rollback.
- Os estagios tracked do OTA agora sao:
  - pre-reboot: `metadata`, `downloading`, `flashing`, `rebooting`;
  - pos-reboot: `pending-verify`, `validated` ou `rolled-back`.
- `rebooting` deixou de ser terminal:
  - sucesso real so existe em `validated`;
  - rollback ou timeout encerram a operacao como falha.

## Atualizacao 2026-03 - Saude oficial do loop

### Saude oficial do loop

- A metrica oficial do dashboard deixou de ser `loopLoadPercent`.
- O firmware agora calcula `loopHealthyPercent` como:
  - percentual de iteracoes do `loop()` concluidas em ate `25 ms`;
  - janela fixa de `5 s`;
  - arredondamento para inteiro percentual ao fechar cada janela.
- O tempo medido e o da iteracao completa do `loop()`, incluindo trabalho efetivo do app naquele ciclo.
- O calculo continua ignorando janelas vazias por definicao: sem iteracoes contabilizadas, nao ha novo percentual para publicar.
- Faixas operacionais consumidas pelo dashboard:
  - `>= 90`: `Saudavel: loop estavel`;
  - `>= 75 e < 90`: `Atencao: latencia moderada`;
  - `< 75`: `Sobrecarregado: latencia elevada`.
- `loopLoadPercent` fica apenas como legado de compatibilidade no lado do host/protocolo e deixa de ser emitido pelo caminho oficial de telemetria do firmware.
- O firmware tambem passou a publicar `chipTemperatureCelsius`:
  - leitura do sensor interno via `temperatureRead()`;
  - o campo so e enviado quando a leitura vier valida (`finite`);
  - nao existe estado termico separado nem historico termico dedicado nesta entrega.

## Atualizacao 2026-03 - Budget cooperativo de poll de rede

- O `loop()` passou a limitar o trabalho de rede por iteracao com `kNetworkPollBudgetUs = 8000`.
- O budget cobre apenas a secao cooperativa de rede do `loop()`, antes da secao local de brilho/LED/render.
- Quando uma etapa elegivel de rede e adiada por esgotamento do budget, o firmware incrementa `networkPollDeferCount`.
- O render continua independente do budget:
  - `shouldPresentMatrixFrame(nowUs)` segue sendo o gate oficial;
  - `drawBars()` ou `drawFrame128x64()` continuam encerrando em `commitMatrixFrame()`;
  - o `loop()` nao usa mais `delay()` no caminho principal para a queda de Wi-Fi.

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

## Atualizacao 2026-03 - Versionamento do release oficial

- `kFirmwareVersion` agora usa macro `MICA_FIRMWARE_VERSION`.
- Fallback estatico: `src/firmware_version.h`.
- Build precompilado gera `src/firmware_version.auto.h` com carimbo `UTC timestamp + tag + short commit`.
- Formato oficial do pacote embarcado:
  - `vyyyy.MM.dd-HHmmssZ-<tag>-<sha>`
- O objetivo do timestamp e diferenciar duas geracoes do mesmo commit no mesmo dia sem criar campo extra de release.
- O arquivo auto-gerado e temporario (limpo ao final do script de build).
- O dashboard e o dialogo de update exibem esse valor como `Ultimo release`.

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

## Atualizacao 2026-04 - Module split Phase 1A

- O `main.cpp` monolitico (~5108 linhas) foi dividido em 11 modulos com responsabilidades isoladas.
- O `main.cpp` ficou com ~223 linhas, atuando apenas como orquestrador: `setup()`, `loop()`, `processSignalTimeout()` e `processRenderFrame()`.
- Estrutura final dos modulos:

| Modulo | Responsabilidade | Linhas |
|---|---|---|
| `main.cpp` | Orquestrador: setup, loop, render dispatch | ~223 |
| `mica_types.h` | Header-only: enums, structs, constexpr | ~260 |
| `mica_globals.h/.cpp` | Extern declarations + definicoes de globals | ~206/~195 |
| `mica_display.h/.cpp` | HUB75 init, primitivas, shadow buffer, LEDs, fallback, pacing | ~108/~907 |
| `mica_visuals.h/.cpp` | 10 estilos visuais nativos + dispatcher | ~42/~590 |
| `mica_network.h/.cpp` | MQTT, WebSocket, HTTP, connectivity, telemetria | ~95/~970 |
| `mica_visual_udp.h/.cpp` | Receiver UDP LAN para `Bins128` autenticado por HMAC | ~12/~190 |
| `mica_ota.h/.cpp` | OTA context, boot state, download task, progress bridge | ~39/~580 |
| `mica_panels.h/.cpp` | Panels batch: buffer, download, validate, queue, playback | ~46/~480 |
| `mica_commands.h/.cpp` | handleControlCommandMessage + parameter parsing | ~6/~373 |
| `mica_provisioning.h/.cpp` | Serial provisioning, WiFiManager portal, pairing | ~10/~330 |

- Convencoes do split:
  - Headers usam `#pragma once` e incluem apenas o necessario.
  - Funcoes internas ao modulo ficam `static` no `.cpp`, sem declaracao no `.h`.
  - Funcoes ordenadas no `.cpp` de forma que definicao vem antes de uso, eliminando forward declarations internas.
  - `constexpr` namespace-scope no header e seguro (internal linkage implicito em C++).
  - Default arguments ficam apenas no `.h`.
- Nenhuma funcao foi renomeada; nomes exatos preservados para diff limpo.
- Build metrics estabilizados: RAM 39.0%, Flash 48.5% (identicos ao monolito original).
- 2 FreeRTOS tasks mantidas: `otaDownloadTaskFn` (Core 0) em `mica_ota.cpp`, `panelsBatchPlaybackTask` (Core 1) em `mica_panels.cpp`.

## Atualizacao 2026-04 - Provisioning AP direto no setup

- O caminho oficial de provisioning em `mica_provisioning.cpp` deixou de usar `WiFiManager::autoConnect()` quando o firmware ja sabe que precisa abrir o setup.
- Motivo: no baseline `Arduino-ESP32 v3.3.8` sobre `ESP-IDF v5.5.4`, a tentativa STA previa ao portal podia atrasar ou impedir a aparicao do AP em flash limpo.
- O firmware agora chama `startConfigPortal()` diretamente quando entra em provisioning explicito:
  - boot com host/porta ausentes;
  - boot com `deviceId/token` ausentes;
  - fallback apos queda prolongada de Wi-Fi;
  - entrada explicita em `enterProvisioningMode(...)`.
- O submit do portal continua tentando conectar ao Wi-Fi informado, agora com timeout explicito alinhado a `kWifiConnectAttemptTimeoutMs`.
- O nome salvo do device passa a ser carregado em `String` local antes de montar `WiFiManagerParameter`, evitando depender de `c_str()` sobre temporario no setup do portal.

## Referencias de codigo

- [main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [mica_types.h](../../../firmware/esp32s3-devkitc1/src/mica_types.h#L1)
- [mica_globals.h](../../../firmware/esp32s3-devkitc1/src/mica_globals.h#L1)
- [mica_globals.cpp](../../../firmware/esp32s3-devkitc1/src/mica_globals.cpp#L1)
- [mica_display.h](../../../firmware/esp32s3-devkitc1/src/mica_display.h#L1)
- [mica_display.cpp](../../../firmware/esp32s3-devkitc1/src/mica_display.cpp#L1)
- [mica_visuals.h](../../../firmware/esp32s3-devkitc1/src/mica_visuals.h#L1)
- [mica_visuals.cpp](../../../firmware/esp32s3-devkitc1/src/mica_visuals.cpp#L1)
- [mica_network.h](../../../firmware/esp32s3-devkitc1/src/mica_network.h#L1)
- [mica_network.cpp](../../../firmware/esp32s3-devkitc1/src/mica_network.cpp#L1)
- [mica_visual_udp.h](../../../firmware/esp32s3-devkitc1/src/mica_visual_udp.h#L1)
- [mica_visual_udp.cpp](../../../firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp#L1)
- [mica_ota.h](../../../firmware/esp32s3-devkitc1/src/mica_ota.h#L1)
- [mica_ota.cpp](../../../firmware/esp32s3-devkitc1/src/mica_ota.cpp#L1)
- [mica_panels.h](../../../firmware/esp32s3-devkitc1/src/mica_panels.h#L1)
- [mica_panels.cpp](../../../firmware/esp32s3-devkitc1/src/mica_panels.cpp#L1)
- [mica_commands.h](../../../firmware/esp32s3-devkitc1/src/mica_commands.h#L1)
- [mica_commands.cpp](../../../firmware/esp32s3-devkitc1/src/mica_commands.cpp#L1)
- [mica_provisioning.h](../../../firmware/esp32s3-devkitc1/src/mica_provisioning.h#L1)
- [mica_provisioning.cpp](../../../firmware/esp32s3-devkitc1/src/mica_provisioning.cpp#L1)
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1)
- [patch_websockets_max_data_size.py](../../../firmware/esp32s3-devkitc1/scripts/patch_websockets_max_data_size.py#L1)
- [board local N16R8](../../../firmware/esp32s3-devkitc1/boards/mica_esp32_s3_devkitc1_n16r8.json#L1)
- [particao local 3MB APP / 9.9MB FATFS](../../../firmware/esp32s3-devkitc1/partitions/mica_app3M_fat9M_16MB.csv#L1)
- [build-precompiled-firmware.ps1](../../../scripts/build-precompiled-firmware.ps1#L1)
