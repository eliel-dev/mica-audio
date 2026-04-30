# Handoff - HUB75 tearing diagnostic matrix

## Objetivo

Adicionar uma trilha diagnostica separada para investigar tearing/ghosting no HUB75 `128x64` do ESP32-S3 sem reabrir o cutover desktop para `Bins128`, mantendo o baseline shipping conservador em `60 FPS` e `min_refresh_rate = 60`, e isolando a origem com oracles locais da biblioteca `ESP32-HUB75-MatrixPanel-DMA`.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o env oficial continua intacto como baseline shipping;
  - existe um env diagnostico do firmware oficial com `CORE_DEBUG_LEVEL=3`;
  - o boot serial do firmware oficial e diagnostico passa a registrar `min_refresh_rate` explicitamente;
  - existem envs-oracle locais para `SHIFTREG` e `FM6124` usando o mesmo pinout `128x64`;
  - a documentacao do modulo registra a matriz diagnostica e a politica de nao promover `120 Hz` a baseline.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/diagnostics/hub75-oracle/main.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-19-hub75-tearing-diagnostic-matrix.md`

## Decisoes tomadas

1. O baseline shipping permaneceu conservador:
   - `target present = 60 FPS`
   - `min_refresh_rate = 60`
   - `SHIFTREG`
   - `clkphase = false`
   - `latch_blanking = 2`
2. `min_refresh_rate` deixou de ficar implicito no default da biblioteca e passou a ser registrado explicitamente pelo firmware.
3. A investigacao nao reabre `commitMatrixFrame()` nem troca `flipDMABuffer()` por outra estrategia nesta fase.
4. Em vez de mudar o wire ou o desktop, a fase adiciona isolamento no proprio firmware:
   - `esp32s3_devkitc1_dma_diag` para o runtime real do Mica com logs detalhados;
   - `esp32s3_devkitc1_dma_oracle_shiftreg` e `esp32s3_devkitc1_dma_oracle_fm6124` para comparar a lib fora do runtime do app.
5. `120 Hz` foi explicitamente rejeitado como baseline desta investigacao:
   - o repositorio trava `60` como default;
   - `90` fica apenas como experimento posterior, se a bancada justificar.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_diag
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_oracle_shiftreg
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_oracle_fm6124
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
```

## Riscos e rollback

- Risco principal: os envs-oracle mostram apenas comportamento da lib no mesmo hardware/pinout; se o tearing nascer de alimentacao, cabeamento, aterramento ou painel fisico, a investigacao ainda vai exigir bancada manual.
- Risco secundario: `src_dir` dedicado dos envs-oracle depende do comportamento esperado do PlatformIO e pode exigir ajuste se o ambiente local divergir.
- Rollback:
  - remover os envs diagnosticos/oracle do `platformio.ini`;
  - remover `firmware/esp32s3-devkitc1/diagnostics/hub75-oracle/main.cpp`;
  - restaurar o log anterior do `main.cpp`;
  - remover a secao documental desta fase.

## Proximos passos

1. Gravar `esp32s3_devkitc1_dma_diag` no hardware real e capturar os logs:
   - `driver`
   - `min_refresh_rate`
   - `calculated_refresh_rate`
   - `physical_present_interval_us`
   - `effective_present_interval_us`
   - `clkphase`
   - `double_buffer`
   - `latch_blanking`
   - `target_buffer_id`
   - `back_buffer_id`
   - `ROWS_PER_FRAME`
2. Rodar os dois envs-oracle no mesmo painel para decidir se o tearing nasce na lib/painel ou no caminho Mica `Frame128x64`.
3. Se os oracles tambem rasgarem, abrir a proxima fase para driver/timing/hardware (`SHIFTREG` vs `FM6124`, `clkphase`, `latch blanking`).
4. Se os oracles ficarem limpos e so o Mica rasgar, abrir a proxima fase focada no writer bulk RGB565/BCM bitplane.
