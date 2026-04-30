# ADR 0010 - Client-owned LAN data plane

## Contexto

O baseline historico do Mica colocava o `Device.Server` no hot path visual entre desktop e ESP32. Esse modelo funcionou como etapa de integracao, mas impunha latencia e acoplamento desnecessarios para `visualizador` e `Paineis`, que sao fluxos sensiveis ao caminho de rede e nao precisam do servidor para transportar cada frame.

Ao mesmo tempo, o projeto passou a precisar de um modelo de ownership por device para suportar multiplos clientes observando o mesmo estado, com takeover fluido no estilo `Spotify Connect`, sem deixar o ESP32 exposto a comandos concorrentes e sem transformar o servidor em roteador obrigatorio do data plane visual.

## Decisao

1. O `server` passa a ser oficialmente `control plane + storage + catalogo + estado duravel`.
2. O `cliente Windows` passa a ser oficialmente o primeiro `edge client` e `data plane LAN`.
3. O `ESP32` passa a ser runtime de execucao/render com ownership explicito por device.
4. `visualizador` e `Paineis` passam a ser topologias `client-driven` e `LAN-direct`:
   - `visualizador`: o cliente captura/processa localmente e envia direto ao ESP;
   - `Paineis`: o cliente baixa config/assets do server, faz cache local e empurra ao ESP.
5. O firmware passa a tratar `MQTT` como plano canonico de sessao:
   - `shadow` retained;
   - `activeClientId`;
   - `last-writer-wins`;
   - `lock com lease`.
6. Ownership e por `device`, com um cliente ativo por vez para modos client-driven.
7. Quando o owner expira, o fallback oficial do device e `relogio + mensagem de cliente desconectado`.
8. `WS/UDP` continuam existindo para dados visuais, mas subordinados ao owner atual.
9. O caminho `server -> WS -> ESP` permanece apenas como baseline de transicao/legado; nao e mais a topologia oficial de baixa latencia.

## Consequencias

- O server deixa de ser gargalo obrigatorio para visualizador/paineis em LAN.
- Cloud/Fly/Render continuam uteis para control plane, sem a exigencia de carregar o hot path visual.
- O firmware ganha responsabilidade adicional de sessao/ownership, inclusive rejeicao de stream stale por `ownerEpoch`.
- Clientes futuros (Windows, Android, Home Assistant) precisam observar `shadow` e respeitar ownership/lease.
- O baseline legado continua existindo durante a transicao, mas a documentacao passa a trata-lo explicitamente como `baseline atual`, nao como direcao final.

## Status

Aceita

## Data

2026-04-23

## Referencias

- docs/wiki/architecture/01-system-overview.md
- docs/wiki/modules/device-server-protocol.md
- firmware/esp32s3-devkitc1/src/mica_session.cpp
- src/Device.Protocol/Stream/StreamFrameV3.cs
