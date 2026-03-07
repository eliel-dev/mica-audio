# Handoff - Fase 6 / Onda 1 (invariantes e contratos centrais do core)

## Objetivo

Consolidar invariantes de visualizer, lifecycle de devices e payload HUB75 em um caminho unico e testavel no core, sem mudar o shape publico de `AppSettings`, `PresetDefinition`, `AnalyzerConfig`, `LedPayload` ou `MicaAudioOptions`.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta onda:
  - introduzir `VisualizerRuntimeSettings` como fonte unica de defaults/coercao do visualizer;
  - introduzir `AnalyzerRuntimeProfile` para compor `AnalyzerConfig` a partir de `settings + preset + viewport`;
  - introduzir `DeviceLifecycleSettings` para centralizar `Fresh < Stale < Dormant`;
  - introduzir `LedPayloadFactory` para criar `LedPayload` sem montagem inline repetida;
  - alinhar `AppSettingsDomainService`, `DeviceLifecycleThresholds`, `VisualizerAnalyzerConfigFactory` e o smoke test de pipeline ao novo fluxo.
- Fora desta onda:
  - refactor do runtime/lifecycle de captura;
  - decomposicao da `MainPage`;
  - qualquer mudanca de wire/protocolo.

## Arquivos alterados

- Core:
  - `src/MicaAudio.Core/Config/VisualizerRuntimeDefaults.cs`
  - `src/MicaAudio.Core/Config/VisualizerRuntimeSettings.cs`
  - `src/MicaAudio.Core/Config/AnalyzerRuntimeProfile.cs`
  - `src/MicaAudio.Core/Config/DeviceLifecycleSettings.cs`
  - `src/MicaAudio.Core/Led/LedPayloadFactory.cs`
  - `src/MicaAudio.Core/Properties/AssemblyInfo.cs`
- App / adaptadores:
  - `src/App.WinUI/Services/AppSettingsDomainService.cs`
  - `src/App.WinUI/Services/Devices/DeviceLifecycleThresholds.cs`
  - `src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs`
  - `src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs`
  - `tests/Integration.Smoke/PipelineSmokeTests.cs`
- Testes:
  - `tests/Output.Tests/VisualizerRuntimeSettingsTests.cs`
  - `tests/Output.Tests/AnalyzerRuntimeProfileTests.cs`
  - `tests/Output.Tests/DeviceLifecycleSettingsTests.cs`
  - `tests/Output.Tests/LedPayloadFactoryTests.cs`

## Decisoes tomadas

- `VisualizerRuntimeSettings` passou a concentrar todas as regras de:
  - preset/renderer default;
  - FFT size;
  - smoothing;
  - weighting;
  - faixa de frequencia;
  - `linearBoost`;
  - `barCount`.
- `AnalyzerRuntimeProfile` preserva `AnalyzerConfig` como contrato publico, mas remove a responsabilidade de `MainPage`/factory antiga de repetir clamp/default a cada rebuild.
- `DeviceLifecycleSettings` virou a fonte de verdade para thresholds de presence, e `DeviceLifecycleThresholds` ficou apenas como adaptador de leitura.
- `LedPayloadFactory` passou a ser o caminho canonico para:
  - `SpectrumFrame -> Bins128`;
  - `Frame128x64 -> LedPayload`;
  - remapeamento `bands -> 128 bins`.

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
  - divergencia acidental entre regras antigas da UI e o novo runtime profile centralizado.
- Como o shape publico nao mudou, rollback e direto:
  - restaurar clamps/defaults inline em `AppSettingsDomainService`, `VisualizerAnalyzerConfigFactory` e `MainPage`;
  - remover `VisualizerRuntimeSettings`, `AnalyzerRuntimeProfile`, `DeviceLifecycleSettings` e `LedPayloadFactory`.

## Proximos passos

- Onda 2 da fase 6:
  - decompor o runtime do pipeline (`AudioPipelineCoordinator`);
  - separar lifecycle, roteamento e processador de frame;
  - tornar start/stop/troca de analyzer mais testaveis.
