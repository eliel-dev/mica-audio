# 08 - Render Cloud-first Migration Plan

> **Status:** plano canonico de migracao / ainda nao implementado.
>
> Este documento transforma o target-state cloud-first em uma sequencia executavel de fases para deploy no Render.
> Ele nao muda wire protocol, firmware, CI ou runtime por si so. Ele define a ordem segura para futuras entregas tecnicas.

## Objetivo

Migrar o Mica para uma arquitetura `server + firmware + clients`, cloud-first e apta a deploy no Render, mantendo compatibilidade operacional durante a transicao.

O alvo principal do servidor e `.NET 10 / ASP.NET Core` em Docker. Mudanca de stack fica fora do caminho principal porque o repositorio ja tem `Device.Server`, `Device.Protocol`, observabilidade e contratos em C#; a extracao para host standalone tem menor risco do que reescrever o control plane antes de estabilizar cloud.

## Direcao oficial

- Render/Fly/cloud entram como control plane publico.
- O hot path visual oficial continua fora do servidor: cliente local captura/processa e fala direto com o ESP na LAN.
- O deploy cloud deixa de carregar a expectativa de transportar frames em tempo real para `visualizador` e `Paineis`.

## Escopo oficial

### Escopo de produto v1

1. O primeiro deploy cloud e `single-tenant pessoal`.
2. O servidor Render e o control plane publico.
3. Windows e o primeiro cliente remoto.
4. Android, Home Assistant e multi-tenant ficam para fases posteriores.
5. Audio bruto nao sobe para a nuvem; o cliente publica apenas payload compacto ou frame ja derivado.

### Escopo oficial de hardware

Somente duas combinacoes sao oficiais no roadmap cloud-first:

| Board | Backend | Painel | Status |
| --- | --- | --- | --- |
| `esp32s3_devkitc1` | `dma_hub75` | `hub75_128x64` | Linha principal |
| `matrixportal_s3` | `protomatter_hub75` | `hub75_64x64` | Segunda combinacao oficial |

Regras fechadas:

1. `ESP32-S3 DevKitC-1` fica oficialmente ligado ao painel `128x64`.
2. `Matrix Portal S3` fica oficialmente ligado ao painel `64x64`.
3. Nenhuma combinacao `64x32` entra no roadmap oficial.
4. `Matrix Portal S3 + 128x64` nao deve ser anunciado, gerado nem selecionavel.
5. O catalogo deve filtrar combinacoes invalidas antes de UI, API ou firmware consumirem variantes.

## Decisoes canonicas

### Servidor

- Manter `.NET 10 / ASP.NET Core` como stack principal.
- Extrair um host standalone de servidor antes de refatorar protocolo publico.
- Preservar `Device.Server` e `Device.Protocol` como base do dominio durante a transicao.
- Manter o servidor embutido no WinUI apenas como modo legado/local ate o cliente remoto ficar estavel.

### Render v1

- Publicar um Render Web Service via Docker.
- Escutar a porta de runtime informada por `PORT`.
- Expor health check HTTP.
- Usar `HTTPS/WSS` como superficie publica.
- Usar Postgres para estado duravel.
- Usar Render Key Value para estado efemero, TTL, sessoes e rate-limit distribuivel.
- Usar disco persistente do Render para blobs apenas no v1 pessoal.

### Blobs

Blobs de firmware, midia e batches WebP devem ficar atras de uma abstracao de storage desde a primeira entrega tecnica.

Implementacao v1:

- disco persistente Render;
- single-instance;
- caminho de migracao documentado para S3-compatible storage.

Regra operacional:

1. O dominio nao deve conhecer path fisico do Render.
2. URLs publicas de download devem ser montadas pelo servidor.
3. Metadados de blob ficam no Postgres.
4. Antes de multi-instance, disco Render deve ser substituido por S3-compatible storage ou equivalente.

### Protocolo

- MQTT continua legado/local durante a transicao.
- A superficie publica converge para `HTTPS/WSS`.
- `PairDeviceResponse` cloud nao deve depender de MQTT.
- Campos MQTT podem continuar existindo em respostas legadas enquanto houver firmware local.
- `/ws/device/{deviceId}` permanece dashboard local/WebView2, nao contrato publico de device.

## Baseline atual

O estado atual relevante para a migracao e:

