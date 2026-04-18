# Referencia - Device Telemetry v2 Fields

## Objetivo

Definir o contrato de telemetria v2 entre firmware, protocolo, servidor e App.WinUI, incluindo regras de sanitizacao e consumo na `DevicesPage`.

## Campos do payload de telemetria WS

| Campo | Tipo | Semantica |
| --- | --- | --- |
| `uptimeSeconds` | `int?` | uptime do firmware em segundos |
| `loopHealthyPercent` | `int?` | percentual de iteracoes do `loop()` concluidas em ate `25 ms`, calculado em janela fixa de `5 s` |
| `loopLoadPercent` | `int?` | campo legado de compatibilidade; nao e mais a metrica oficial consumida pelo dashboard HTML |
| `chipTemperatureCelsius` | `double?` | leitura do sensor interno de temperatura do ESP32-S3 em graus Celsius quando disponivel |
| `freeHeapBytes` | `long?` | heap livre em bytes |
| `largestHeapBlockBytes` | `long?` | maior bloco contiguo de heap livre |
| `psramAvailable` | `bool?` | indica se o build/device possui PSRAM utilizavel |
| `freePsramBytes` | `long?` | psram livre em bytes |
| `largestPsramBlockBytes` | `long?` | maior bloco contiguo de psram livre |
| `wifiConnected` | `bool?` | estado de conectividade Wi-Fi reportado pelo firmware |
| `wifiState` | `string?` | estado canonico de conectividade (`connecting|connected|portal|disconnected`) |
| `provisioningPortalActive` | `bool?` | indica se o portal de provisioning esta ativo no firmware |
| `auxLedAvailable` | `bool?` | disponibilidade do LED auxiliar no hardware/build atual |
| `testLedAvailable` | `bool?` | disponibilidade efetiva de LED de teste (onboard e/ou auxiliar) |
| `lastWifiEvent` | `string?` | ultimo evento curto de conectividade reportado pelo firmware |
| `telemetrySequence` | `uint?` | contador monotono de heartbeats de telemetria por sessao |
| `brightnessCap` | `int?` | limite de brilho aplicado pelo device (`30..160`) |
| `brightnessRequested` | `int?` | brilho solicitado pelo stream/comando antes do cap |
| `brightnessApplied` | `int?` | brilho efetivamente aplicado no painel |
| `testLedEnabled` | `bool?` | estado legado/compatibilidade do LED auxiliar continuo |
| `testLedDuty` | `int?` | duty atual do LED auxiliar em escala 8-bit (quando aplicavel) |
| `animatedWebpBatchSupported` | `bool?` | capacidade declarada pelo firmware para receber batches animados `WebP` da sessao `Paineis` |
| `streamFramesReceived` | `uint?` | quantidade de payloads de stream aceitos pelo firmware (`bins` ou `frame`) |
| `streamFramesApplied` | `uint?` | quantidade de payloads novos efetivamente exibidos ao menos uma vez no painel HUB75 |
| `hub75PresentFrames` | `uint?` | contador monotono de `flipDMABuffer()` realmente apresentados no HUB75 |
| `streamSequenceGapCount` | `uint?` | quantidade acumulada de lacunas de sequencia detectadas no stream |
| `streamInvalidFrameCount` | `uint?` | quantidade acumulada de payloads invalidos rejeitados |
| `streamLastSequence` | `uint?` | ultimo numero de sequencia aceito do stream |
| `networkPollDeferCount` | `uint?` | quantidade monotona de etapas elegiveis de rede adiadas para a iteracao seguinte por esgotamento do budget cooperativo do `loop()` |
| `resetReason` | `string?` | reset reason do CPU0 no boot atual, exposto com o nome curto da ROM (`POWERON_RESET`, `RTC_SW_SYS_RESET`, `TG0WDT_SYS_RESET`, etc.) |
| `controlQueueDepth` | `uint?` | profundidade atual da fila de ingress do plano de controle, incluindo um comando lento diferido ainda nao despachado |
| `controlWorkerState` | `string?` | estado resumido do worker de controle no Core 0 (`idle`, `panels_downloading`, `panels_validating`, `fetching_firmware`, `awaiting_ota_result`, `provisioning_pending`, `failed`) |
| `panelsWorkerState` | `string?` | estado resumido do runtime de playback `Paineis` (`idle`, `pending_batch`, `decoding`, `presenting`, `cancelled`, `failed`) |
| `lastSlowCommand` | `string?` | ultimo comando lento observado pelo runtime (`enter_provisioning`, `update_firmware`, `queue_panels_batch`) |
| `lastSlowCommandDurationMs` | `long?` | duracao, em milissegundos, do ultimo comando lento concluido no boot atual |

## Regras de sanitizacao e pass-through

