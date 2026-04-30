# Handoff - HUB75 frame transport unblock + AudioMotion Clone parity

## Objetivo

Destravar o caminho `Frame128x64Rgb565` do HUB75 no build oficial do firmware e alinhar o `AudioMotion Clone` ao mesmo transporte por frame, para que o painel fisico acompanhe o preview WinUI em vez de cair no fallback de barras por `Bins128`.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o build oficial do firmware garante `WEBSOCKETS_MAX_DATA_SIZE >= 32768`;
  - o warning de redefinicao efetiva para `15 * 1024` deixa de aparecer no build oficial;
  - `AudioMotion Clone` passa a anunciar `HubTransportMode = Frame128x64`;
  - os smoke tests refletem o novo contrato de transporte;
  - a documentacao do modulo registra que o caminho autoritativo do HUB75 agora inclui `AudioMotion Clone`.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/scripts/patch_websockets_max_data_size.py`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs`
- `tests/Integration.Smoke/RendererIntegrationContractSmokeTests.cs`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/modules/visual-win2d.md`
- `docs/handoffs/2026-03-13-hub75-frame-transport-audiomotion-parity.md`

## Decisoes tomadas

1. O congelamento dos renderers `Frame128x64` foi tratado primeiro no build do firmware, porque o payload `Frame128x64Rgb565` tem `16400` bytes e a dependencia `WebSockets` estava preservando um limite efetivo de `15 * 1024`.
2. Em vez de editar a dependencia instalada manualmente, o repositorio passou a usar um `extra_script` versionado do PlatformIO para patchar `WebSockets.h` de forma idempotente durante o build oficial.
3. O firmware recebeu `static_assert` e log serial de boot para validar rapidamente que o limite WS efetivo cobre o payload `Frame128x64`.
4. `AudioMotion Clone` deixou de usar `Bins128` no HUB75 e passou para `Frame128x64`, priorizando paridade visual com o preview.
5. O tuning especifico do painel `ICN6124D + FM7258E` ficou explicitamente fora desta entrega inicial; se ainda houver artefatos apos o frame transport funcionar, isso vira fase 2.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj --filter "RendererIntegrationContractSmokeTests|AudioPipelineCoordinatorTests|VisualizerPresetSmokeTests"
```

## Riscos e rollback

- Risco principal: mesmo com o frame transport destravado, o painel `P2.5 128x64 1/32` com `ICN6124D + FM7258E` ainda pode exigir tuning de driver/timing no caminho HUB75.
- Risco secundario: uma mudanca futura na dependencia `WebSockets` pode invalidar a assinatura esperada do patch e fazer o build falhar cedo.
- Como reverter:
  - remover `extra_scripts` de `platformio.ini`;
  - excluir `firmware/esp32s3-devkitc1/scripts/patch_websockets_max_data_size.py`;
  - remover `static_assert` e o log WS adicional do `main.cpp`;
  - restaurar `AudioMotionCloneRenderer` para `HubTransportMode = Bins128`;
  - restaurar os smoke tests e a documentacao anterior.

## Proximos passos

1. Gravar o firmware no painel real e confirmar que `AuroraRibbon`, `LaunchpadGrid` e `PlasmaPulse` deixam de congelar no ultimo frame.
2. Validar que o `AudioMotion Clone` no HUB75 passa a seguir o preview em vez de mostrar o fallback espelhado por `Bins128`.
3. Se ainda houver faixas fisicas em todos os renderers por frame, abrir uma entrega separada para tuning do painel `ICN6124D + FM7258E`.