1. `DeviceServerHost` nasce dentro do `App.WinUI` no fluxo desktop embedded e tambem pode ser iniciado pelo executavel standalone `MicaAudio.Server`.
2. Persistencia de devices fica em JSON local protegido por DPAPI.
3. Pair codes, sessoes, comandos pendentes e batches WebP vivem em memoria do processo; o standalone persiste apenas o registry de devices em JSON local.
4. O catalogo oficial de firmware fica embarcado no app desktop.
5. O control plane operacional usa HTTP local, WS local e MQTT embutido.
6. O hot path visual legado ainda existe em `Esp32S3LedOutput -> DeviceServerHost.BroadcastFrame -> /ws/v1/stream`, mas a direcao oficial passa a ser `cliente LAN -> ESP`.
7. `Paineis` ja tem batch WebP, mas o batch atual e in-memory.
8. `render.yaml` e `src/MicaAudio.Server/Dockerfile` existem para smoke Docker/Render do server standalone.
9. O WinUI possui modo remoto opt-in contra `MicaAudio.Server` via Admin API/WSS, com `Embedded` ainda como default seguro.

## Fases de migracao

### Fase 0 - Documentacao canonica

Objetivo:

- Fixar este plano como contrato de execucao antes de alterar codigo.

Passos:

1. Criar este documento.
2. Atualizar a arquitetura `07` para refletir somente as duas combinacoes oficiais de hardware.
3. Atualizar o gap map do control plane para apontar para este plano.
4. Linkar este documento no indice da wiki.
5. Validar documentacao.

Saida esperada:

- Wiki consistente sobre Render, cloud-first, hardware oficial e ordem de fases.
- Status em 2026-04-22: concluida.

### Fase 1 - Server standalone cloud-ready sem mudar wire

Objetivo:

- Separar o runtime do servidor do papel de host obrigatorio do WinUI.

Passos:

1. Criar um projeto executavel ASP.NET Core para o servidor standalone.
2. Reaproveitar `Device.Server` como biblioteca de hosting/transportes.
3. Mover configuracao de host para options/environment.
4. Permitir `PORT` como fonte de porta quando rodar em cloud.
5. Preservar `/api/v1/*`, `/ws/v1/stream`, MQTT local e dashboard local.
6. Fazer WinUI consumir o servidor via fronteira de cliente quando em modo remoto.
7. Manter modo local legado ate cutover completo.

Gates:

- `dotnet build MicaAudio.sln -c Debug`.
- Teste de `/api/v1/health`.
- Pairing local ainda funcionando.
- WS stream legado ainda funcionando.

Status em 2026-04-22:

- `src/MicaAudio.Server` foi criado como executavel standalone sem dependencia de `App.WinUI`.
- `PORT` tem precedencia sobre a porta configurada e `MICA_SERVER__*` alimenta `ServerConfig`.
- O startup gera pair code transitorio para smoke local quando `StartupPairCodeTtlSeconds > 0`.
- A Admin API tokenizada e os WebSockets admin foram adicionados para o primeiro WinUI remoto (`MICA_SERVER__ADMINTOKEN`).
- `Device.Client.Remote` permite listar devices, gerar pair code, remover device, enviar comandos tracked, registrar batches WebP e enviar frames HUB75 via server standalone.

### Fase 2 - Bootstrap Render

Objetivo:

- Tornar deploy do servidor no Render um caminho oficial.

Passos:

1. Adicionar Dockerfile multi-stage para o servidor standalone.
2. Adicionar `.dockerignore` para reduzir build context.
3. Adicionar `render.yaml` com Web Service Docker, health check e env vars obrigatorias.
4. Configurar shutdown gracioso para conexoes WS.
5. Documentar disco persistente no mount path de blobs.
6. Documentar que disco persistente impede multi-instance e zero-downtime deploy.
7. Criar smoke script para health, server info e conexao WSS basica.

Gates:

- Build Docker local.
- Render deploy responde health check.
- Logs nao expoem secrets.
- WSS usa `wss://` publicamente.

Status em 2026-04-22:

- `.dockerignore`, `src/MicaAudio.Server/Dockerfile` e `render.yaml` foram adicionados.
- `render.yaml` declara `MICA_SERVER__ADMINTOKEN` como secret `sync: false`.
- Docker local agora deve anunciar `MICA_SERVER__PUBLICHTTPBASEADDRESS=http://<IP_DO_PC>:5272` quando `PORT=8080` estiver mapeado para `5272`, deve publicar `5273` se o firmware atual usar MQTT local e pode publicar `5274/udp` quando `PreferLanUdpVisualTransport` estiver ativo para visual LAN.
- O smoke local valida `/api/v1/health` e `/api/v1/server/info`; deploy Render real ainda depende de publicar o repo e aplicar o Blueprint no Dashboard.

