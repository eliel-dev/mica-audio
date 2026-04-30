# Handoff - HUB75 upstream baseline fluidity recovery

## Objetivo

Recuperar fluidez real e reduzir distorcao/ghosting do painel HUB75 `128x64 1/32` no firmware oficial do ESP32-S3, priorizando o baseline upstream da `ESP32-HUB75-MatrixPanel-DMA` sobre o tuning memcalc customizado recente.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o painel continua operando como um unico canvas `128x64` com linha `E` valida;
  - a dependencia `ESP32-HUB75-MatrixPanel-DMA` fica fixada em `3.0.11`;
  - o firmware oficial usa `SHIFTREG`, `double buffer`, `clkphase = false`, `i2sspeed = HZ_10M`, `PIXEL_COLOR_DEPTH_BITS = 6` e `setLatBlanking(2)`;
  - `Bins128` volta a ser reapresentado continuamente na cadencia derivada de `calculated_refresh_rate`;
  - `Frame128x64` continua orientado a frame novo sem exceder a taxa fisica do painel.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-14-hub75-upstream-baseline-fluidity-recovery.md`

## Decisoes tomadas

1. O baseline oficial deixou de depender do `git` flutuante da lib HUB75 e passou a pinar a dependencia em `3.0.11` (`c4ecdcfeeb5aa668d92ddf3c3c74bc93316f6e10`), porque a estabilidade dessa linha foi priorizada sobre o tuning mais agressivo recente.
2. O perfil `6-bit / 144 Hz` deixou de ser baseline oficial. A profundidade de cor `6 bits` foi preservada, mas o forcamento de `min_refresh_rate = 144` foi removido.
3. O baseline do painel passou a assumir `SHIFTREG` como driver oficial desta entrega, isolando qualquer experimento futuro com `FM6124` para uma fase separada.
4. O firmware passou a aplicar `setLatBlanking(2)` por recomendacao direta da README da upstream para casos de ghosting/clones horizontais.
5. O pacing do flip deixou de usar um cap fixo de `20 ms` e passou a derivar a cadencia a partir de `gMatrix->calculated_refresh_rate`.
6. `Bins128` voltou a ser reapresentado continuamente para recuperar a sensacao de fluidez do firmware antigo, sem reintroduzir canvas cortado nem extrapolar a taxa fisica calculada do painel.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> bloqueado localmente porque `src/App.WinUI/bin/.../App.WinUI.exe` estava em uso pelo processo `App.WinUI (PID 1844)`
```

## Riscos e rollback

- Risco principal: a melhora de fluidez e ghosting ainda depende do comportamento real do painel `ICN6124D + FM7258E`; o baseline upstream reduz risco, mas nao elimina a possibilidade de tuning extra por hardware.
- Risco secundario: `Bins128` reapresentado continuamente pode aumentar trabalho do loop em comparacao ao modo estritamente dirty-only, embora agora respeite a taxa fisica calculada do painel.
- Como reverter:
  - desfazer o pin da lib para `3.0.11`;
  - restaurar o pacing fixo anterior;
  - remover `setLatBlanking(2)` e voltar ao perfil documental anterior.

## Proximos passos

1. Gravar o firmware e validar no hardware se as barras deixam de distorcer e se o flicker diminui perceptivelmente.
2. Confirmar no serial boot os valores efetivos de `driver`, `calculated_refresh_rate`, `latch_blanking`, `clkphase` e `double buffer`.
3. Se ainda houver distorcao relevante apos esta entrega, abrir uma fase separada para testar driver alternativo (`FM6124`) e, se necessario, usar o `PIO_TestPatterns` da upstream como oracle de bancada.
