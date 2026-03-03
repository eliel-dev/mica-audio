# ADR 0009 - HUB75 128x64 hard cutover DevKitC-1 only

## Contexto

A base estava ancorada em `64x32` em protocolo, simulador, preview local, GIF e catalogo de firmware.

## Decisao

A base passa a tratar `P2.5 128x64, SMD2121, HUB75, 1/32 scan` como unico painel oficial do fluxo ativo.

## Status

Aceito.

## Consequencias

1. `64x32` sai do caminho principal.
2. `StreamFrameV2` vira o protocolo binario ativo.
3. O firmware oficial e `ESP32-S3 DevKitC-1`.
4. O preview HUB75 local e as miniaturas usam grade nativa `128x64`.

## Compatibilidade

1. `StreamFrameV1` permanece como legado.
2. registros antigos com `hub75_64x32` ainda podem ser lidos, mas nao sao mais ofertados no setup oficial.
