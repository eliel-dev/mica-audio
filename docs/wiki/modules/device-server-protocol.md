# Modulo Device.Server + Device.Protocol

## Objetivo

Fornecer o control plane HTTP/WS/MQTT/UDP do Mica para auto-registro LAN, controle, telemetria, assets e estado duravel dos dispositivos ESP32.

## Direcao oficial

- O `Device.Server` e o control plane oficial do sistema.
- O server continua dono de auto-registro LAN, compatibilidade de pairing, catalogo, comandos tracked, snapshots, logs, assets e metadata de ownership/mode.
- O server nao e mais a topologia oficial do hot path visual para `visualizador` e `Paineis`; nesses casos, o cliente local fala direto com o ESP na LAN.

## Baseline atual / transicao

- O host ainda preserva `/ws/v1/stream`, UDP LAN opt-in e o fluxo embedded/remoto atual.
- Esses caminhos continuam validos como compatibilidade enquanto o client-owned data plane converge.
- O server continua mediando o runtime legado e o fluxo atual de batches `WebP`.

## Responsabilidades

- HTTP API `/api/v1/*` para info, pair, command-ack e health.
- UDP discovery LAN em `5275/udp` para registrar devices confiaveis sem codigo de pareamento.
- Broker MQTT embutido para `commands`, `command-events`, `status`, `presence`, `stats` e `logs`.
- WebSocket `/ws/v1/stream` exclusivamente para stream visual binario legado/de transicao.
- Endpoint de descoberta visual LAN para o WinUI remoto enviar `Bins128` direto ao ESP por UDP, com WS admin como fallback.
- UDP visual server->ESP permanece opt-in/experimental para diagnostico local.
- Admin API opt-in por token para clients remotos WinUI (`/api/v1/admin/*`).
- Admin API de biblioteca para paineis e midias persistidos no `StorageRoot`.
- Admin API de endpoints visuais LAN (`GET /api/v1/admin/visual-endpoints`) para descobrir ESPs online aptos a UDP direto.
- WebSockets admin para eventos (`/ws/v1/admin/events`) e frames remotos (`/ws/v1/admin/frames`).
- Dashboard local para `WebView2` via `GET /dashboard` + `WS /ws/device/{deviceId}` com DTO dedicado.
- Sessao de comandos rastreados com timeout.
- Transporte HTTP autenticado para lotes animados `WebP` da sessao `Paineis`.
- Normalizacao interna de `ServerConfig` para limites, timeouts e CIDRs.
- Controle temporal deterministico via `TimeProvider` no pairing, snapshots e timeouts tracked.
- Encaminhamento de comandos de operacao do device (`test_led`, `set_brightness`, `install/activate/set_app_config`).
- Encaminhamento de `queue_panels_batch` para lotes `WebP` precompostos no host.
- Encaminhamento de `update_firmware` com progresso tracked via `command-events`.
- Controle de acesso de rede e rate limiting por endpoint critico.
- Persistencia de metadados de hardware (`BoardModel`, `PanelType`) por dispositivo.
- Pass-through de telemetria operacional e de conectividade (`loopHealthyPercent`, `loopLoadPercent` legado, `chipTemperatureCelsius`, `wifiState`, `provisioningPortalActive`, `auxLedAvailable`, `testLedAvailable`, `lastWifiEvent`, `resetReason`, `controlQueueDepth`, `controlWorkerState`, `panelsWorkerState`, `lastSlowCommand`, `lastSlowCommandDurationMs`).
- Persistencia round-trip de estatisticas estruturadas do firmware (`chip/sdk/heap/flash/sketch`).
- Encaminhamento de logs estruturados do firmware para a UI via `DeviceLogReceived`.
- Resolucao do firmware oficial por `boardModel + panelType + profile` a partir do pacote precompilado local do app.
- Endpoints autenticados para OTA:
  - `GET /api/v1/device/firmware/latest`
  - `GET /api/v1/device/firmware/download?version=...`
- Endpoint autenticado para batches `Paineis`:
  - `GET /api/v1/device/panels/batches/{batchSequence}.webp?panelsSessionId=...`

## Fluxo de execucao

1. `DeviceServerHost.StartAsync` sobe web app local.
2. Dispositivo com Wi-Fi envia broadcast UDP `mica.discovery.v1` com MAC, IP LAN, nome, firmware, board, painel e profile.
3. Servidor com `TrustedLanAutoRegistration=true` registra ou reutiliza o device por `DeviceMac`, grava `LanIpAddress` a partir do IP declarado pelo firmware e responde por UDP com `deviceId`, `token`, `httpBase`, MQTT, WS e UDP visual.
4. Device autentica no broker MQTT com `clientId = username = deviceId` e `password = token`.
5. `presence`, `status` e `stats` MQTT alimentam online/offline e o snapshot operacional do device.
6. `ISessionStateStore` mantem o estado efemero de presenca e snapshot, sem expor WebSocket/frame stream.
7. App envia comandos tracked (`SendCommandTrackedAsync`) via `mica/v1/devices/{deviceId}/commands`.
8. `ICommandStateStore` e `TrackedCommandState` correlacionam `command-events` por `commandId`.
9. `logs` MQTT transporta eventos estruturados do firmware para o estado do app.
10. `/api/v1/pair` permanece disponivel apenas como compatibilidade/deprecado para fluxos tecnicos.
11. `Device.Client.Remote` consulta `/api/v1/admin/visual-endpoints` e envia `Bins128` direto ao `LanIpAddress:visualUdpPort` do ESP via `VisualUdpFrameV1`; `Frame128x64`, GIF/painel e endpoints ausentes caem para `/ws/v1/admin/frames`.
12. `IDeviceFrameTransport.BroadcastFrame/SendFrame` continua existindo como baseline legado/de transicao; a direcao oficial para tempo real e cliente LAN direto no ESP.

## Ownership, shadow e lock lease

- O firmware passa a tratar `MQTT` como plano canonico de sessao por device.
- O topico retained `mica/v1/devices/{deviceId}/shadow` passa a carregar:
  - `shadowVersion`
  - `mode`
  - `activeClientId`
  - `activeOwnerEpoch`
  - `ownerLeaseRemainingMs`
  - `lockHeld`
  - `lockClientId`
  - `lockReason`
  - `lockLeaseRemainingMs`
  - `activeAppId`
  - `fallbackState`
- `clientId`, `ownerEpoch` e `lockToken` entram como envelope canonico dos comandos de sessao-aware.
- `session_heartbeat`, `session_lock_acquire` e `session_lock_release` passam a compor o protocolo canonicamente observado pelo device.
- `last-writer-wins` define o owner atual por device; lock com lease continua separado do ownership.
- `WS/UDP` seguem como data plane visual, mas subordinados ao `ownerEpoch` atual do device.

## Atualizacao 2026-04 - Fronteira De Client Embutido

