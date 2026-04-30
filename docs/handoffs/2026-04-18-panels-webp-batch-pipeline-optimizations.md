## Objetivo

Reduzir custo de CPU, copias de frame e churn de heap no pipeline de batches `WebP` de `Paineis`, mantendo o contrato wire atual e sem revisar `BatchDuration`, `BatchPreloadLead`, `frameCount` ou `durationMs`.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui: compositor host com render em buffer reutilizavel, encode incremental do lote `WebP`, queue do batch sem materializar `30` frames completos, hot path do firmware para `RGBA -> RGB565`, espera de timestamp sem `mutex` por iteracao e medicao leve local de decode/present.
- Nao inclui: mudanca de wire/protocolo, retuning de compressao `WebP`, revisao da cadencia `1 s / 30 frames`, migracao para PSRAM como estrategia principal de performance ou alteracao da prioridade entre `Paineis` e visualizador.

## Arquivos alterados

- `src/App.WinUI/Services/Panels/PanelsFrameComposer.cs`
- `src/App.WinUI/Services/Panels/PanelsAnimatedWebpEncoder.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `tests/Integration.Smoke/PanelsFrameComposerTests.cs`
- `firmware/esp32s3-devkitc1/src/mica_panels.cpp`
- `firmware/esp32s3-devkitc1/src/mica_globals.cpp`
- `firmware/esp32s3-devkitc1/src/mica_globals.h`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-04-18-panels-webp-batch-pipeline-optimizations.md`

## Decisoes tomadas

1. O hot path do host passou a usar `RenderFrameInto(...)` em vez de depender de `scratch.ToArray()` para cada frame do batch.
2. O encode `WebP` ganhou um caminho incremental com `RgbaColor[]` e `byte[] RGBA` reutilizaveis, mantendo `Lossless = true`, `Method = 6`, `ThreadLevel = true` e `UseSharpYuv = true`.
3. `PanelsPlaybackService` deixou de materializar `List<RgbaColor[]>` para o lote inteiro e agora renderiza/encode frame a frame, preservando o mesmo contrato de envio.
4. O firmware deixou de pegar `gPanelsBatchMutex` em cada iteracao de `waitForPanelsBatchTimestampUs()` e passou a consultar um sinal de cancelamento lock-free para reduzir overhead no playback task.
5. A conversao `RGBA -> RGB565` do batch task foi simplificada para caminhada linear por ponteiro, evitando multiplicacao e chamada de funcao por pixel no caminho quente.
6. A observabilidade nova ficou restrita a medicao local default-off (`kPanelsPerfLoggingEnabled = false`) para `decode_max_us` e `present_max_us`, sem novos campos de protocolo.

## Validacoes executadas

```text
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~PanelsFrameComposerTests.PanelCompositionSession_RenderFrameInto_ShouldMatchRenderFrame" -> FAIL esperado (RenderFrameInto inexistente)
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~PanelsFrameComposerTests.PanelCompositionSession_RenderFrameInto_ShouldMatchRenderFrame|FullyQualifiedName~PanelsFrameComposerTests.AnimatedWebpEncoder_ShouldEmitWebpContainerAndPreserveBatchMetadata|FullyQualifiedName~PanelsFrameComposerTests.CreateSessionAsync_ShouldRenderClockWidgetIntoFullFrame" -> OK
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceServerHostPanelsBatchTests.RegisterPanelsBatch_ShouldServePayloadOnlyToAuthenticatedTargetDevice" -> OK
platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (warnings NU190x preexistentes de Magick.NET-Q8-AnyCPU 14.11.1)
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

## Riscos e rollback

- Risco principal: algum caminho ainda depender implicitamente da semantica anterior de `RenderFrame()`/alocacao por frame e apresentar regressao visual sutil no batch encoder.
- Risco secundario: leitura lock-free do cancelamento no firmware pode expor corrida se outra parte do runtime passar a depender de sincronizacao mais forte do que o `bool volatile` atual oferece.
- Mitigacao: o contrato wire, a ordem de batches e o fallback para stream WS bruto foram preservados; ha cobertura direta para equivalencia do frame renderizado e para o contrato do batch servido pelo host.
- Rollback:
  1. voltar `PanelsPlaybackService` ao caminho `RenderBatchFrames(...) + Encode(IReadOnlyList<RgbaColor[]>, ...)`;
  2. remover `RenderFrameInto(...)` e retornar ao `scratch.ToArray()` no compositor;
  3. restaurar `waitForPanelsBatchTimestampUs()` para o polling protegido por `gPanelsBatchMutex`;
  4. remover os contadores locais `decode_max_us` / `present_max_us`.

## Proximos passos

1. Validar em hardware real se a entrada do modo `Paineis` e o apply de painel ficaram perceptivelmente mais rapidos sob carga repetida.
2. Medir serialmente `decode_max_us` e `present_max_us` apenas se algum novo incidente exigir investigacao de pacing no playback task.
3. Se o gargalo dominante remanescente passar a ser o custo puro do encoder `WebP`, abrir uma fase separada para tradeoffs de compressao/cadencia sem misturar com este hardening conservador.
