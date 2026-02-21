# Handoff - Render HUB75 no firmware Matrix Portal S3

## Objetivo

Implementar renderizacao real no firmware para que frames recebidos por WebSocket (`gBins`, `gLevel`, `gServerBrightness`) sejam efetivamente exibidos no painel HUB75.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite: ambos os perfis (`matrixportal_s3_stable` e `matrixportal_s3_dma_exp`) compilam com `drawBars()` funcional e inicializacao de display ativa.

## Arquivos alterados

- `firmware/matrixportal-s3/src/main.cpp`
- `docs/wiki/modules/firmware-matrixportal-s3.md`

## Decisoes tomadas

1. Reaproveitar o mapeamento de pinos do Matrix Portal S3 usado nos exemplos oficiais da Adafruit Protomatter para manter compatibilidade com o hardware alvo.
2. Implementar barras espelhadas verticalmente (64x32) com gradiente arco-iris por coluna, usando `gBins` como altura e `gServerBrightness` como limite de brilho aplicado em runtime.
3. Inicializar o painel em `setup()` via `initMatrixDisplay()` e manter fallback seguro (log serial + firmware segue funcional) quando a inicializacao falhar.
4. No perfil `stable`, aplicar brilho por escala de cor (Protomatter nao expoe `setBrightness`); no perfil `dma_exp`, usar `setBrightness8` nativo da lib DMA.

## Validacoes executadas

```text
py -m platformio run -e matrixportal_s3_stable -> sucesso
py -m platformio run -e matrixportal_s3_dma_exp -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> sucesso
dotnet build MicaAudio.sln -c Debug -> sucesso
```

## Riscos e rollback

- Risco principal: variacao de hardware/painel pode exigir ajuste fino de clock/phase no perfil `dma_exp`.
- Como reverter: restaurar `firmware/matrixportal-s3/src/main.cpp` ao estado anterior e recompilar os dois ambientes PlatformIO.

## Proximos passos

1. Flash manual em um Matrix Portal S3 com HUB75 para validar orientacao/cores em hardware real.
2. Se necessario, expor configuracao de orientacao/espelhamento por comando para ajuste sem recompilacao.