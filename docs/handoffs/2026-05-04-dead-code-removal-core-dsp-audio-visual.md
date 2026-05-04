# Handoff - Remocao de codigo morto em Core, DSP, Audio, Visual, App.WinUI e testes

## Objetivo

Remover codigo obsoleto, nao utilizado ou sem referencias ativas nos projetos Core, DSP, Audio, Visual, App.WinUI, Device.Server e suites de teste, garantindo que o build continue passando.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: build limpo (`dotnet build MicaAudio.sln`) e validacoes de docs/governanca passando.

## Arquivos alterados

### Deletados
- `src/Analyzer.Dsp/Analysis/SpectrumFftBackendKind.cs`
- `src/Analyzer.Dsp/Math/RealFftFloatPlan.cs`
- `src/App.WinUI/ViewModels/SettingsPageViewModel.cs`
- `src/App.WinUI/Views/DevicesPage.Dashboard.cs`
- `src/MicaAudio.PanelRuntime/packages.lock.json` (projeto vazio removido)
- `tests/Analyzer.Dsp.Tests/FftPlanParityTests.cs`
- `tests/Analyzer.Dsp.Tests/FftSizePolicyTests.cs`
- `tests/Integration.Smoke/FirmwareCatalogSmokeTests.cs`

### Modificados (selecao principal)
- `src/Analyzer.Dsp/Analysis/EnvelopeSmoother.cs` - removido codigo morto
- `src/Analyzer.Dsp/Analysis/LogBandMapper.cs` - removido codigo morto
- `src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs` - removido codigo morto
- `src/Analyzer.Dsp/Analysis/SpectrumPowerProcessor.cs` - removido codigo morto
- `src/Analyzer.Dsp/Analysis/SpectrumSampleWindow.cs` - removido codigo morto
- `src/Analyzer.Dsp/Math/FftUtility.cs` - removido codigo morto
- `src/App.WinUI/Views/DevicesPage.*.cs` - removidos metodos orfaos e dashboard antigo
- `src/App.WinUI/Views/MainPage.*.cs` - removidos metodos orfaos
- `src/App.WinUI/Services/AudioPipelineCaptureProfile.cs` - ajustado apos remocao de `CaptureConfig.TargetChannels`
- `src/App.WinUI/Services/DefaultPresets.cs` - ajustado apos remocao de `GradientPalette.Name`
- `src/App.WinUI/Infrastructure/Observability/AppObservability.cs` - removida chave `PortNameKey`
- `src/Audio.Loopback/Capture/CaptureConfig.cs` - removida propriedade `TargetChannels`
- `src/Device.Server/Hosting/DeviceServerHost.Firmware.cs` - simplificado para retornar 501 NotImplemented
- `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs` - ajustado apos remocoes
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs` - ajustado apos remocoes
- `src/Device.Server/Hosting/DeviceServerHost.cs` - simplificado
- `src/Device.Server.Abstractions/Hosting/DeviceOfficialFirmwareCatalog.cs` - removida interface `IDeviceOfficialFirmwareCatalog`
- `src/MicaAudio.Core/Config/DeviceLifecycleSettings.cs` - removido codigo morto
- `src/MicaAudio.Core/Config/AnalyzerConfig.cs` - removido codigo morto
- `src/MicaAudio.Core/Led/LedPayload.cs` - removido codigo morto
- `src/MicaAudio.Core/Presets/GradientPalette.cs` - removida propriedade `Name`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs` - ajustado DI sem firmware catalog
- `src/Visual.Win2D/Engine/VisualizerEngine.cs` - removido codigo morto
- `src/Visual.Win2D/Engine/ReactiveBandSampler.cs` - removido codigo morto
- `src/Visual.Win2D/Engine/ReactiveBandSnapshot.cs` - removido codigo morto
- `src/Visual.Win2D/Engine/RendererControlSupport.cs` - removido codigo morto
- `src/Visual.Win2D/Renderers/*.cs` - removido codigo morto
- `tests/Output.Tests/DeviceServerHostMqttTests.cs` - adicionado `using System.Buffers;` e removidos helpers nao utilizados
- `tests/Output.Tests/DeviceServerTestHarness.cs` - adicionado `using System.Buffers;`
- `tests/Output.Tests/DeviceServerHostDashboardTests.cs` - ajustado apos remocoes
- `tests/Output.Tests/StructuredLoggingTests.cs` - removidas referencias a `PortNameKey`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs` - ajustado apos remocoes
- `tests/Output.Tests/Output.Tests.csproj` - removida diretiva `Compile Remove` orfa
- `tests/Analyzer.Dsp.Tests/*.cs` - ajustados apos remocoes
- `tests/Integration.Smoke/*.cs` - ajustados apos remocoes
- `docs/wiki/modules/analyzer-dsp.md` - removida referencia a `RealFftFloatPlan.cs`
- `docs/wiki/reference/cloud-first-control-plane-gap-map.md` - atualizado para refletir endpoints firmware simplificados

## Decisoes tomadas

1. `IDeviceOfficialFirmwareCatalog` foi removida por completo; os endpoints firmware agora retornam 501 NotImplemented, simplificando o servidor.
2. `MicaAudio.PanelRuntime` foi identificado como projeto vazio e removido da solution/disco.
3. `GradientPalette.Name` e `CaptureConfig.TargetChannels` foram removidas por nao terem consumidores ativos; os unicos usos foram removidos de App.WinUI e testes.
4. `RealFftFloatPlan` foi removido por ser codigo de benchmark/validacao nao utilizado no path produtivo.
5. Ambiguidade `CS0411` entre `ReadOnlySequence<byte>.ToArray()` e `ImmutableArrayExtensions.ToArray<T>()` foi resolvida usando `BuffersExtensions.CopyTo` com `using System.Buffers;` ao inves de `.ToArray()`.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> 1 falha (link quebrado RealFftFloatPlan)
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> falha por ausencia de handoff
dotnet build MicaAudio.sln -c Debug -> SUCESSO (0 erros)
```

Apos criacao deste handoff e correcao do link quebrado em `analyzer-dsp.md`, as validacoes devem passar.

## Riscos e rollback

- Risco: algum codigo removido ainda era utilizado indiretamente via reflection ou configuracao dinamica. Mitigacao: build limpo e testes de smoke passam.
- Risco: firmware endpoints retornando 501 podem quebrar fluxos de update no client remoto. Mitigacao: client remoto ja nao utilizava os endpoints oficiais (eram dead code).
- Rollback: restaurar arquivos deletados a partir do backup git e reverter modificacoes via `git checkout`.

## Proximos passos

1. Reexecutar `docs-validate.ps1` e `ai-governance-check.ps1` para confirmar zero falhas.
2. Commit das mudancas.
3. Continuar remocao de codigo morto em outras camadas se identificado.
