# Handoff - HUB75 Anti-Flicker Com Double Buffer

## Objetivo

Eliminar flicker/tearing do painel HUB75 no firmware oficial do ESP32-S3 ativando `double buffer`, usando `flipDMABuffer()` no commit e interrompendo o redesenho continuo sem frame novo.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - `initMatrixDisplay()` sobe com `config.double_buff = true`;
  - `commitMatrixFrame()` executa `flipDMABuffer()`;
  - o loop do firmware apresenta frames apenas quando houver `dirty frame` e quando a cadencia minima permitir novo flip;
  - o timeout de `15 s` apaga o painel uma vez e permanece estavel;
  - `streamFramesApplied` passa a refletir frames realmente apresentados no HUB75.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/reference/device-observability-dashboard.md`
- `docs/handoffs/2026-03-14-hub75-anti-flicker-double-buffer.md`

## Decisoes tomadas

1. O diagnostico principal foi confirmado no firmware atual: `commitMatrixFrame()` estava vazio, `double_buff` nao estava ativo e o loop redesenhava a matriz inteira em toda iteracao.
2. O contador `streamFramesApplied` nao foi movido para `commitMatrixFrame()`, porque esse helper tambem e usado em clears nao relacionados ao stream; a contagem passou a acontecer apenas apos apresentacao real do conteudo do stream.
3. O firmware ganhou `dirty flag` e limite de apresentacao de `20 ms` (`50 FPS`) para reduzir tearing sem reintroduzir redraw continuo.
4. A aplicacao de brilho continuou imediata fora do repaint, preservando `setMatrixBrightness(resolveAppliedBrightness())` e `updateTestLedDutyFromBrightness(...)` no loop principal.
5. O timeout de silencio do stream passou a limpar bins/frame uma unica vez, evitando clears e flips repetidos a cada loop.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceServerHostDashboardTests|FullyQualifiedName~DeviceMetricsFormatterTests"
```

## Riscos e rollback

- Risco principal: o painel `ICN6124D + FM7258E` ainda pode exigir tuning adicional de driver/timing mesmo com `double buffer`, se persistirem faixas fisicas no hardware.
- Risco secundario: com throttle de apresentacao, `streamFramesApplied` pode ficar abaixo de `streamFramesReceived` sob carga alta, o que e esperado pela nova semantica.
- Como reverter:
  - desligar `config.double_buff`;
  - restaurar `commitMatrixFrame()` vazio;
  - remover `dirty flag`/cap de `present_interval_ms`;
  - voltar a semantica documental anterior de `streamFramesApplied`.

## Proximos passos

1. Gravar o firmware no ESP32-S3 e validar no painel real se o tearing/flicker desapareceu.
2. Confirmar que `hub75Fps` no dashboard passa a acompanhar a taxa real de apresentacao no painel.
3. Se ainda houver artefatos fisicos apos esta entrega, abrir uma fase separada para tuning do painel `ICN6124D + FM7258E`.
