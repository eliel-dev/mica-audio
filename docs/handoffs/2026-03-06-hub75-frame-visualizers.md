# Handoff - HUB75 frame visualizers 2D

## Objetivo

Adicionar visualizacoes 2D mais artisticas e reativas, com saida real para o painel HUB75 `128x64`, sem alterar o protocolo wire existente.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: novos renderers e presets entram no catalogo, o app consegue alternar entre `Bins128` e `Frame128x64` por renderer, e os checks obrigatorios de docs/governanca/build seguem verdes.

## Arquivos alterados

- `src/Visual.Win2D/Engine/RendererCapabilities.cs`
- `src/Visual.Win2D/Engine/RendererHubTransportMode.cs`
- `src/Visual.Win2D/Engine/RendererIds.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs`
- `src/Visual.Win2D/Renderers/AuroraRibbonRenderer.cs`
- `src/Visual.Win2D/Renderers/PlasmaPulseRenderer.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `src/App.WinUI/Services/AudioPipelineCoordinator.cs`
- `src/App.WinUI/Services/Visualizer/Hub75VisualizerFrameRenderer.cs`
- `src/App.WinUI/Views/MainPage.Hub75VisualizerFrames.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `tests/Integration.Smoke/RendererIntegrationContractSmokeTests.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. O painel HUB75 passou a receber `Frame128x64` por renderer, em vez de depender sempre do caminho `Bins128`, para permitir liberdade artistica real sem reescrever o firmware.
2. O protocolo nao foi alterado: o app usa o `messageType = 2` ja suportado no stream v2 e o firmware continua apenas desenhando o frame recebido.
3. O `AudioMotion Clone` ficou com `HubTransportMode = Bins128` para preservar o caminho de menor custo quando throughput e simplicidade forem prioridade.
4. O render autoritativo do HUB75 usa um `VisualizerEngine` dedicado em offscreen `128x64`, evitando acoplamento direto com o estado temporal do canvas principal.
5. O envio de frames do visualizer foi limitado a 30 FPS no caminho HUB75 para reduzir custo de rede sem perder reatividade perceptivel no painel.
6. Os dois presets novos (`Aurora Ribbon` e `Plasma Pulse`) foram calibrados com formas largas e leitura forte para baixa resolucao, seguindo o alvo primario `128x64`.

## Validacoes executadas

```text
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~VisualizerPresetSmokeTests|FullyQualifiedName~RendererIntegrationContractSmokeTests" -> OK (12 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
```

## Riscos e rollback

- Risco principal: divergencia visual leve entre o canvas principal e o preview/painel HUB75, porque o frame autoritativo usa um engine dedicado em resolucao nativa.
- Como reverter: remover `Hub75VisualizerFrameRenderer`, voltar `RendererCapabilities.HubTransportMode` para `Bins128` e retirar os presets/renderers `aurora-ribbon` e `plasma-pulse`.

## Proximos passos

1. Validar visualmente os novos presets com musica real no painel fisico.
2. Medir estabilidade/latencia de `Frame128x64` em sessoes longas com Wi-Fi real.
3. Se necessario, promover mais renderers para `Bins128` ou criar politica dinamica por carga/telemetria.
