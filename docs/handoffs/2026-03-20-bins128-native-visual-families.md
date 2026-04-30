# Handoff - familias fisicas nativas para `Bins128` no HUB75

## Objetivo

Diferenciar no HUB75 os presets de audio que hoje usam `Bins128`, reutilizando o byte `flags` do protocolo tipo `1` para sinalizar estilo e familia de paleta sem mudar o wire shape.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - `Bins128` continua com `145` bytes e `messageType = 1`
  - o host envia `flags` nao-zero para presets mapeados
  - o firmware deixa de colapsar todos os presets `Bins128` no mesmo `drawBars()` legado
  - `flags = 0` continua reproduzindo o comportamento antigo
  - `Frame128x64` de `Paineis` permanece intacto

## Arquivos alterados

- `src/Device.Protocol/Stream/Bins128VisualFlags.cs`
- `src/MicaAudio.Core/Led/LedPayload.cs`
- `src/MicaAudio.Core/Led/LedPayloadFactory.cs`
- `src/Output/Led/Esp32S3LedOutput.cs`
- `src/App.WinUI/Services/AudioPipelineFrameProcessor.cs`
- `src/App.WinUI/Services/AudioPipelineCoordinator.cs`
- `src/App.WinUI/Services/Visualizer/Hub75BinsVisualIdentityResolver.cs`
- `src/App.WinUI/Views/MainPage.Hub75VisualizerFrames.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `tests/Output.Tests/Bins128VisualFlagsTests.cs`
- `tests/Output.Tests/LedPayloadFactoryTests.cs`
- `tests/Output.Tests/Esp32S3LedOutputTests.cs`
- `tests/Output.Tests/StreamFrameV2Tests.cs`
- `tests/Integration.Smoke/AudioPipelineCoordinatorTests.cs`
- `tests/Integration.Smoke/Hub75BinsVisualIdentityResolverTests.cs`
- `docs/wiki/reference/ws-protocol-v2.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

1. O protocolo tipo `1` nao mudou de tamanho nem de `messageType`; o byte `flags` existente virou a superficie oficial para `styleId` e `paletteFamilyId`.
2. O host resolve `presetId + rendererId` para poucas familias fisicas nativas, em vez de tentar paridade total entre preview WinUI e HUB75.
3. O firmware preserva `flags = 0` como fallback legado para backward compatibility com emissores antigos ou identidades desconhecidas.
4. O dispatcher novo `drawBinsVisual()` usa apenas bins, level e brightness; `Frame128x64` continua reservado aos caminhos full-frame como `Paineis`.
5. O estado temporal dos visuais nativos e resetado em mudanca de estilo e em timeout do stream para evitar contaminacao entre presets.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> sucesso
dotnet build MicaAudio.sln -c Debug -m:1 -> sucesso
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "Bins128VisualFlagsTests|StreamFrameV2Tests|Esp32S3LedOutputTests|LedPayloadFactoryTests" -> sucesso
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "AudioPipelineCoordinatorTests|Hub75BinsVisualIdentityResolverTests|RendererIntegrationContractSmokeTests|VisualizerPresetSmokeTests" -> sucesso
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> sucesso
```

## Riscos e rollback

- Risco principal: algum preset customizado ou caminho tecnico sem `presetId` conhecido cair em familia fisica inesperada no HUB75.
- Como reverter:
  - no host, voltar `ResolveFlags(...)` para `flags = 0`
  - no firmware, restaurar o fluxo unico `drawBars()` para o tipo `1`
  - o contrato wire nao precisa de rollback estrutural porque o tamanho do pacote nao mudou

## Proximos passos

1. Validar manualmente no painel as familias `wave-mirror`, `mirror-lines`, `classic-bars`, `flow-line`, `history-scan`, `radial-orbit`, `atmosphere` e `launchpad-grid`.
2. Se alguma familia ainda colapsar visualmente no painel real, simplificar o desenho nativo correspondente antes de tentar acrescentar mais detalhes.
