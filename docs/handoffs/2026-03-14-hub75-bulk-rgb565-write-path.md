# Handoff - HUB75 bulk RGB565 write path

## Objetivo

Eliminar o custo do caminho `drawFrame128x64()` baseado em `rgb565ToRgb888` + `drawMatrixPixel` por pixel, substituindo-o por uma escrita bulk RGB565 diretamente no back buffer BCM da `ESP32-HUB75-MatrixPanel-DMA` usada no firmware oficial do ESP32-S3.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o build oficial aplica um patch reproduzivel na lib HUB75 pinada;
  - `drawFrame128x64()` deixa de fazer diff por pixel e deixa de usar `rgb565ToRgb888`/`drawMatrixPixel`;
  - o frame `128x64` e escrito em uma unica chamada bulk da lib antes do `commitMatrixFrame()`;
  - `gMatrixShadowFrames[...]` continua coerente para o retorno ao modo `Bars`.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/scripts/patch_hub75_bulk_rgb565.py`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-14-hub75-bulk-rgb565-write-path.md`

## Decisoes tomadas

1. Nao foi alterado `.pio/libdeps` manualmente. O patch na lib foi versionado via `extra_script`, seguindo o padrao ja usado para `WebSockets`.
2. A lib pinada em `3.0.11` nao expunha `getBackBuffer()` publico nem `drawPixelRGB565()`, entao a entrega adiciona `writeFrameRGB565(const uint16_t* frame565)` como API publica minima.
3. O novo writer trabalha direto no formato BCM interno da lib, preservando bits de controle e evitando roundtrip por `RGB888`.
4. O mapeamento foi orientado a throughput, nao a paridade exata da curva antiga:
   - `R/B` sao expandidos de `5` para `6` bits por replicacao simples;
   - `G` usa os `6` bits nativos do `RGB565`;
   - `lumConvTab` e a curva/gamma da lib nao entram nesse caminho.
5. `drawFrame128x64()` foi reduzido a:
   - chamada bulk na lib;
   - `memcpy` do shadow frame;
   - reset do cache de barras;
   - `commitMatrixFrame()`.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> falhou em paralelo por lock transitivo em `src/Output/obj/Debug/net10.0/Output.dll`
dotnet build MicaAudio.sln -c Debug -m:1 -> OK
```

## Riscos e rollback

- Risco principal: o path bulk muda a resposta visual porque nao replica `lumConvTab`; o ganho de throughput vem com mudanca esperada de curva/brilho.
- Risco secundario: o patch depende de assinaturas especificas da lib `3.0.11`; drift da dependencia precisa falhar cedo no build, nao de forma silenciosa.
- Como reverter:
  - remover o `extra_script` novo do `platformio.ini`;
  - remover o patch script;
  - restaurar `drawFrame128x64()` para o caminho anterior baseado em `drawMatrixPixel`.

## Proximos passos

1. Compilar o firmware para validar que o patch da lib esta sendo aplicado e que `main.cpp` enxerga `writeFrameRGB565(...)`.
2. Rodar `docs-validate` e `ai-governance-check`.
3. Se o pipeline global continuar sendo usado em paralelismo no ambiente local, investigar separadamente o lock transitivo em `Output.dll` observado no build sem `-m:1`.
4. Gravar o firmware e comparar no hardware se o ganho de fluidez supera a mudanca de resposta visual introduzida pelo caminho throughput-first.