- `IDeviceServerHost` permanece como contrato de lifecycle do server embutido e continua implementado por `DeviceServerHost`.
- `IDeviceFrameTransport`, `IDeviceServerClient` e `PanelsBatchRegistration` vivem em `Device.Client.Abstractions`, que e a dependencia permitida para clients/output.
- `Device.Client.Embedded` contem `EmbeddedDeviceServerClient`, a adaptacao local que monta `ServerConfig`, faz seed/save do registry e encaminha eventos/comandos/batches para o host embedded.
- `Device.Server.Abstractions` referencia `Device.Client.Abstractions`; `IDeviceServerHost` continua herdando `IDeviceFrameTransport`, mas permanece limitado ao host embedded/lifecycle.
- O WinUI consome a fronteira app-level (`IDeviceServerClient`) para operacoes de devices e batches de `Paineis`, mantendo o server concreto apenas no composition root.
- Nao houve alteracao de endpoints, portas, topicos MQTT, autenticacao, DTOs wire ou firmware.

## Atualizacao 2026-04 - Storage De Batches WebP

- `IPanelsBatchStore`, `PanelsBatchWrite` e `PanelsBatchEntry` vivem em `Device.Server.Abstractions` como fronteira publica de storage efemero de batches.
- `Device.Server` fornece `InMemoryPanelsBatchStore`, mantendo a semantica atual: chave por `deviceId`, um `panelsSessionId` ativo por device, `SHA-256` calculado no registro e limite de `4` batches recentes por device.
- `DeviceServerHost.PanelsBatches` delega `Save`, `TryGet` e `Clear` ao store, mas preserva as assinaturas de `RegisterPanelsBatch`, `ClearPanelsBatches` e o endpoint de download autenticado.
- O composition root da `App.WinUI` registra `IPanelsBatchStore -> InMemoryPanelsBatchStore` junto do `DeviceServerHost` embedded.
- Nao houve alteracao de URL, payload `queue_panels_batch`, autenticacao, firmware, portas, HTTP/WS/MQTT ou client remoto.

## Atualizacao 2026-04 - Storage De Pairing

- `IDevicePairingStore` vive em `Device.Server.Abstractions` como fronteira publica para pair codes e tentativas de pareamento.
- `Device.Server` fornece `InMemoryDevicePairingStore`, mantendo a semantica atual: codigo case-insensitive, uso unico, TTL por `ExpiresAtUtc`, tentativas por `remoteIpKey`, janela de abuso e reset apos pareamento valido.
- `DeviceServerHost` continua gerando o codigo aleatorio e usando `TimeProvider`, mas delega `SaveCode`, `TryConsumeCode`, `TryRegisterAttempt`, `ResetAttempts` e `Clear` ao store.
- O composition root da `App.WinUI` registra `IDevicePairingStore -> InMemoryDevicePairingStore` junto do `DeviceServerHost` embedded.
- Nao houve alteracao de `/api/v1/pair`, erros HTTP, rate limiter ASP.NET, autenticacao, firmware, portas, HTTP/WS/MQTT ou client remoto.

## Atualizacao 2026-04 - Storage De Comandos Tracked

- `ICommandStateStore` e `TrackedCommandState` vivem em `Device.Server.Abstractions` como fronteira publica para comandos tracked pendentes.
- `Device.Server` fornece `InMemoryCommandStateStore`, mantendo a semantica atual: chave case-insensitive por `commandId`, substituicao por mesmo id, `Remove` com retorno e `Drain` para shutdown.
- `DeviceServerHost.Advanced` continua emitindo observabilidade especifica do host, mas delega armazenamento, lookup e drain dos comandos ao store.
- O composition root da `App.WinUI` registra `ICommandStateStore -> InMemoryCommandStateStore` junto do `DeviceServerHost` embedded.
- Nao houve alteracao de command ids, payloads, eventos de progresso, timeouts, MQTT, HTTP/WS, firmware, portas ou client remoto.

## Atualizacao 2026-04 - Storage De Sessoes De Device

- `ISessionStateStore` e `DeviceSessionState` vivem em `Device.Server.Abstractions` como fronteira publica para presenca efemera, snapshots e records de device.
- `Device.Server` fornece `InMemorySessionStateStore`, mantendo a semantica atual de chave case-insensitive por `deviceId`, substituicao por mesmo id, `Remove`, `CreateSnapshots`, `CreateRecords` e `Drain`.
- `DeviceFrameConnection` e `DeviceFrameConnectionRegistry` ficam internos ao `Device.Server` para preservar WebSocket, fila bounded de frames e `SendToken` fora do contrato publico.
- `DeviceServerHost` usa o store para auth, MQTT presence/status/stats/logs, firmware heartbeat, pairing, seed e snapshots; frame stream continua process-local via `/ws/v1/stream`.
- O composition root da `App.WinUI` registra `ISessionStateStore -> InMemorySessionStateStore` junto do `DeviceServerHost` embedded.
- Nao houve alteracao de endpoints, payloads, topicos MQTT, auth, firmware, portas, client remoto ou semantica de online/legacy/offline.

## Atualizacao 2026-04 - Zero-Code LAN Onboarding + Biblioteca Server-First

- Novos contratos compartilhados em `Device.Protocol`:
  - `MicaDiscoveryRequestV1`
  - `MicaDiscoveryResponseV1`
  - `PanelLibraryDocument`
  - `PanelDeviceState`
  - `PanelLibraryItem`
  - `PanelWidgetItem`
  - `PanelWidgetDataSources`
  - `MediaAssetInfo`
- `DeviceRecord.DeviceMac` passou a ser persistido. Re-registro LAN com o mesmo MAC reutiliza o registro/token existente em vez de criar device duplicado.
- `MicaDiscoveryRequestV1.DeviceIp` passou a carregar o IP LAN real do ESP para preencher `LanIpAddress`, sem depender do IP observado pelo socket HTTP/MQTT quando o servidor roda em Docker.
- `DeviceServerHost` abre UDP discovery apenas quando `ServerConfig.TrustedLanAutoRegistration=true`.
- A resposta discovery anuncia os mesmos endpoints operacionais que o firmware precisa no boot: HTTP, MQTT, root topic, WS path e porta UDP visual.
- `MICA_SERVER__TRUSTEDLANAUTOREGISTRATION=false` bloqueia auto-registro e deixa `/api/v1/pair` como compatibilidade temporaria.
- `StartupPairCodeTtlSeconds` fica `0` por default no standalone para nao emitir pair code na UX normal.
- `IPanelLibraryStore` e `IMediaLibraryStore` isolam persistencia de biblioteca:
  - embedded usa stores in-memory;
  - standalone usa `StorageRoot/panels/panels.json`, `StorageRoot/media/<sha256>.<ext>` e `StorageRoot/media/media-index.json`.
