# Handoff - Fase 5 de qualidade .NET 10 (core-first em Analyzer/Loopback)

## Objetivo

Melhorar a arquitetura e a testabilidade do core em `Analyzer.Dsp` e `Audio.Loopback` sem alterar os contratos publicos do pipeline nem o comportamento externo esperado pelo app.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta fase:
  - decompor o `SpectrumAnalyzer` em colaboradores internos nomeados;
  - centralizar janela PCM, FFT/power/weighting e layout de bandas em helpers testaveis;
  - normalizar `CaptureConfig` em runtime config dedicado no loopback;
  - extrair a criacao de `PcmFrame` do callback WASAPI para um factory interno;
  - adicionar cobertura deterministica para os novos colaboradores internos.
- Fora desta fase:
  - mudanca de contrato publico em `IAnalyzer`, `SpectrumFrame`, `ILoopbackCapture` ou `CaptureConfig`;
  - mudanca em `App.WinUI`, firmware, wire de devices ou `Device.Server`;
  - lapidacao de UX/visualizacao.

## Arquivos alterados

- Analyzer:
  - `src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs`
  - `src/Analyzer.Dsp/Analysis/SpectrumSampleWindow.cs`
  - `src/Analyzer.Dsp/Analysis/SpectrumPowerProcessor.cs`
  - `src/Analyzer.Dsp/Analysis/SpectrumBandLayout.cs`
  - `src/Analyzer.Dsp/Properties/AssemblyInfo.cs`
- Loopback:
  - `src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs`
  - `src/Audio.Loopback/Capture/LoopbackCaptureRuntimeConfig.cs`
  - `src/Audio.Loopback/Capture/LoopbackFrameFactory.cs`
  - `src/Audio.Loopback/Properties/AssemblyInfo.cs`
- Testes:
  - `tests/Analyzer.Dsp.Tests/SpectrumSampleWindowTests.cs`
  - `tests/Analyzer.Dsp.Tests/SpectrumPowerProcessorTests.cs`
  - `tests/Analyzer.Dsp.Tests/SpectrumBandLayoutTests.cs`
  - `tests/Integration.Smoke/LoopbackCaptureRuntimeConfigTests.cs`
  - `tests/Integration.Smoke/LoopbackFrameFactoryTests.cs`
- Documentacao:
  - `docs/wiki/modules/analyzer-dsp.md`
  - `docs/wiki/modules/audio-loopback.md`
  - `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `SpectrumAnalyzer` passou a atuar como orquestrador fino:
  - `SpectrumSampleWindow` concentra append, resize, copy e slide da janela;
  - `SpectrumPowerProcessor` concentra FFT, smoothing por bin, weighting e calculo de `Level`;
  - `SpectrumBandLayout` concentra mapeamento/agregacao de bandas de display e output.
- O contrato publico do analyzer foi preservado:
  - `Process(in PcmFrame frame)` continua sendo a porta unica;
  - `SpectrumFrame` e `Bands64` nao mudaram de shape;
  - a semantica externa de `DisplayMode`, scaling e smoothing foi mantida.
- `WasapiLoopbackCaptureService` continua como fronteira publica de captura, mas deixou de carregar inline regras de normalizacao e materializacao de frame:
  - `LoopbackCaptureRuntimeConfig` centraliza `Clamp/Max` de buffer e capacidade do canal;
  - `LoopbackFrameFactory` centraliza conversao PCM para `PcmFrame` e timestamp de captura.
- O isolamento em helpers internos foi preferido a redesenhar APIs publicas:
  - reduz complexidade do core;
  - melhora testabilidade deterministica;
  - preserva compatibilidade do app e dos testes de integracao existentes.

## Validacoes executadas

- `dotnet build MicaAudio.sln -c Debug --no-restore -m:1`
  - OK
  - baseline parcial: `0 warnings`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - OK
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
  - OK
  - baseline final: `0 warnings`
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
  - OK
  - `217` aprovados
  - `1` ignorado

## Riscos e rollback

- Risco funcional principal:
  - qualquer divergencia de comportamento agora tende a ficar confinada aos helpers internos do analyzer ou do loopback, sem mudar contratos.
- Pontos sensiveis:
  - drift de hop/window em `SpectrumSampleWindow`;
  - alteracao acidental de weighting/level em `SpectrumPowerProcessor`;
  - normalizacao excessiva de `CaptureConfig` em `LoopbackCaptureRuntimeConfig`;
  - diferenca de timestamp/conversao PCM no `LoopbackFrameFactory`.
- Rollback:
  - restaurar a logica inline antiga de `SpectrumAnalyzer`;
  - restaurar a logica inline antiga de `WasapiLoopbackCaptureService`;
  - remover os novos helpers internos e seus testes dedicados.

## Proximos passos

- Proxima onda natural de lapidacao:
  - revisar `MicaAudio.Core` e `AudioPipelineCoordinator` com o mesmo criterio core-first;
  - apertar invariantes de configuracao/presets sem tocar em UX;
  - seguir para uma fase focada em reducao de complexidade ciclomática e contratos internos do app.
