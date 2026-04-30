# Referencia - Cloud-first control plane gap map

## Objetivo

Comparar o control plane atual do Mica com o target-state cloud-first antes de qualquer mudanca de wire protocol em `Device.Server`, `Device.Protocol`, firmware ou clientes.

Este documento e preparatorio. Ele nao muda contrato publico, nao define endpoints novos e nao autoriza remover MQTT do runtime local atual.

## Direcao oficial

- O target-state cloud-first do Mica assume `server = control plane`.
- `visualizador` e `Paineis` deixam de ser requisitos do data plane cloud.
- O gap principal deixa de ser "como rotear cada frame pela nuvem" e passa a ser "como expor control plane publico, ownership e assets sem quebrar o data plane LAN local".

## Fontes e escopo

- Target-state canonico: [Cloud-first multi-panel future architecture](../architecture/07-cloud-first-multi-panel-future-architecture.md#protocolo-publico-futuro).
- Plano operacional Render: [Render cloud-first migration plan](../architecture/08-render-cloud-migration-plan.md#objetivo).
- Contrato atual: [Device.Server + Device.Protocol](../modules/device-server-protocol.md#objetivo).
- Entry points atuais:
  - [DeviceServerHost routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1)
  - [DeviceServerHost MQTT](../../../src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs#L1)
  - [DeviceServerHost panels batches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1)
  - [PairDeviceRequest](../../../src/Device.Protocol/Models/PairDeviceRequest.cs#L1)
  - [PairDeviceResponse](../../../src/Device.Protocol/Models/PairDeviceResponse.cs#L1)
  - [ServerInfoResponse](../../../src/Device.Protocol/Models/ServerInfoResponse.cs#L1)
  - [DeviceSnapshot](../../../src/Device.Protocol/Models/DeviceSnapshot.cs#L1)
  - [PanelsBatchCommandPayload](../../../src/Device.Protocol/Models/PanelsBatchCommandPayload.cs#L1)
- Fontes Espressif consultadas para manter qualquer mencao a ESP32-S3 alinhada ao contrato de IA:
  - [ESP-IDF Programming Guide v5.5.4 - ESP32-S3](https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/index.html)
  - [esp-idf docs/en/index.rst v5.5.4](https://github.com/espressif/esp-idf/blob/v5.5.4/docs/en/index.rst)

## Inventario do contrato atual

| Superficie | Estado atual preservado | Gap para cloud-first |
| --- | --- | --- |
| HTTP `/api/v1/pair` | Claim local por `pair code`; retorna `deviceId`, `token`, `HttpBase`, `WsPath`, `MqttHost`, `MqttPort`, `MqttRootTopic` e `MdnsService`. | Pairing/claim futuro precisa ser HTTPS publico e nao deve retornar MQTT como caminho canonico. |
| HTTP `/api/v1/server/info` | Discovery local para firmware/AP portal com HTTP, MQTT e mDNS. | Discovery cloud precisa expor capacidades/URLs publicas e nao depender de mDNS/local LAN. |
| HTTP `/api/v1/device/config` | Config autenticada por device no host local. | Config futura precisa vir de estado duravel do servidor e funcionar fora do processo WinUI. |
| HTTP firmware latest/download | OTA usa catalogo oficial local/app-bound via `IDeviceOfficialFirmwareCatalog`. | Catalogo futuro precisa ser storage-backed e indexado por `boardModel + displayBackend + panelProfileId + firmwareProfile`, publicando somente DevKitC-1 `128x64` e Matrix Portal S3 `64x64`. |
| HTTP panels batches | `DeviceServerHost` guarda batches WebP em memoria por `deviceId + panelsSessionId + batchSequence`. | Cloud precisa blob store/cache; no Render v1 pessoal pode usar disco persistente atras de abstracao, mas batch nao pode depender de memoria do processo. |
| HTTP command-ack | Compatibilidade legada para ACK fora do MQTT moderno. | Deve continuar como transicao/rollback ate existir sessao publica WSS equivalente. |
| MQTT `commands` | Host injeta comandos tracked no topico do device. | MQTT nao deve ser protocolo publico cloud-first; precisa de equivalente WSS/HTTPS antes de virar legado. |
| MQTT `command-events` | Device publica progresso/conclusao por `commandId`. | Eventos futuros devem trafegar em WSS/public session com semantica de progresso preservada. |
| MQTT `status`/`presence` | Fonte oficial de online/offline local; retained. | Presenca futura precisa ser estado de sessao cloud, nao broker local embutido. |
| MQTT `stats`/`logs` | Telemetria estruturada e logs chegam ao host e alimentam snapshot/UI. | Deve migrar para canal publico WSS mantendo campos nullable e compatibilidade com firmware legado. |
| WS `/ws/v1/stream` | Stream visual binario legado/de transicao para o device. | O target-state pode manter WSS publico para casos remotos, mas o caminho oficial de baixa latencia passa a ser `cliente local -> ESP` na LAN. |
| WS `/ws/device/{deviceId}` | Dashboard local/WebView2, DTO dedicado e sem auth de device. | Nao e contrato publico do firmware; precisa permanecer separado de API cloud/admin. |

## Gaps principais

1. **Hosting e ownership**: `DeviceServerHost` ainda nasce dentro do app WinUI; o target-state exige servidor standalone, Dockerizavel e com lifecycle proprio.
2. **Persistencia**: pairing, sessions, batches e catalogo oficial ainda dependem de memoria/processo/app local em pontos criticos; cloud precisa Postgres, Key Value e blob store.
3. **Protocolo publico**: o caminho publico ainda nao esta consolidado em `HTTPS/WSS`; HTTP/WS locais e MQTT embutido continuam sendo o baseline operacional.
4. **Pairing e claim**: `PairDeviceResponse` ainda devolve endpoints MQTT e HTTP locais; o target-state precisa separar claim, credenciais, capacidades e endpoints publicos.
5. **Taxonomia de hardware**: o contrato atual tem `BoardModel` e `PanelType`; o target-state precisa vocabulario explicito para `boardModel`, `displayBackend`, `panelProfileId`, `panelWidth`, `panelHeight` e `firmwareProfile`.
6. **Catalogo de firmware**: a resolucao atual usa pacote oficial embarcado/local; cloud precisa catalogo multi-board/multi-panel restrito as duas combinacoes oficiais e blobs versionados fora do app.
7. **Paineis e midia**: batches WebP atuais sao efemeros e in-memory; cloud precisa blobs cacheaveis por device/painel/perfil/source.
8. **Clients publishers**: Windows hoje e host/publisher local; target-state precisa Windows/Android como clientes remotos que publicam audio/metricas compactas.
9. **Compatibilidade**: MQTT deve continuar funcionando durante a transicao ate existir um caminho WSS equivalente para comandos, eventos, presenca, stats e logs.

## DTOs candidatos a evolucao

| DTO atual | Uso atual | Evolucao candidata |
| --- | --- | --- |
| `PairDeviceRequest` | Envia `PairingCode`, identidade do device, firmware e metadados simples de hardware. | Adicionar de forma compat capacidade declarada, taxonomia de display e claim metadata; nao remover campos atuais nesta fase. |
| `PairDeviceResponse` | Retorna token e endpoints locais HTTP/WS/MQTT. | Preparar resposta para endpoints `https/wss`, expiracao/rotacao de credenciais e ausencia de MQTT como caminho canonico. |
| `ServerInfoResponse` | Informa `HttpBase`, `WsPath`, MQTT e mDNS para setup local. | Separar discovery local de discovery cloud; adicionar capabilities sem depender de mDNS. |
| `DeviceSnapshot` | Snapshot operacional local, com campos nullable para legado e `IsConnected` baseado em MQTT. | Distinguir estado de sessao cloud, control plane legado/local e capacidades de board/painel sem quebrar persistencia atual. |
| `PanelsBatchCommandPayload` | Carrega `downloadUrl`, hash, tamanho e metadados do batch em memoria do host. | Trocar gradualmente URL efemera por blob/cache key storage-backed, preservando download autenticado pelo device. |

## Fases recomendadas

### Fase 0 - Gap map documental

- Manter contratos atuais intocados.
- Consolidar o inventario acima como base para trabalho futuro.
- Validar apenas documentacao.
- Usar o plano Render como roadmap canonico de execucao.

### Fase 1 - Server standalone cloud-ready sem mudar wire

- Separar o runtime do servidor do papel de host obrigatorio do WinUI.
- Introduzir fronteiras para storage duravel/efemero/blob sem trocar endpoints.
- Manter MQTT, HTTP local e WS local funcionando como baseline.

### Fase 2 - Device.Protocol preparado para multi-board/multi-panel

- Evoluir DTOs de forma aditiva para `boardModel`, `displayBackend`, `panelProfileId`, `panelWidth`, `panelHeight` e `firmwareProfile`.
- Manter `BoardModel`/`PanelType` existentes enquanto houver firmware/app legado.
- Filtrar combinacoes invalidas no catalogo antes de expor opcoes para UI/clients.
- Publicar oficialmente apenas `ESP32-S3 DevKitC-1 + 128x64` e `Matrix Portal S3 + 64x64`.

### Fase 3 - Superficie publica HTTPS/WSS

- Criar caminho publico `HTTPS/WSS` para pairing, claim, comandos, eventos, presenca, stats/logs e publishers.
- Manter MQTT como legado/local ate firmware e clientes terem paridade operacional.
- Tratar `/ws/device/{deviceId}` como dashboard local, nao como contrato publico de device.

## Decisoes que ficam fora desta entrega

- Nao alterar firmware ESP32-S3.
- Nao alterar `Device.Protocol` nem criar novos DTOs agora.
- Nao remover MQTT, command-ack legado, WS stream ou dashboard local.
- Nao implementar storage concreto nem deploy Render nesta fase.
- Nao implementar Matrix Portal S3 nesta fase; qualquer trabalho real desse board deve revalidar memoria, flash, pinout e limites DMA contra fontes primarias do fabricante.

## Checklist para a proxima entrega tecnica

- Escolher uma unica fase tecnica para implementar primeiro; recomendacao: Fase 1.
- Definir se o servidor standalone nasce como novo host/processo ou como modo alternativo do host atual.
- Definir interfaces de storage antes de mover panels batches ou catalogo de firmware.
- Escrever testes de compatibilidade garantindo que os endpoints e topicos atuais continuam funcionando durante a transicao.
- Seguir a sequencia completa em [Render cloud-first migration plan](../architecture/08-render-cloud-migration-plan.md#fases-de-migracao).