- APIs admin/client novas:
  - `GET /api/v1/admin/library/panels`
  - `PUT /api/v1/admin/library/panels`
  - `POST /api/v1/admin/library/media`
  - `GET /api/v1/admin/library/media/{mediaId}`
  - `DELETE /api/v1/admin/library/media/{mediaId}`
- Upload de midia respeita `MaxMediaUploadBytes` (`MICA_SERVER__MAXMEDIAUPLOADBYTES`, default `20971520`) e deduplica blobs por `SHA-256`.

## Atualizacao 2026-04 - Direct LAN Visual + Stable Device Identity

- `DeviceMac` virou a identidade primaria de re-registro LAN. Um flash com NVS preservada continua usando `deviceId/token`; um flash limpo com NVS apagada redescobre o servidor por MAC e recebe o mesmo `deviceId/token`.
- `/api/v1/pair` permanece legado, mas agora tambem aceita `DeviceMac` e reutiliza o registro existente quando o MAC ja esta cadastrado.
- A telemetria MQTT aceita `deviceMac`; se um registro legado autenticado ainda nao tiver MAC, o servidor faz backfill apenas nesse registro autenticado.
- Registros antigos offline sem MAC nao sao mesclados automaticamente por IP, porque `LastKnownIp` pode ser NAT/bridge de Docker ou DHCP reutilizado.
- `DeviceRecord` e `DeviceSnapshot` separam `LanIpAddress` de `LastKnownIp`:
  - `LanIpAddress` vem de `deviceIp` no discovery ou `ipAddress` na telemetria do firmware;
  - `LastKnownIp` continua representando o IP observado pela conexao e pode ser `172.17.0.1` em Docker.
- `GET /api/v1/admin/visual-endpoints` retorna somente devices online no control plane MQTT, UDP-capable, com token e `LanIpAddress` valido.
- `Device.Client.Remote` usa esse endpoint para enviar `StreamFrameV2/3` tipo `Bins128` direto do WinUI para o ESP via `VisualUdpFrameV1` autenticado por HMAC com o token do device.
- Se `/api/v1/admin/visual-endpoints` retornar `404`, a causa provavel e container/servidor antigo: o cliente nao descobriu endpoint LAN algum e deve orientar redeploy do `MicaAudio.Server`.
- O fallback por `/ws/v1/admin/frames` continua para `Frame128x64`, payloads grandes, GIF/painel, endpoint ausente ou erro UDP.
- O caminho Docker local padrao continua com UDP visual server->ESP desligado; o hot path remoto normal nao depende do container repassar frames visuais.

## Atualizacao 2026-04 - Painel LAN Sempre Ligado + Estado Ativo

- `PanelLibraryDocument` passou a carregar `ActivePanels`, uma lista por device com `DeviceId`, `ActivePanelId`, `ActiveAppId`, `LastServerOwnedPanelId` e `UpdatedAtUtc`.
- `PanelWidgetItem.DataSource` formaliza a origem de dados do widget: `server`, `windows-client`, `android-client` ou `device`.
- `server` e o caminho esperado para relogio, clima configurado no servidor, GIF/imagem e status simples que devem continuar conhecidos apos o cliente fechar.
- `windows-client` e `android-client` representam fontes efemeras como audio ao vivo e metricas locais; quando o cliente dono desconectar, o painel deve voltar ao ultimo estado server-owned valido.
- O WinUI Remote permanece cliente do control plane para descobrir/autenticar devices, mas o visualizador de audio usa UDP direto para `Bins128`; `/ws/v1/admin/frames` fica como fallback tecnico.
- OTA continua no control plane existente: o app resolve o firmware oficial, envia `update_firmware` via servidor e acompanha progresso por `command-events`.

## Atualizacao 2026-04 - Server Standalone + Docker/Render Smoke

- `MicaAudio.Server` e o primeiro executavel standalone do server e reutiliza `DeviceServerHost` como implementacao real de HTTP/WS/MQTT.
- O host standalone registra stores in-memory para estado efemero, persiste `DeviceRecord` em JSON simples via `StandaloneDeviceRegistryStore`, persiste biblioteca/midia em `StorageRoot` e aceita configuracao por `MICA_SERVER__*`.
- A porta HTTP continua `5272` por default, mas o env `PORT` tem precedencia para Render; `render.yaml` configura `StorageRoot=/data` e `RestrictToPrivateNetworks=false`.
- O startup pode gerar um pair code transitorio em log/console (`StartupPairCodeTtlSeconds`, default `0`, valor maior que `0` liga) para compatibilidade tecnica.
- O fluxo WinUI embedded permanece o default do app desktop; este corte nao cria client remoto nem muda firmware, endpoints, payloads, MQTT topics ou auth.

## Admin API Remota

Esta secao ancora os DTOs e handlers admin remotos. O historico operacional detalhado fica na atualizacao abaixo.

## Atualizacao 2026-04 - Admin API E WinUI Remote

- `ServerConfig.AdminToken` habilita a Admin API; quando vazio, `/api/v1/admin/*` e `/ws/v1/admin/*` retornam `admin_api_disabled`.
- A autenticacao admin aceita `Authorization: Bearer <token>` ou `X-Mica-Admin-Token`, separada da autenticacao de device.
- Endpoints remotos adicionados:
  - `GET /api/v1/admin/devices`
  - `POST /api/v1/admin/pairing-codes`
  - `DELETE /api/v1/admin/devices/{deviceId}`
  - `POST /api/v1/admin/devices/{deviceId}/commands/tracked`
  - `POST /api/v1/admin/panels/batches/{deviceId}/{panelsSessionId}/{batchSequence}`
  - `DELETE /api/v1/admin/panels/batches/{deviceId}`
  - `GET /api/v1/admin/library/panels`
  - `PUT /api/v1/admin/library/panels`
  - `POST /api/v1/admin/library/media`
  - `GET /api/v1/admin/library/media/{mediaId}`
  - `DELETE /api/v1/admin/library/media/{mediaId}`
  - `GET /api/v1/admin/visual-endpoints`
- `WS /ws/v1/admin/events` publica JSON de `devices_changed`, `device_log`, `command_progress` e `heartbeat`.

## Admin WebSocket Frames

- `WS /ws/v1/admin/frames` recebe envelope binario admin -> server e chama `BroadcastFrame` ou `SendFrame` sem mudar `StreamFrameV2`.
- `Device.Client.Remote` implementa `IDeviceServerClient` via HTTP Admin API e `IDeviceFrameTransport` hibrido: UDP direto para `Bins128` com endpoint visual LAN valido, WebSocket admin como fallback.

## Atualizacao 2026-04 - Visual UDP LAN Opt-In

