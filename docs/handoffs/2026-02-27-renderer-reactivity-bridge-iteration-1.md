# Handoff - Renderer reactivity bridge iteration 1

## Objetivo

Introduzir uma bridge incremental de capacidades de renderer e reatividade compartilhada, migrando apenas `AudioMotionCloneRenderer` e `PolarArcsRenderer` para resolver a baixa reatividade e a falta de integracao do `Polar Arcs` com os controles globais da UI.

## Escopo classificado

Estrutural.

## Arquivos alterados

- `src/Visual.Win2D/Engine/IRendererCapabilitiesProvider.cs`
- `src/Visual.Win2D/Engine/RendererCapabilities.cs`
- `src/Visual.Win2D/Engine/RendererControlSupport.cs`
- `src/Visual.Win2D/Engine/RendererBarCountMode.cs`
- `src/Visual.Win2D/Engine/RendererIntegrationMode.cs`
- `src/Visual.Win2D/Engine/ReactiveEnvelopeState.cs`
- `src/Visual.Win2D/Engine/ReactiveBandSnapshot.cs`
- `src/Visual.Win2D/Engine/ReactiveBandSampler.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs`
- `src/Visual.Win2D/Renderers/PolarArcsRenderer.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/ReactiveBandSamplerTests.cs`
- `tests/Integration.Smoke/RendererIntegrationContractSmokeTests.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/adr/0007-renderer-reactivity-and-ui-capability-bridge.md`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/add-new-renderer.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. `IRenderer` foi mantido intacto para evitar uma migracao breaking do catalogo inteiro.
2. O contrato novo entrou por `IRendererCapabilitiesProvider` com fallback `LegacyAssumed`.
3. `ReactiveBandSampler` virou o gate deterministico de reatividade.
4. `PolarArcsRenderer` foi classificado como `Resampled`.
5. `AudioMotionCloneRenderer` ficou com `SupportsBarCount=false`, porque sua geometria continua dependente da largura efetiva do layout atual.

## Validacoes executadas

- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug --no-restore`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer|FullyQualifiedName~RendererIntegration"`

## Riscos e rollback

- A lateral de configuracao agora depende de `VisualizerEngine.GetCapabilities(...)`; regressao nessa inferencia afeta principalmente a visibilidade do controle de barras.
- Renderers legados continuam sem contrato explicito, entao o fallback precisa permanecer estavel ate a proxima iteracao.
- Rollback: remover a bridge nova, restaurar o gating antigo da `MainPage` e voltar os dois renderers para smoothing local.

## Proximos passos

1. Validar o `Polar Arcs` visualmente em runtime real.
2. Migrar o proximo lote de renderers so depois dessa validacao.
3. Tornar os testes de contrato obrigatorios para qualquer renderer novo.

## Update 2 - Polar Arcs visual simplification
- Ajustado `PolarArcsRenderer` para remover guias persistentes e o viés de arco pre-aberto.
- O visual agora mantem identidade de disco com contorno RGB fixo, ponto central verde e apenas barras em arco reativas ao audio.
- As barras partem do zero visual e abrem somente conforme a energia da musica, com jitter secundario.
- `DefaultPresets` recebeu schema bump para reaplicar os defaults revisados do preset builtin `spectrum-polar-arcs`.
- Refinamento adicional: removida a moldura do disco em `PolarArcsRenderer`; a visualizacao agora renderiza apenas as barras em arco sobre fundo vazio.
