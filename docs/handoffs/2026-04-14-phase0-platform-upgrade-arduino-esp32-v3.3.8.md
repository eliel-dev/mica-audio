# Handoff: Phase 0 — Platform Upgrade para Arduino-ESP32 v3.3.8 / ESP-IDF v5.5.4

## Objetivo

Atualizar a plataforma PlatformIO do firmware oficial do ESP32-S3 DevKitC-1 para Arduino-ESP32 v3.3.8 (ESP-IDF v5.5.4), corrigindo todas as breaking changes de API e atualizando as referencias de versao nos documentos de governanca do repositorio.

## Escopo classificado

- Tipo: estrutural (firmware + governanca documental)
- Criterio de aceite: build `esp32s3_devkitc1_dma_exp` compilando sem erros com a nova plataforma, docs de governanca refletindo v5.5.4.

## Arquivos alterados

### firmware/esp32s3-devkitc1/platformio.ini

- `platform` atualizado de `espressif32` (sem pin, v6.13.0 instalada) para `https://github.com/pioarduino/platform-espressif32/releases/download/55.03.38-1/platform-espressif32.zip`
- Resultado: `espressif32@55.3.38`, `framework-arduinoespressif32@3.3.8`, `framework-arduinoespressif32-libs@5.5.4`

### firmware/esp32s3-devkitc1/src/main.cpp

1. **LEDC API** (Arduino-ESP32 v3.x breaking change):
   - `ledcSetup(channel, freq, resolution)` + `ledcAttachPin(pin, channel)` -> `ledcAttach(pin, freq, resolution)`
   - `ledcWrite(channel, duty)` -> `ledcWrite(pin, duty)`
   - Constante `kTestLedPwmChannel` removida (canais nao sao mais usados)

2. **mbedtls 3.x** (sufixo `_ret` removido):
   - `mbedtls_sha256_starts_ret` -> `mbedtls_sha256_starts` (replaceAll)
   - `mbedtls_sha256_update_ret` -> `mbedtls_sha256_update` (replaceAll)
   - `mbedtls_sha256_finish_ret` -> `mbedtls_sha256_finish` (replaceAll, 3 ocorrencias)

3. **neopixelWrite deprecado** (Arduino-ESP32 v3.x):
   - `neopixelWrite(pin, r, g, b)` -> `rgbLedWrite(pin, r, g, b)` (replaceAll, 2 ocorrencias)

### Documentos de governanca (v5.5.3 -> v5.5.4)

- `AGENTS.md` — 3 ocorrencias atualizadas
- `docs/wiki/ai/agent-entrypoint.md` — 3 ocorrencias atualizadas
- `docs/wiki/reference/ai-contract.v1.yaml` — 2 ocorrencias atualizadas
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md` — 2 ocorrencias atualizadas

### Handoff corrigido

- `docs/handoffs/2026-04-14-freertos-ota-background-task.md` — secao `## Arquivo alterado` renomeada para `## Arquivos alterados` (fix de governance check)

## Decisoes tomadas

1. **pioarduino em vez de espressif32 oficial**: a plataforma oficial `espressif32` do PlatformIO ainda nao tem release estavel com Arduino-ESP32 v3.3.x. O fork `pioarduino` e o caminho recomendado pela comunidade para Arduino-ESP32 v3.x no PlatformIO.

2. **Handoffs historicos nao atualizados**: os handoffs de 2026-03 (`2026-03-12-esp32s3-official-sources-policy.md`, `2026-03-16-network-poll-budget-loop-health.md`) mencionam v5.5.3 mas sao registros historicos do que era verdade naquela data. Nao foram alterados.

3. **`security_best_practices_report.md` nao atualizado**: esse relatorio foi gerado em data anterior e referencia v5.5.3 como contexto da epoca. Pode ser regenerado se necessario.

4. **Warning de `NetworkClient::flush()`**: vem da lib WebSockets (nao do nosso codigo). E apenas warning, nao erro. Sera resolvido quando a lib WebSockets atualizar para Arduino-ESP32 v3.x.

## Validacoes executadas

```text
pio run -e esp32s3_devkitc1_dma_exp   -> SUCCESS (28s, 48.1% Flash, 39.0% RAM)
powershell docs-validate.ps1          -> OK (59 wiki files, 499 links, 72 backlinks)
powershell ai-governance-check.ps1    -> OK (29 changed, 20 structural, 9 docs evidence)
```

## Riscos e rollback

| Risco | Severidade | Mitigacao |
|-------|------------|-----------|
| Regressao de comportamento no runtime do Arduino-ESP32 v3.3.8 | Media | Testar no hardware fisico: Wi-Fi, MQTT, WS stream, OTA, HUB75 render |
| Warning de `NetworkClient::flush()` na lib WebSockets | Baixa | Apenas warning, funcionalidade preservada. Monitorar releases da lib |
| pioarduino drift vs upstream | Baixa | Pin exato em `55.03.38-1`. Nao usar `latest`. |

- **Rollback:** reverter `platformio.ini` para `platform = espressif32` e desfazer as 3 substituicoes de API no `main.cpp`.

## Proximos passos

1. Testar firmware no hardware fisico: validar Wi-Fi, MQTT, WS, HUB75, OTA, panels batch.
2. Rebuildar `dma_diag` para confirmar que tambem compila.
3. Iniciar Phase 1: module split do `main.cpp` monolitico em ~10 arquivos.
