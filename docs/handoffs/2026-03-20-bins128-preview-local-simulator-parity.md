# Handoff - preview HUB75 local fiel ao `Bins128` do device

## Objetivo

Fazer o preview HUB75 inferior do `Visualizador` refletir localmente as familias visuais `Bins128` que o firmware desenha no painel fisico.

## Escopo classificado

- Tipo: funcional
- Criterio de aceite:
  - o preview HUB75 continua vindo de `SimulatorLedOutput`
  - `Bins128` deixa de colapsar no visual espelhado legado para todos os presets
  - `Frame128x64` continua intacto
  - `flags = 0` preserva o fallback legado

## Arquivos alterados

- `src/Output/Led/Bins128PreviewRenderer.cs`
- `src/Output/Led/SimulatorLedOutput.cs`
- `tests/Output.Tests/Bins128PreviewRendererTests.cs`
- `tests/Output.Tests/SimulatorLedOutputTests.cs`
- `tests/Integration.Smoke/PipelineSmokeTests.cs`
- `docs/wiki/modules/output-led.md`

## Decisoes tomadas

1. O preview da pagina nao mudou de fonte; a correcao entrou no simulador local para preservar a arquitetura atual do `MainPage`.
2. O renderer novo replica em C# as familias nativas do firmware, incluindo historico, peak hold e launchpad hold, para reduzir drift visual no preview.
3. O payload usado pelo preview continua sendo o payload real emitido para o HUB75, inclusive `BinsFlags`, evitando uma segunda resolucao paralela de preset/renderer.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -m:1 -> sucesso
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "Bins128PreviewRendererTests|SimulatorLedOutputTests|LedOutputLifecycleTests|Esp32S3LedOutputTests|LedPayloadFactoryTests" -> sucesso
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "PipelineSmokeTests|AudioPipelineCoordinatorTests|Hub75BinsVisualIdentityResolverTests" -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso
```

## Riscos e rollback

- Risco principal: drift futuro entre o dispatcher visual do firmware e o renderer local C#.
- Como reverter:
  - voltar `SimulatorLedOutput` para o `RenderBins(...)` legado;
  - remover `Bins128PreviewRenderer`;
  - manter o wire/protocolo inalterado.

## Proximos passos

1. Validar manualmente no app se `Wave Mirror`, `AudioMotion Clone`, `Bars`, `Spectrogram`, `Radial`, `Aurora` e `Launchpad` ficam reconheciveis no preview inferior.
2. Se algum estilo divergir demais do painel real, alinhar primeiro as formulas/helpers compartilhados conceitualmente antes de mexer na UI.