### Fase 3 - Persistencia cloud

Objetivo:

- Remover dependencia de memoria/processo para estado que precisa sobreviver a restart.

Passos:

1. Criar fronteiras `IDeviceRegistryStore`, `IFirmwareCatalog`, `IBlobStore` e stores persistiveis para as fronteiras in-memory ja extraidas.
2. Implementar Postgres para devices, pair codes, firmware releases, painel ativo e comandos rastreados.
3. Implementar Key Value para TTL, locks leves, rate-limit e estado efemero de sessao.
4. Implementar blob store em disco Render atras de `IBlobStore`.
5. Mover batches WebP de dicionario em memoria para blob persistido + metadados.
6. Manter fallback in-memory apenas para testes locais.

Gates:

- Restart do processo nao perde devices pareados.
- Batch WebP continua baixavel depois de restart.
- Pair code expira corretamente via storage.
- Testes cobrem stores com fake clock.

### Fase 4 - Pairing cloud single-tenant

Objetivo:

- Criar onboarding cloud com admin token pessoal e pair code efemero.

Passos:

1. Criar admin token server-side para Windows client.
2. Criar endpoint de pair code para cliente autorizado.
3. Criar claim de device usando pair code.
4. Persistir device token no Postgres.
5. Responder `httpsBase` e `wssBase` no caminho cloud.
6. Manter campos MQTT apenas para compatibilidade legado/local.
7. Registrar capacidades declaradas do device durante claim.

Gates:

- Pair code so pode ser criado por cliente autorizado.
- Pair code e uso unico.
- Device claim gera token persistido.
- Device legado ainda pareia no modo local.

### Fase 5 - WSS publico

Objetivo:

- Criar sessao publica canonica para devices e publishers.

Passos:

1. Criar `/ws/v2/device` para device session.
2. Criar `/ws/v2/publisher` para Windows e futuros clients.
3. Transportar presenca, stats, logs e command events por WSS.
4. Transportar comandos com correlacao por `commandId`.
5. Adicionar keepalive e reconnect/backoff documentados.
6. Manter MQTT como legado/local ate paridade operacional.
7. Separar dashboard/admin API de contrato de firmware.

Gates:

- Device reconecta apos deploy/restart.
- Command event preserva semantica de progresso.
- Presenca nao depende de broker MQTT publico.
- Publisher Windows consegue enviar payload compacto ao servidor.

### Fase 6 - Catalogo oficial de firmware

Objetivo:

- Evoluir de pacote unico local para catalogo cloud com duas variantes oficiais.

Passos:

1. Evoluir manifestos de firmware de forma aditiva.
2. Adicionar `displayBackend`, `panelProfileId`, `panelWidth`, `panelHeight` e `firmwareProfile`.
3. Preservar `BoardModel` e `PanelType` ate consumidores legados sairem.
4. Publicar somente:
   - `esp32s3-devkitc1-128x64-dma_exp_merged.bin`;
   - `matrixportal-s3-64x64-protomatter_exp_merged.bin`.
5. Armazenar binarios e manifests no blob store.
6. Fazer OTA resolver release por combinacao oficial.

Gates:

- UI/API nao lista combinacao invalida.
- OTA rejeita board/painel incompativel.
- Manifesto antigo ainda e aceito no modo legado quando necessario.

### Fase 7 - Firmware direct-to-cloud

Objetivo:

- Fazer firmware operar contra `HTTPS/WSS` publico.

Passos:

1. Atualizar portal AP para aceitar URL cloud.
2. Fazer DevKitC-1 usar claim cloud e WSS device session.
3. Preservar OTA safe mode.
4. Manter fallback local/legado para diagnostico enquanto necessario.
5. Criar profile Matrix Portal S3 separado.
6. Validar Matrix Portal S3 com Protomatter ou backend equivalente para `64x64`.
7. Documentar limites de RAM interna, PSRAM, DMA e task stacks para cada board.

Gates:

- DevKitC-1 conecta direto no Render.
- Reset/reboot preserva credenciais e reconecta.
- Matrix Portal S3 exibe `64x64` com backend proprio.
- OTA nao causa rollback falso por falha temporaria de rede.

### Fase 8 - Pipeline server-side de paineis e midia

Objetivo:

- Mover widgets cloud-safe e midia para o servidor.