1. Sanitizacao de `largestHeapBlockBytes` e `largestPsramBlockBytes` ocorre apenas no firmware emissor.
2. O servidor deve tratar `loopHealthyPercent`, `loopLoadPercent`, `chipTemperatureCelsius` e os demais campos v2 em pass-through (sem clamp/renormalizacao).
3. Campos permanecem `nullable` para compatibilidade com firmware legado.
4. `loopLoadPercent` continua aceito apenas para leitura/round-trip de compatibilidade.
5. `chipTemperatureCelsius` so deve ser emitido quando a leitura do sensor interno vier valida (`finite`).
6. `wifiState` usa valores canonicos em minusculo para facilitar consumo no dashboard.
7. `streamFramesApplied` representa payload novo efetivamente exibido ao menos uma vez; reapresentacoes do mesmo conteudo nao incrementam esse contador.
8. `hub75PresentFrames` representa a cadencia fisica de apresentacao do HUB75; cada flip real incrementa esse contador monotono.
9. `networkPollDeferCount` e emitido apenas pelo firmware nesta entrega e serve como diagnostico bruto do budget cooperativo de rede no `loop()`.
10. `animatedWebpBatchSupported` funciona como capability bit para o host optar por `queue_panels_batch` em `Paineis`; `null` continua significando firmware legado sem suporte declarado.
11. `resetReason` usa a causa do CPU0 para simplificar o diagnostico pos-boot no host; o campo nao tenta expor toda a matriz de causas por core.
12. `controlQueueDepth` inclui o comando diferido em memoria quando `queue_panels_batch` aguardou o fim do job lento anterior para preservar ordem.
13. `lastSlowCommandDurationMs` cobre apenas comandos lentos concluidos no boot atual; duracao de OTA bem-sucedida nao e persistida apos reboot.

## Persistencia local

- `DeviceSnapshot` e `DeviceRecord` carregam os campos v2 para uso em UI e store.
- `JsonDeviceRegistryStore` faz round-trip desses campos no `devices.json`.
- Em offline, a UI pode mostrar o ultimo snapshot conhecido sem depender de nova telemetria.

## Consumo no dashboard por device

- A superficie oficial desta entrega e o dashboard HTML/WebView2 servido por `Device.Server`.
- O card principal de saude usa `loopHealthyPercent` com tres faixas:
  - `>= 90`: `Saudavel: loop estavel`;
  - `>= 75 e < 90`: `Atencao: latencia moderada`;
  - `< 75`: `Sobrecarregado: latencia elevada`.
- O dashboard HTML nao depende mais de `loopLoadPercent` para renderizar saude.
- O dashboard exibe `chipTemperatureCelsius` no card `Temperatura do chip`, com fallback textual quando ausente.
- O estado de update de firmware no dashboard nao e telemetria crua:
  - `firmwareVersion` continua vindo do device;
  - `latestFirmwareVersion`, `firmwareUpdateSupported` e `firmwareUpdateAvailable` sao derivados no host a partir do catalogo oficial de firmware embarcado no app.
- `hub75Fps` e calculado no host a partir do delta de `hub75PresentFrames`, portanto reflete a cadencia real de apresentacao do painel.
- Barras derivadas de fragmentacao sao exibidas apenas quando os dados sao coerentes.
- `RSSI` deve aparecer apenas quando o `snapshot.Status` esta online; para offline a UI exibe estado de rede sem sinal numerico.
- O card de logs usa `GetDeviceLogs(deviceId)` e exibe somente o device selecionado.
- Sem selecao, dashboard e logs exibem placeholders dedicados.
- O dashboard exibe heartbeat (`telemetrySequence`) e estado de LED de teste sem depender de log textual.
- O slider de brilho usa `brightnessCap` e o card mostra `brightnessApplied/brightnessCap`.
- O dashboard tambem mostra:
  - estado canonico de Wi-Fi;
  - portal ativo/inativo;
  - idade do ultimo heartbeat;
  - ultimo evento curto de conectividade por dispositivo;
  - contadores de stream em tabela textual segura.
- O fallback nativo antigo da `DevicesPage` continua fora do escopo funcional desta entrega.

## Checklist rapido de validacao

- Device online atualiza o painel seguro com status `Online`.
- Device offline exibe aviso `Offline (ultimo snapshot)` e mantem dados do ultimo snapshot.
- Sem selecao, o dashboard mostra placeholder de selecao.
- Logs trocam junto com a selecao do device e nao misturam eventos de outros devices.

## Referencias de codigo

- [firmware telemetry sender](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [DeviceTelemetryMessage](../../../src/Device.Protocol/Models/DeviceTelemetryMessage.cs#L1)
- [DeviceSnapshot](../../../src/Device.Protocol/Models/DeviceSnapshot.cs#L1)
- [DeviceRecord](../../../src/Device.Protocol/Models/DeviceRecord.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceMetricsFormatter](../../../src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs#L1)
- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
