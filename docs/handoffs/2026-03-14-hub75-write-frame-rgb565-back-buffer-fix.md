# Handoff - HUB75 writeFrameRGB565 back-buffer fix

## Objetivo

Corrigir a corrupcao visual do painel HUB75 no caminho `writeFrameRGB565()` migrando a dependencia upstream para `ESP32-HUB75-MatrixPanel-DMA 3.0.13` e alinhando o writer bulk RGB565 com a semantica real de `fb` e `back_buffer_id`, sem tocar em `commitMatrixFrame()` nem na ordem de chamada de `drawFrame128x64()`.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o build oficial usa `ESP32-HUB75-MatrixPanel-DMA` `3.0.13` pinada por SHA;
  - `writeFrameRGB565()` escreve no framebuffer apontado por `fb`, nao escolhe destino por `back_buffer_id`;
  - o log serial mostra `target_buffer_id`, `back_buffer_id` observado e `ROWS_PER_FRAME`;
  - o fluxo `writeFrameRGB565(...) -> shadow memcpy -> commitMatrixFrame()` permanece inalterado no firmware.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/scripts/patch_hub75_bulk_rgb565.py`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-14-hub75-write-frame-rgb565-back-buffer-fix.md`

## Decisoes tomadas

1. A dependencia upstream foi migrada de `3.0.11` para `3.0.13` e fixada pelo SHA `a6221865c71fd5aeba885c31b81fe41bd36c5705`, evitando drift de tag/branch.
2. O patch versionado da lib passou a validar o layout esperado da `3.0.13` antes de aplicar a injecao do writer bulk:
   - `frame_buffer[2]`
   - `fb`
   - `back_buffer_id`
   - `flipDMABuffer()` com alternancia de `back_buffer_id` e `fb`
3. `writeFrameRGB565()` agora captura `target_fb = fb` no inicio, deriva `target_buffer_id` por comparacao de ponteiros e escreve diretamente em `target_fb->rowBits[row_idx]->getDataPtr(...)`.
4. O writer bulk preserva os bits de controle com `BITMASK_RGB12_CLEAR`, mantem o loop `row_idx = 0..ROWS_PER_FRAME-1` e usa o par de linhas `row_idx` / `row_idx + ROWS_PER_FRAME` para o painel `128x64`.
5. O log serial do writer foi limitado por tempo para auditoria de ownership sem inundar a porta serial.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
```

## Riscos e rollback

- Risco principal: a `3.0.13` muda detalhes internos da lib e pode quebrar o patch se o layout divergir do esperado; por isso o script agora falha cedo.
- Risco secundario: o writer bulk continua throughput-first e nao replica a curva `lumConvTab`, entao fluidez e ownership de buffer sao corrigidos sem prometer paridade exata de gamma.
- Rollback:
  - restaurar o pin da dependencia anterior no `platformio.ini`;
  - remover o patch novo ou reverter para o patch anterior;
  - regenerar o build oficial do firmware.

## Proximos passos

1. Gravar o firmware no painel real e verificar na serial se `ROWS_PER_FRAME=32` e se `target_buffer_id` alterna sem escrita no buffer ativo.
2. Validar em hardware com frame cheio, quadrantes e checkerboard em `Frame128x64` para confirmar ausencia de faixas pretas rolantes e tearing.
3. Se a corrupcao persistir com o ownership do back buffer confirmado, auditar a codificacao BCM/bitplane do writer bulk como proximo suspeito.