Passos:

1. Modelar painel como entidade persistida no Postgres.
2. Persistir widgets, configuracoes e composicao.
3. Preprocessar midia por perfil de painel.
4. Cachear resultados em blob store.
5. Gerar batches WebP storage-backed.
6. Manter publishers locais para audio e metricas.
7. Separar `push` efemero, `install` persistente e `preview`.

Gates:

- Painel cloud-safe roda sem WinUI aberto.
- Batch WebP sobrevive a restart.
- Preview e install usam a mesma geometria do device.
- Audio/metricas locais continuam client-owned.

### Fase 9 - Windows remote-first

Objetivo:

- Transformar WinUI em cliente remoto do servidor.

Passos:

1. Adicionar configuracao de server URL e admin token.
2. Criar cliente HTTP/WSS para admin e publisher.
3. Gerar pair code no servidor cloud.
4. Publicar visualizador de audio e metricas locais como payload compacto.
5. Exibir devices/painels a partir do servidor remoto.
6. Manter servidor local como fallback configuravel.
7. Remover dependencia de host embutido do fluxo principal.

Gates:

- WinUI opera sem abrir `DeviceServerHost` local em modo remoto.
- Visualizador envia dados ao device via cloud.
- Devices persistem no servidor, nao em `devices.json`.

### Fase 10 - CI/CD e operacao

Objetivo:

- Tornar o pipeline seguro para cloud, firmware e clients.

Passos:

1. Separar jobs de server cloud, WinUI e firmware.
2. Adicionar build/test Docker do servidor.
3. Adicionar validacao de `render.yaml`.
4. Adicionar firmware matrix somente com as duas combinacoes oficiais.
5. Publicar artefatos de firmware em storage do servidor ou release asset controlado.
6. Rodar smoke tests contra ambiente Render.
7. Documentar rollback de deploy e rollback de firmware.

Gates:

- CI falha se aparecer combinacao de hardware nao oficial no catalogo.
- Deploy Render so ocorre apos checks passarem.
- Smoke cobre health, pairing, WSS, OTA metadata e blob download.

## Interfaces e contratos

DTOs devem evoluir de forma aditiva. Nenhuma fase inicial deve quebrar firmware ou WinUI legado.

Contratos que precisam existir antes de trocar infraestrutura:

- `IBlobStore`: upload, open read, delete, URL publica/autenticada e metadata.
- `IDeviceRegistryStore`: devices, tokens, capacidades, estado duravel e snapshots.
- `IFirmwareCatalog`: releases por board/backend/panel/profile.
- `IDevicePairingStore`: pair codes de uso unico com TTL.
- `ICommandStateStore`: comandos tracked com progresso, timeout e resultado final.
- `ISessionStateStore`: presenca efemera, reconnect e handoff de shutdown.

Estado atual do corte embedded-first:

- `IPanelsBatchStore`, `IDevicePairingStore`, `ICommandStateStore` e `ISessionStateStore` ja existem como fronteiras in-memory first no server embutido.
- `IBlobStore`, `IDeviceRegistryStore` e `IFirmwareCatalog` remoto continuam pendentes para fases posteriores.
- WebSocket/frame stream permanece process-local no `Device.Server` via registry interno de conexoes; o desenho cloud ainda precisa definir WSS publico, reconnect e handoff de shutdown.

Separacoes obrigatorias:

1. `device session`: firmware autenticado.
2. `publisher session`: Windows/Android publicando payload client-owned.
3. `admin/client API`: operacao, pair code, catalogo, paineis e configuracoes.

## Render constraints

Regras de plataforma que afetam o plano:

1. Web services do Render aceitam WebSocket publico.
2. Trafego publico HTTP e WebSocket entra pela mesma porta publica do servico.
3. O app deve escutar a porta indicada por `PORT`.
4. Health check deve responder HTTP `2xx` ou `3xx`.
5. Filesystem sem disco persistente e efemero.
6. Disco persistente preserva arquivos apenas no mount path.
7. Disco persistente prende o servico a single-instance e remove zero-downtime deploy.
8. Estado de sessao relevante deve ir para Key Value ou storage compartilhado antes de escala horizontal.

## Validacao obrigatoria

Fase documental:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
```

Fases estruturais:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
```

