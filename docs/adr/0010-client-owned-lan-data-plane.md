# ADR 0010 - Client-owned LAN data plane

## Status

Superseded em 2026-04-30 por `remote-only server panel runtime`.

## Contexto

Esta ADR registrava a direcao `cliente Windows -> ESP32` para o hot path visual de `visualizador` e `Paineis`, com o servidor atuando principalmente como control plane.

Essa decisao foi substituida: o WinUI agora e remote-only e o firmware deve receber dados visuais pelo servidor remoto/standalone. O cliente nao deve conversar diretamente com o ESP nem manter server embedded como fallback.

## Decisao Superseded

A decisao antiga de `client-driven`/`LAN-direct` nao e mais direcao ativa.

## Decisao Atual

1. O fluxo oficial e `WinUI -> MicaAudio.Server -> ESP32`.
2. O WinUI edita, salva e ativa estado remoto por Admin API.
3. O `MicaAudio.Server` e o owner autoritativo do runtime de paineis server-capable.
4. O ESP32 continua como runtime de display conectado ao servidor por MQTT/WS/HTTP, recebendo comandos, batches e frames ja compostos.
5. Widgets dependentes do cliente, como metricas do PC e visualizador de audio, param quando o WinUI fecha; widgets server-capable continuam pelo servidor.

## Consequencias

- `Device.Client.Embedded` foi removido da solution e do composition root WinUI.
- O WinUI nao inicia `DeviceServerHost` in-process.
- Documentacao ativa nao deve recomendar comunicacao direta cliente-ESP.
- O servidor remoto/standalone passa a ser requisito operacional, mesmo quando roda em `localhost:5272`.

## Referencias

- `docs/handoffs/2026-04-30-remote-only-server-panel-runtime.md`
- `docs/wiki/architecture/01-system-overview.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/paineis.md`