- `ServerConfig.VisualUdpPort` usa default `5274` e `ServerConfig.PreferLanUdpVisualTransport` liga a preferencia UDP de forma explicita.
- O firmware anuncia `visualUdpSupported`, `visualUdpPort` e `visualUdpMode` pela telemetria; campos ausentes continuam sendo firmware legado.
- `DeviceServerHost` tenta UDP server->ESP apenas quando opt-in esta ligado, o device esta online no control plane, possui `LanIpAddress` LAN, tem token valido e anunciou `visualUdpMode = bins128`.
- O envelope `VisualUdpFrameV1` usa `magic/version/sequence/payloadLength/payload/tag`, com `tag = HMAC-SHA256` truncado pelo token do device.
- UDP v1 aceita apenas `StreamFrameV2.Bins128`; `Frame128x64 RGB565` permanece em WS/WebP batch para evitar fragmentacao IP.
- `DeviceFrameConnection` manteve `DropOldest`, mas a fila visual passou a default `3` para absorver jitter curto sem acumular latencia longa.
- O WebSocket admin de frames deixou de montar `MemoryStream`/slices intermediarios e passa a parsear envelopes com buffers alugados e `ReadOnlySpan<byte>`.
- `Device.Client.Remote` aluga o buffer do envelope admin para reduzir alocacoes no hot path remoto.
- Render/cloud continuam em HTTPS/WSS; UDP e apenas otimizacao local para PC/ESP na mesma LAN.

## Politicas de seguranca

1. Rate limiting:
- `/api/v1/pair` (janela por minuto)
- `/api/v1/device/command-ack` (janela por segundo)
- handshake de `/ws/v1/stream` (janela por minuto)

2. Rede permitida:
- loopback sempre liberado;
- por padrao apenas IP privado quando `RestrictToPrivateNetworks=true`;
- allowlist CIDR opcional em `AllowedCidrs`.

3. Autenticacao:
- Discovery LAN: autenticacao inicial implicita por rede confiavel, gated por `TrustedLanAutoRegistration`; desabilitar em redes nao confiaveis.
- HTTP (`/api/v1/*`): aceita somente `X-Device-Token` ou `Authorization: Bearer`.
- `latest/download` de firmware reaproveitam a mesma autenticacao de device (`X-Device-Id` + `X-Device-Token` ou bearer).
- WebSocket (`/ws/v1/stream`): aceita `X-Device-Id` + `X-Device-Token` (ou `Authorization: Bearer`).
- MQTT: exige `clientId = username = deviceId` e `password = token`.
- Admin remoto: exige `AdminToken` configurado e token via bearer ou `X-Mica-Admin-Token`; nao aceita token admin por query string.
- Query token legado em WS permanece disponivel apenas por compatibilidade quando `AllowLegacyWebSocketQueryToken=true`.
- Default de seguranca: `AllowLegacyWebSocketQueryToken=false`.

4. Limites de payload:
- body JSON limitado por `MaxJsonBodyBytes` (default 64KB).
- upload de midia admin limitado por `MaxMediaUploadBytes` (default 20MB).
- mensagem WS limitada por `MaxWebSocketMessageBytes` (default 64KB).
- mensagens WS fragmentadas sao reagrupadas ate `EndOfMessage`.
- UDP visual v1 e LAN-only, autenticado por HMAC truncado com token do device e restrito a `Bins128`.

## Pontos de alteracao frequente

- Novos comandos (`DeviceCommandType` + `CommandTypeToWire`).
- Endpoint novo em `/api/v1/*`.
- Politica de timeout/comando e progresso.
- Estrutura de DTOs em `Device.Protocol/Models`.
- Topicos e autenticacao do control plane MQTT.
- Politicas de seguranca em `ServerConfig`.
- Superficie admin remota em `DeviceServerHost.Admin`.
- Normalizacao de runtime em `DeviceServerRuntimeConfig`.
- Transicoes de estado em `DeviceRecordMutations` e `DeviceSessionState`.
- Envelope visual UDP em `VisualUdpFrameV1` e receiver firmware `mica_visual_udp.cpp`.
- Ownership/shadow do device em `DeviceSessionShadowMessage`, `StreamFrameV3` e `mica_session.cpp`.

## Riscos e efeitos colaterais

- Mudanca no wire protocol exige compatibilidade com firmware.
- Timeout curto demais gera falso offline.
- Mudanca de token/session pode invalidar devices em campo.
- Filtro de rede/CIDR mal configurado pode bloquear dispositivos legitimos.

## Atualizacao 2026-04 - Gap map cloud-first