Fases com WinUI:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1
```

Fases firmware/protocolo:

1. executar validacoes estruturais;
2. build PlatformIO do profile oficial afetado;
3. validar pairing, WSS, OTA metadata e reconexao em bancada;
4. atualizar backlinks `DOCS:` em arquivos-chave alterados;
5. criar handoff estrutural quando tocar `src/`, `firmware/`, scripts, workflow ou contratos centrais.

## Riscos e rollback

| Risco | Mitigacao | Rollback |
| --- | --- | --- |
| Deploy Render perde WS em troca de instancia | Reconnect/backoff e shutdown gracioso | Voltar para servidor local legado |
| Disco persistente vira gargalo | `IBlobStore` desde o inicio | Migrar blobs para S3-compatible |
| Firmware perde compatibilidade | DTOs aditivos e MQTT legado | Reativar fluxo local HTTP/WS/MQTT |
| Pairing cloud bloqueia device em campo | Pair code uso unico + portal AP editavel | Reprovisionar URL local no portal AP |
| Matrix Portal S3 excede limite de memoria | Backend/profile separado e validacao oficial | Nao publicar artefato Matrix Portal ate bancada aprovar |

## Checklist de aceite do roadmap

- Existe um servidor standalone Dockerizavel.
- Render responde health check e aceita WSS publico.
- Estado duravel sobrevive a restart.
- Blobs nao ficam em memoria do processo.
- Catalogo publica somente as duas combinacoes oficiais.
- DevKitC-1 conecta direto ao cloud.
- Matrix Portal S3 tem profile `64x64` validado.
- WinUI opera como cliente remoto.
- MQTT fica restrito a legado/local.
- CI valida docs, server, firmware oficial e smoke cloud.

## Referencias de codigo

- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1) - assinatura esperada: `public sealed partial class DeviceServerHost`
- [DeviceServerHost routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1) - assinatura esperada: `public sealed partial class DeviceServerHost`
- [DeviceServerHost panels batches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1) - assinatura esperada: `public sealed partial class DeviceServerHost`
- [MicaAudio.Server](../../../src/MicaAudio.Server/MicaAudio.Server.csproj#L1) - assinatura esperada: `<Project Sdk="Microsoft.NET.Sdk.Web">`
- [MicaAudioServerBootstrap](../../../src/MicaAudio.Server/MicaAudioServerBootstrap.cs#L1) - assinatura esperada: `public static class MicaAudioServerBootstrap`
- [MicaAudioServerRuntime](../../../src/MicaAudio.Server/MicaAudioServerRuntime.cs#L1) - assinatura esperada: `public sealed partial class MicaAudioServerRuntime`
- [StandaloneDeviceRegistryStore](../../../src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs#L1) - assinatura esperada: `public sealed class StandaloneDeviceRegistryStore`
- [Render Blueprint](../../../render.yaml#L1) - assinatura esperada: `services:`
- [PairDeviceRequest](../../../src/Device.Protocol/Models/PairDeviceRequest.cs#L1) - assinatura esperada: `public sealed class PairDeviceRequest`
- [PairDeviceResponse](../../../src/Device.Protocol/Models/PairDeviceResponse.cs#L1) - assinatura esperada: `public sealed class PairDeviceResponse`
- [PanelsBatchCommandPayload](../../../src/Device.Protocol/Models/PanelsBatchCommandPayload.cs#L1) - assinatura esperada: `public sealed class PanelsBatchCommandPayload`
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1) - assinatura esperada: `void setup()`
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1) - assinatura esperada: `[platformio]`
- [build-precompiled-firmware.ps1](../../../scripts/build-precompiled-firmware.ps1#L1) - assinatura esperada: `param(`

## Fontes primarias consultadas

- [Render - Docker](https://render.com/docs/docker)
- [Render - WebSockets](https://render.com/docs/websocket)
- [Render - Persistent Disks](https://render.com/docs/disks)
- [Render - Health Checks](https://render.com/docs/health-checks)
- [Render - Postgres](https://render.com/docs/postgresql-creating-connecting)
- [Microsoft Learn - Containerize a .NET app](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container)
- [Microsoft Learn - ASP.NET Core WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)
- [ESP-IDF Programming Guide v5.5.4 - ESP32-S3](https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/index.html)
- [esp-idf docs/en/index.rst v5.5.4](https://github.com/espressif/esp-idf/blob/v5.5.4/docs/en/index.rst)
- [ESP-IDF v5.5.4 - Support for External RAM / ESP32-S3](https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/api-guides/external-ram.html)
- [Adafruit Matrix Portal S3 - official guide](https://learn.adafruit.com/adafruit-matrixportal-s3/overview)
