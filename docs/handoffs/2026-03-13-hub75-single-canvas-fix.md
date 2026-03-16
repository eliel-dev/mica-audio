# Handoff - HUB75 single-canvas fix

## Objetivo

Corrigir o firmware do `ESP32-S3 DevKitC-1` para dirigir o painel HUB75 `128x64` como um unico canvas continuo, removendo a faixa preta observada fisicamente entre duas metades aparentes do display.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o pinout HUB75 do firmware fica alinhado com a bancada validada;
  - a linha `E` deixa de ser omitida na inicializacao do painel `128x64`;
  - o firmware falha cedo quando um painel `128x64` for configurado sem `E` ou com conflito de pinos criticos;
  - a documentacao do modulo registra explicitamente o mapeamento single-canvas `128x64`.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-13-hub75-single-canvas-fix.md`

## Decisoes tomadas

1. O caminho de display do app e do protocolo foi preservado porque ele ja trabalha como frame continuo `128x64`.
2. A correcao foi restrita ao firmware: o bug principal estava no `pinMap` da HUB75, que inicializava `E = -1` e ainda conflitiva `GPIO41` entre `E` e `CLK`.
3. O pinout oficial passou a refletir a bancada funcional informada pelo usuario:
   - `RGB = {4, 5, 6, 7, 15, 16}`
   - `A/B/C/D/E = {18, 8, 3, 42, 17}`
   - `LAT = 40`, `OE = 2`, `CLK = 41`
4. O firmware ganhou validacao de pinout e log serial do mapeamento efetivo para reduzir regressao silenciosa em campo.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
```

## Riscos e rollback

- Risco principal: o painel fisico usar cabeamento diferente do sketch validado em bancada.
- Se o hardware real divergir, a imagem pode continuar deslocada ou sem enderecamento correto apesar do canvas logico permanecer unico.
- Como reverter:
  - restaurar os valores anteriores de `kMatrixAddrPins` e do `pinMap` em `main.cpp`;
  - remover a validacao/local log adicional;
  - remover a secao `HUB75 128x64 single-canvas mapping` da wiki do modulo.

## Proximos passos

1. Gravar o firmware no device real e validar cores solidas, linhas continuas e grid sem quebra em `x=63/64` e `y=31/32`.
2. Se ainda houver descontinuidade fisica, comparar o comportamento com o sketch de bancada antes de considerar troca de driver/configuracao mais ampla.
