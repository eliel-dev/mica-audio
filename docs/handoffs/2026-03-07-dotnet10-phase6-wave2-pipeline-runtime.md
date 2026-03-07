# Handoff - Fase 6 / Onda 2 (runtime do pipeline)

## Objetivo

Transformar o `AudioPipelineCoordinator` em orquestrador fino e separar o runtime do pipeline em colaboradores dedicados, preservando o contrato funcional atual do coordinator e o comportamento externo do app.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta onda:
  - introduzir `AudioPipelineCaptureProfile` para a politica fixa de captura;
  - introduzir `AudioPipelineOutputRouter` para brilho/roteamento entre ESP32, simulador e null output;
  - introduzir `AudioPipelineFrameProcessor` para analyzer atual, `LatestFrame`, preset atual e transporte `Bins128`/`Frame128x64`;
  - manter `AudioPipelineCoordinator` como orquestrador fino de `StartAsync`/`StopAsync`/status;
  - adicionar cobertura de lifecycle, idempotencia e troca de analyzer.
- Fora desta onda:
  - redesign de UI;
  - mudanca de `ILedOutput`, `SpectrumFrame` ou wire de `StreamFrameV2`;
  - mudanca de firmware.

## Arquivos alterados

- Pipeline:
  - `src/App.WinUI/Services/AudioPipelineCoordinator.cs`
  - `src/App.WinUI/Services/AudioPipelineCaptureProfile.cs`
  - `src/App.WinUI/Services/AudioPipelineOutputRouter.cs`
  - `src/App.WinUI/Services/AudioPipelineFrameProcessor.cs`
- Testes:
  - `tests/Integration.Smoke/AudioPipelineCoordinatorTests.cs`

## Decisoes tomadas

- `AudioPipelineCoordinator` preserva:
  - `StartAsync`
  - `StopAsync`
  - `SetHubPreview`
  - `ConfigureHubOutputs`
  - `SendHubFrame`
  - `LatestFrame`
  - `StatusChanged`
- O coordinator deixou de concentrar inline:
  - politica de captura;
  - composicao de payload;
  - roteamento de outputs;
  - ownership do analyzer atual.
- `StartAsync` e `StopAsync` ficaram idempotentes com limpeza centralizada de recursos.
- `SetAnalyzer()` passou a trocar o analyzer em runtime sem depender de lambda ou `new` oculto no loop.
- O remapeamento `SpectrumFrame -> Bins128` foi consolidado no `LedPayloadFactory`, o que removeu duplicacao do smoke test.

## Validacoes executadas

- Checkpoint integrado da fase 6:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
  - `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
- Resultado final do checkpoint integrado:
  - rebuild com `0 warnings`;
  - `229` testes aprovados;
  - `1` teste ignorado.

## Riscos e rollback

- Risco principal:
  - drift de lifecycle entre capture/output se algum colaborador interno deixar de ser resetado no caminho de erro.
- Rollback:
  - restaurar o `AudioPipelineCoordinator` monolitico;
  - remover `AudioPipelineCaptureProfile`, `AudioPipelineOutputRouter` e `AudioPipelineFrameProcessor`;
  - voltar a compor `LedPayload` inline no coordinator.

## Proximos passos

- Onda 3 da fase 6:
  - reduzir a orquestracao tecnica da `MainPage`;
  - mover persistencia de runtime e rebuild de analyzer para helpers dedicados;
  - manter a tela como borda de integracao, nao como origem das invariantes.