- O contrato atual permanece preservado: HTTP/WS/MQTT local continuam sendo o baseline operacional.
- A preparacao para cloud-first foi documentada em [cloud-first-control-plane-gap-map](../reference/cloud-first-control-plane-gap-map.md#objetivo).
- O mapa separa inventario atual, gaps para `HTTPS/WSS`, DTOs candidatos a evolucao e fases recomendadas.
- Nenhuma mudanca de wire, firmware, endpoints ou topicos MQTT foi feita nesta entrega documental.

## Atualizacao 2026-04 - Transporte De Lotes WebP Para Paineis

- `Paineis` ganhou um caminho `monitor-first` alternativo ao `Frame128x64` continuo:
  - o host continua compositor autoritativo do canvas `128x64`;
  - o batch scheduler renderiza `30` frames futuros (`1 s`) e os codifica em `WebP` animado lossless;
  - o host usa `IPanelsBatchStore` para manter os batches apenas em memoria por `deviceId + panelsSessionId + batchSequence`;
  - o device baixa o batch por HTTP autenticado e toca localmente uma unica vez.
- O comando tracked novo e `queue_panels_batch` e usa `PanelsBatchCommandPayload` com:
  - `panelsSessionId`
  - `batchSequence`
  - `downloadUrl`
  - `sha256`
  - `fileSizeBytes`
  - `contentType`
  - `frameCount`
  - `durationMs`
- `batchSequence` e monotono por sessao e define a ordenacao `ativo -> proximo`.
- `DeviceServerHost.RegisterPanelsBatch(...)` salva o payload no store, recebe metadata calculada (`sha256`, tamanho e duracao) e monta a URL autenticada do download.
- `ClearPanelsBatches(...)` e chamado no teardown/fallback para evitar reter batches alem do necessario.
- Compatibilidade:
  - o fluxo WS binario `Frame128x64` continua intocado;
  - o host so tenta `WebP batch` quando o snapshot do device anuncia `animatedWebpBatchSupported = true`.

## Checklist apos alteracao

- Subir app e validar `/api/v1/health`.
- Validar pareamento com e sem `BoardModel`/`PanelType`.
- Validar telemetria atualizando metadados de hardware.
- Validar pareamento em burst (429 esperado no abuso).
- Confirmar que stream continua estavel.

## Observabilidade tecnica

- O host embutido passou a reutilizar o `Serilog` global do processo via `DeviceServerObservability.ConfigureLogging(builder.Logging)`.
- O `OpenTelemetry` do host continua desligado por default e so e configurado quando `OTEL_EXPORTER_OTLP_ENDPOINT` existe no ambiente.
- O provider do host e isolado do provider do app:
  - host escuta `AspNetCore` + `DeviceServerObservability.ActivitySourceName/MeterName`;
  - app escuta `HttpClient` + fontes/meters do `AppObservability`.
- `SendTrackedCommandCoreAsync` agora abre span manual para o comando tracked e o mantem vivo ate `ACK`, timeout, cancelamento ou falha de envio.
- `DeviceServerHost.Advanced` anexa `ActivityEvent`s de progresso/conclusao no mesmo span do `TrackedCommandState` para manter a correlacao `deviceId + commandId`.
- Metricas customizadas adicionadas ao host:
  - `mica.device.command.duration`
  - `mica.device.command.timeout.count`
  - `mica.device.command.failure.count`
- `DeviceServerHost.LogMessage` continua alimentando a UI, mas agora tambem replica a mensagem para a trilha estruturada de engenharia.

## Atualizacao 2026-03 - Observabilidade nativa por device

- O broker MQTT do host ganhou dois canais adicionais:
  - `mica/v1/devices/{deviceId}/stats`
  - `mica/v1/devices/{deviceId}/logs`
- `stats` e retained e atualiza `DeviceRecord`/`DeviceSnapshot` com identidade e capacidade do firmware.
- `logs` nao e retained e chega ao app como `DeviceLogMessage`, ja normalizado pelo host.
- Payload invalido de `stats/logs` e rejeitado em `HandleMqttInterceptingPublishAsync(...)`.
- A referencia de contrato desta entrega esta em [device-observability-dashboard](../reference/device-observability-dashboard.md#objetivo).

## Atualizacao 2026-03 - Dashboard local servido para WebView2

- O host passou a copiar `wwwroot/dashboard/*` para o output do processo.
- `GET /dashboard` e tratado antes do middleware de static files e redireciona para `/dashboard/index.html`.
- O JavaScript servido localmente conecta em `WS /ws/device/{deviceId}`.
- O websocket do dashboard:
  - nao reutiliza autenticacao de device;
  - continua limitado a loopback/rede permitida pela politica global do host;
  - envia um DTO pronto para renderizacao, sem expor `DeviceSnapshot` bruto ao HTML.
- O DTO do dashboard inclui:
  - identidade do device e app ativo;
  - `firmwareVersion`, `latestFirmwareVersion`, `firmwareUpdateSupported` e `firmwareUpdateAvailable`;
  - brilho solicitado/aplicado/cap;
  - `loopHealthyPercent` como metrica oficial de saude do device;
  - `chipTemperatureCelsius` como leitura atual do sensor interno do chip;
  - percentuais de heap e PSRAM calculados no servidor;
  - `hub75Fps`, calculado pelo host a partir do delta de `StreamFramesApplied`.

## Atualizacao 2026-03 - Catalogo oficial + OTA por device

- O host passou a aceitar um catalogo neutro de firmware oficial (`IDeviceOfficialFirmwareCatalog`) alimentado pelo pacote precompilado local do app.
- A comparacao de update e deliberadamente simples:
  - sem pacote oficial compativel -> sem CTA;
  - `FirmwareVersion` vazio ou diferente da versao oficial -> update disponivel;
  - `FirmwareVersion` igual -> device atualizado.
- O mesmo catalogo alimenta tres superficies:
  - DTO do dashboard (`latestFirmwareVersion`, `firmwareUpdateSupported`, `firmwareUpdateAvailable`);
  - dialogo nativo no `WebView2` via bridge `update-firmware`;
  - endpoints OTA autenticados consumidos pelo firmware.
- O comando wire novo `update_firmware` segue o mesmo pipeline tracked de `commands` + `command-events`.
- O fluxo tracked de OTA foi ajustado para `Safe update mode`:
  - `rebooting` nao conclui mais o comando;
  - o host considera sucesso apenas quando recebe `validated`;
  - `rolled-back` encerra o comando como falha reportada pelo device;
  - `timeout` continua sendo falha quando nao houver confirmacao final no prazo.
- O device reutiliza o mesmo `commandId` antes e depois do reboot:
  - a imagem nova publica `pending-verify` e depois `validated`;
  - a imagem antiga pode publicar `rolled-back` quando o bootloader reverter a OTA.

## Referencias de codigo

- [IDeviceServerHost](../../../src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs#L1) - assinatura: `public interface IDeviceServerHost`
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.Advanced](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.Firmware](../../../src/Device.Server/Hosting/DeviceServerHost.Firmware.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.Mqtt](../../../src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.PanelsBatches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.Routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.Dashboard](../../../src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [MicaAudio.Server](../../../src/MicaAudio.Server/MicaAudio.Server.csproj#L1) - assinatura: `<Project Sdk="Microsoft.NET.Sdk.Web">`
- [MicaAudioServerBootstrap](../../../src/MicaAudio.Server/MicaAudioServerBootstrap.cs#L1) - assinatura: `public static class MicaAudioServerBootstrap`
- [MicaAudioServerRuntime](../../../src/MicaAudio.Server/MicaAudioServerRuntime.cs#L1) - assinatura: `public sealed partial class MicaAudioServerRuntime`
- [StandaloneDeviceRegistryStore](../../../src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs#L1) - assinatura: `public sealed class StandaloneDeviceRegistryStore`
- [Render Blueprint](../../../render.yaml#L1) - assinatura: `services:`
- [IPanelsBatchStore](../../../src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs#L1) - assinatura: `public interface IPanelsBatchStore`
- [PanelsBatchWrite](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchWrite.cs#L1) - assinatura: `public sealed record PanelsBatchWrite`
- [PanelsBatchEntry](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchEntry.cs#L1) - assinatura: `public sealed record PanelsBatchEntry`
- [InMemoryPanelsBatchStore](../../../src/Device.Server/Hosting/InMemoryPanelsBatchStore.cs#L1) - assinatura: `public sealed class InMemoryPanelsBatchStore`
- [IPanelLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IPanelLibraryStore.cs#L1) - assinatura: `public interface IPanelLibraryStore`
- [IMediaLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IMediaLibraryStore.cs#L1) - assinatura: `public interface IMediaLibraryStore`
- [InMemoryPanelLibraryStore](../../../src/Device.Server/Hosting/InMemoryPanelLibraryStore.cs#L1) - assinatura: `public sealed class InMemoryPanelLibraryStore`
- [InMemoryMediaLibraryStore](../../../src/Device.Server/Hosting/InMemoryMediaLibraryStore.cs#L1) - assinatura: `public sealed class InMemoryMediaLibraryStore`
- [StandalonePanelLibraryStore](../../../src/MicaAudio.Server/StandalonePanelLibraryStore.cs#L1) - assinatura: `public sealed class StandalonePanelLibraryStore`
- [StandaloneMediaLibraryStore](../../../src/MicaAudio.Server/StandaloneMediaLibraryStore.cs#L1) - assinatura: `public sealed class StandaloneMediaLibraryStore`
- [IDevicePairingStore](../../../src/Device.Server.Abstractions/Hosting/IDevicePairingStore.cs#L1) - assinatura: `public interface IDevicePairingStore`
- [InMemoryDevicePairingStore](../../../src/Device.Server/Hosting/InMemoryDevicePairingStore.cs#L1) - assinatura: `public sealed class InMemoryDevicePairingStore`
- [ICommandStateStore](../../../src/Device.Server.Abstractions/Hosting/ICommandStateStore.cs#L1) - assinatura: `public interface ICommandStateStore`
- [TrackedCommandState](../../../src/Device.Server.Abstractions/Hosting/TrackedCommandState.cs#L1) - assinatura: `public sealed class TrackedCommandState`
- [InMemoryCommandStateStore](../../../src/Device.Server/Hosting/InMemoryCommandStateStore.cs#L1) - assinatura: `public sealed class InMemoryCommandStateStore`
- [ISessionStateStore](../../../src/Device.Server.Abstractions/Hosting/ISessionStateStore.cs#L1) - assinatura: `public interface ISessionStateStore`
- [DeviceSessionState](../../../src/Device.Server.Abstractions/Hosting/DeviceSessionState.cs#L1) - assinatura: `public sealed class DeviceSessionState`
- [InMemorySessionStateStore](../../../src/Device.Server/Hosting/InMemorySessionStateStore.cs#L1) - assinatura: `public sealed class InMemorySessionStateStore`
- [DeviceFrameConnection](../../../src/Device.Server/Hosting/DeviceFrameConnection.cs#L1) - assinatura: `internal sealed class DeviceFrameConnection`
- [DeviceFrameConnectionRegistry](../../../src/Device.Server/Hosting/DeviceFrameConnectionRegistry.cs#L1) - assinatura: `internal sealed class DeviceFrameConnectionRegistry`
- [VisualUdpSender](../../../src/Device.Server/Hosting/VisualUdpSender.cs#L1) - assinatura: `internal sealed class SocketVisualUdpSender`
- [DeviceOfficialFirmwareCatalog](../../../src/Device.Server.Abstractions/Hosting/DeviceOfficialFirmwareCatalog.cs#L1) - assinatura: `public interface IDeviceOfficialFirmwareCatalog`
- [IDeviceFrameTransport](../../../src/Device.Client.Abstractions/IDeviceFrameTransport.cs#L1) - assinatura: `public interface IDeviceFrameTransport`
- [IDeviceServerClient](../../../src/Device.Client.Abstractions/IDeviceServerClient.cs#L1) - assinatura: `public interface IDeviceServerClient`
- [PanelsBatchRegistration](../../../src/Device.Client.Abstractions/PanelsBatchRegistration.cs#L1) - assinatura: `public sealed record PanelsBatchRegistration`
- [EmbeddedDeviceServerClient](../../../src/Device.Client.Embedded/EmbeddedDeviceServerClient.cs#L1) - assinatura: `public sealed partial class EmbeddedDeviceServerClient`
- [EmbeddedDeviceServerClientOptions](../../../src/Device.Client.Embedded/EmbeddedDeviceServerClientOptions.cs#L1) - assinatura: `public sealed class EmbeddedDeviceServerClientOptions`
- [NetworkInterfaceEmbeddedDevicePublicHostResolver](../../../src/Device.Client.Embedded/NetworkInterfaceEmbeddedDevicePublicHostResolver.cs#L1) - assinatura: `public sealed class NetworkInterfaceEmbeddedDevicePublicHostResolver`
- [DeviceServerObservability](../../../src/Device.Server/Hosting/DeviceServerObservability.cs#L1) - assinatura: `internal static class DeviceServerObservability`
- [DeviceServerRuntimeConfig](../../../src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs#L1) - assinatura: `internal sealed class DeviceServerRuntimeConfig`
- [DeviceMqttTopics](../../../src/Device.Server/Hosting/DeviceMqttTopics.cs#L1) - assinatura: `internal static class DeviceMqttTopics`
- [PairDeviceRequest](../../../src/Device.Protocol/Models/PairDeviceRequest.cs#L1) - assinatura: `public sealed class PairDeviceRequest`
- [PairDeviceResponse](../../../src/Device.Protocol/Models/PairDeviceResponse.cs#L1) - assinatura: `public sealed class PairDeviceResponse`
- [MicaDiscoveryRequestV1](../../../src/Device.Protocol/Models/MicaDiscoveryRequestV1.cs#L1) - assinatura: `public sealed class MicaDiscoveryRequestV1`
- [MicaDiscoveryResponseV1](../../../src/Device.Protocol/Models/MicaDiscoveryResponseV1.cs#L1) - assinatura: `public sealed class MicaDiscoveryResponseV1`
- [PanelLibraryDocument](../../../src/Device.Protocol/Models/PanelLibraryDocument.cs#L1) - assinatura: `public sealed class PanelLibraryDocument`
- [PanelDeviceState](../../../src/Device.Protocol/Models/PanelDeviceState.cs#L1) - assinatura: `public sealed class PanelDeviceState`
- [PanelLibraryItem](../../../src/Device.Protocol/Models/PanelLibraryItem.cs#L1) - assinatura: `public sealed class PanelLibraryItem`
- [PanelWidgetItem](../../../src/Device.Protocol/Models/PanelWidgetItem.cs#L1) - assinatura: `public sealed class PanelWidgetItem`
- [PanelWidgetDataSources](../../../src/Device.Protocol/Models/PanelWidgetDataSources.cs#L1) - assinatura: `public static class PanelWidgetDataSources`
- [MediaAssetInfo](../../../src/Device.Protocol/Models/MediaAssetInfo.cs#L1) - assinatura: `public sealed class MediaAssetInfo`
- [ServerInfoResponse](../../../src/Device.Protocol/Models/ServerInfoResponse.cs#L1) - assinatura: `public sealed class ServerInfoResponse`
- [DevicePresenceMessage](../../../src/Device.Protocol/Models/DevicePresenceMessage.cs#L1) - assinatura: `public sealed class DevicePresenceMessage`
- [DeviceTelemetryMessage](../../../src/Device.Protocol/Models/DeviceTelemetryMessage.cs#L1) - assinatura: `public sealed class DeviceTelemetryMessage`
- [DeviceSessionShadowMessage](../../../src/Device.Protocol/Models/DeviceSessionShadowMessage.cs#L1) - assinatura: `public sealed class DeviceSessionShadowMessage`
- [StreamFrameV3](../../../src/Device.Protocol/Stream/StreamFrameV3.cs#L1) - assinatura: `public static class StreamFrameV3`
- [VisualUdpFrameV1](../../../src/Device.Protocol/Stream/VisualUdpFrameV1.cs#L1) - assinatura: `public static class VisualUdpFrameV1`
- [Firmware UDP visual receiver](../../../firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp#L1) - assinatura: `void processVisualUdpReceiver()`
- [Firmware session runtime](../../../firmware/esp32s3-devkitc1/src/mica_session.cpp#L1) - assinatura: `void processClientSessionRuntime(...)`
- [PanelsBatchCommandPayload](../../../src/Device.Protocol/Models/PanelsBatchCommandPayload.cs#L1) - assinatura: `public sealed class PanelsBatchCommandPayload`
- [DeviceFirmwareReleaseInfo](../../../src/Device.Protocol/Models/DeviceFirmwareReleaseInfo.cs#L1) - assinatura: `public sealed class DeviceFirmwareReleaseInfo`
- [DeviceStatsMessage](../../../src/Device.Protocol/Models/DeviceStatsMessage.cs#L1) - assinatura: `public sealed class DeviceStatsMessage`
- [DeviceLogMessage](../../../src/Device.Protocol/Models/DeviceLogMessage.cs#L1) - assinatura: `public sealed class DeviceLogMessage`
- [DeviceRecord](../../../src/Device.Protocol/Models/DeviceRecord.cs#L1) - assinatura: `public sealed class DeviceRecord`
- [DeviceSnapshot](../../../src/Device.Protocol/Models/DeviceSnapshot.cs#L1) - assinatura: `public sealed class DeviceSnapshot`
- [ServerConfig](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L1) - assinatura: `public sealed class ServerConfig`

## Backlinks no codigo

- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs`
- `src/MicaAudio.Server/MicaAudioServerOptions.cs`
- `src/MicaAudio.Server/MicaAudioServerRuntime.cs`
- `src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs`
- `src/Device.Server/Hosting/InMemoryPanelsBatchStore.cs`
- `src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs`
- `src/Device.Server.Abstractions/Hosting/PanelsBatchWrite.cs`
- `src/Device.Server.Abstractions/Hosting/PanelsBatchEntry.cs`
- `src/Device.Server/Hosting/InMemoryDevicePairingStore.cs`
- `src/Device.Server.Abstractions/Hosting/IDevicePairingStore.cs`
- `src/Device.Server/Hosting/InMemoryCommandStateStore.cs`
- `src/Device.Server.Abstractions/Hosting/ICommandStateStore.cs`
- `src/Device.Server.Abstractions/Hosting/TrackedCommandState.cs`
- `src/Device.Server/Hosting/InMemorySessionStateStore.cs`
- `src/Device.Server.Abstractions/Hosting/ISessionStateStore.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceSessionState.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server/Hosting/DeviceFrameConnection.cs`
- `src/Device.Server/Hosting/DeviceFrameConnectionRegistry.cs`
- `src/Device.Server/Hosting/VisualUdpSender.cs`
- `src/Device.Protocol/Stream/VisualUdpFrameV1.cs`
- `firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs`
- `src/Device.Server/Hosting/InMemoryPanelLibraryStore.cs`
- `src/Device.Server/Hosting/InMemoryMediaLibraryStore.cs`
- `src/Device.Server.Abstractions/Hosting/IPanelLibraryStore.cs`
- `src/Device.Server.Abstractions/Hosting/IMediaLibraryStore.cs`
- `src/MicaAudio.Server/StandalonePanelLibraryStore.cs`
- `src/MicaAudio.Server/StandaloneMediaLibraryStore.cs`
- `src/Device.Server/Hosting/DeviceMqttTopics.cs`
- `src/Device.Client.Embedded/EmbeddedDeviceServerClient.cs`
- `src/Device.Client.Embedded/NetworkInterfaceEmbeddedDevicePublicHostResolver.cs`
- `src/Device.Protocol/Models/PairDeviceRequest.cs`
- `src/Device.Protocol/Models/PairDeviceResponse.cs`
- `src/Device.Protocol/Models/MicaDiscoveryRequestV1.cs`
- `src/Device.Protocol/Models/MicaDiscoveryResponseV1.cs`
- `src/Device.Protocol/Models/PanelLibraryDocument.cs`
- `src/Device.Protocol/Models/PanelDeviceState.cs`
- `src/Device.Protocol/Models/PanelLibraryItem.cs`
- `src/Device.Protocol/Models/PanelWidgetItem.cs`
- `src/Device.Protocol/Models/PanelWidgetDataSources.cs`
- `src/Device.Protocol/Models/MediaAssetInfo.cs`
- `src/Device.Protocol/Models/ServerInfoResponse.cs`
- `src/Device.Protocol/Models/DevicePresenceMessage.cs`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`

## Atualizacao 2026-03 - MQTT como control plane oficial

- O stream visual permaneceu no WS binario (`/ws/v1/stream`) sem espelhamento para MQTT.
- O control plane oficial agora usa topicos fixos:
  - `mica/v1/devices/{deviceId}/commands`
  - `mica/v1/devices/{deviceId}/command-events`
  - `mica/v1/devices/{deviceId}/status`
  - `mica/v1/devices/{deviceId}/presence`
- `status` e `presence` sao retained; `commands` e `command-events` nao sao retained.
- O snapshot `Online` passou a significar disponibilidade do control plane MQTT.
- Sessao WS sem MQTT passa a ser tratada como firmware legado fora do control plane.
- Telemetria WS-texto ou `command-ack` HTTP sem MQTT ativo passam a marcar o snapshot como `LegacyOnly`.
- `LegacyOnly` nao promove o device a `Online`; ele continua elegivel apenas ao caminho visual/rollback.

## Atualizacao 2026-04 - Endereco anunciado no Docker local

- `ServerConfig.PublicHttpBaseAddress` permite que `MicaAudio.Server` anuncie uma base HTTP publica diferente do bind interno do container.
- `/api/v1/server/info`, `/api/v1/pair` e URLs autenticadas de batch WebP usam a mesma regra de base HTTP:
  - `PublicHttpBaseAddress`, quando configurado;
  - senao `scheme + Request.Host`, preservando a porta externa recebida;
  - senao `PublicHost/ListenHost + Port` como fallback sem contexto HTTP.
- O `MqttHost` anunciado usa `PublicHost`; se vazio, usa o host de `PublicHttpBaseAddress`; se vazio, usa o host da request.
- Em Docker local com `-p 5272:8080`, configurar `MICA_SERVER__PUBLICHTTPBASEADDRESS=http://<IP_DO_PC>:5272` evita que o firmware grave a porta interna `8080`.
- MQTT continua legado/local nesta fase e precisa de publicacao explicita `-p 5273:5273` para o ESP conectar fora do container.
- UDP visual LAN, quando habilitado, tambem precisa de publicacao explicita `-p 5274:5274/udp` no Docker local; nao e caminho Render/cloud.
- UDP discovery LAN, quando habilitado, tambem precisa de publicacao explicita `-p 5275:5275/udp` no Docker local. Render/cloud permanecem sem auto-registro LAN.

## Atualizacao 2026-03 - Presenca Leve e Carimbos de Sessao

- `LastAuthUtc` e preenchido quando a autenticacao WebSocket e concluida com sucesso em `HandleWebSocketAsync`, apos `TryAuthenticate(..., AuthContext.WebSocket, ...)` validar o token.
- `LastTelemetryUtc` e preenchido apenas no processamento de telemetria em `HandleIncomingWsTextAsync`.
- Esses carimbos tem semanticas diferentes e nao devem ser misturados:
  - `LastAuthUtc` = sessao autenticada estabelecida
  - `LastTelemetryUtc` = telemetria recente recebida
- A estrategia deliberadamente continua leve:
  - sem `shadow`
  - sem timeline de lifecycle
  - sem inferir `nao configurado` automaticamente

## Atualizacao 2026-03 - Telemetria v2 pass-through

- A mensagem de telemetria WS agora transporta tambem:
  - `uptimeSeconds`
  - `loopHealthyPercent`
  - `loopLoadPercent`
  - `chipTemperatureCelsius`
  - `freeHeapBytes`
  - `largestHeapBlockBytes`
  - `psramAvailable`
  - `freePsramBytes`
  - `largestPsramBlockBytes`
  - `wifiConnected`
  - `resetReason`
  - `controlQueueDepth`
  - `controlWorkerState`
  - `panelsWorkerState`
  - `lastSlowCommand`
  - `lastSlowCommandDurationMs`
- O payload MQTT `status` e o modelo `DeviceTelemetryMessage` compartilham o mesmo contrato para esses campos.
- O servidor mantem comportamento pass-through para esses campos (sem clamp ou renormalizacao no host).
- `loopLoadPercent` permanece apenas como compatibilidade de leitura; o dashboard WebView2 usa `loopHealthyPercent` para o card e o historico de saude.
- Sanitizacao de `largest*BlockBytes` permanece restrita ao firmware emissor.
- Detalhes de contrato e semantica: [device-telemetry-v2-fields](../reference/device-telemetry-v2-fields.md#objetivo).

## Atualizacao 2026-03 - Brilho + LED auxiliar + heartbeat de telemetria

- O comando wire `set_brightness` foi adicionado para controlar `brightnessCap` por dispositivo.
- O comando wire `test_led` mantem comportamento principal de pulso curto sem parametros.
- `test_led` continua aceitando parametro legado `enabled=true|false` em compatibilidade operacional.
- A telemetria agora inclui:
  - `telemetrySequence`
  - `brightnessCap`
  - `brightnessRequested`
  - `brightnessApplied`
  - `testLedEnabled`
  - `testLedDuty`
  - `testLedAvailable`
- `DeviceServerHost` faz pass-through desses campos para `DeviceRecord`/`DeviceSnapshot`, preservando compatibilidade com firmware legado (campos `nullable`).

## Atualizacao 2026-03 - Mitigacao de flapping de sessao WS

- O detach de socket agora e seguro por identidade da conexao: somente o socket atualmente anexado pode transicionar a sessao para desconectada.
- Foi adicionado grace period curto de 500ms apos detach para absorver reconexoes rapidas sem alternancia visual online/offline na UI.
- O objetivo e reduzir falso flapping quando o firmware reconecta em janela curta.

## Atualizacao 2026-03 - RSK-002 cutover de auth WS

- O fallback de query token legado no WS foi mantido apenas como mecanismo de rollback, mas desligado por default.
- O host carrega `AllowLegacyWebSocketQueryToken` via `settings.json` do app.
- Em incidente de campo, o rollback pode reativar temporariamente o legado sem recompilar:
  - `%AppData%\\MicaAudio\\settings.json`
  - `"AllowLegacyWebSocketQueryToken": true`

## Atualizacao 2026-03 - Hotfix P0 de conectividade (Wi-Fi/AP)

- O protocolo de telemetria manteve compatibilidade e recebeu 5 campos opcionais:
  - `wifiState`
  - `provisioningPortalActive`
  - `auxLedAvailable`
  - `testLedAvailable`
  - `lastWifiEvent`
- `DeviceServerHost` faz pass-through desses campos para `DeviceRecord` e `DeviceSnapshot` sem normalizacao destrutiva.
- `test_led` preserva compatibilidade legado, mas pode responder erro operacional explicito quando nenhum LED de teste esta disponivel no hardware:
  - `errorCode = "test_led_unavailable"`

## Atualizacao 2026-03 - Onboarding USB sem mudanca no wire WS/HTTP

- O onboarding de novo dispositivo continua sem mudanca nos contratos publicos WS/HTTP do servidor.
- O fluxo oficial atual do desktop e `COM -> flash -> pair code -> AP`.
- Nao houve mudanca nos contratos publicos WS/HTTP do servidor:
  - `/api/v1/pair` permanece o endpoint de pareamento;
  - `/ws/v1/stream` permanece para sessao e telemetria.
- `mica.serial.v1` permanece no codigo apenas como compatibilidade/diagnostico entre app e firmware, fora do caminho oficial do wizard.

## Atualizacao 2026-03 - Refactor core-first do host em .NET 10

- `DeviceServerHost` foi reduzido para orquestracao do host ASP.NET Core e passou a mapear endpoints em route groups via `DeviceServerHost.Routes`.
- O estado interno foi separado em colaboradores dedicados:
  - `InMemorySessionStateStore`
  - `DeviceFrameConnectionRegistry`
  - `InMemoryDevicePairingStore`
  - `InMemoryCommandStateStore`
  - `DeviceRecordMutations`
- A logica temporal sensivel agora usa `TimeProvider` em:
  - expiracao de pairing code;
  - janela de tentativas por IP;
  - grace period de detach;
  - snapshots online/offline;
  - espera de comandos tracked fora do caminho `TimeProvider.System`.
- O wire HTTP/WS permaneceu congelado:
  - mesmos paths;
  - mesmos DTOs;
  - mesmos comandos wire.

## Atualizacao 2026-03 - Baseline de observabilidade do host

- `DeviceServerHost.StartAsync` passou a configurar logging estruturado e `OpenTelemetry` no `WebApplication` interno antes de montar o pipeline.
- Requests `/api/v1/*` entram no provider `AspNetCore` do host quando OTLP esta ativo.
- O caminho tracked `dispatch -> ACK` agora registra:
  - span manual por comando;
  - eventos de progresso e conclusao;
  - metricas de duracao, timeout e falha.
- A correlacao atual e por `deviceId` e `commandId`; nao houve alteracao no protocolo com firmware para propagacao W3C trace context.

