# Handoff - RSK-003 trilha faseada de upgrade .NET (Extensions + WebView2)

## Objetivo

Executar a trilha RSK-003 em fases (runtime, testes e benchmark) para reduzir drift de `Microsoft.Extensions.*` e `Microsoft.Web.WebView2`, mantendo rollback simples por pin no `csproj`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: fases aplicadas com lockfiles sincronizados, build/test verdes, sem vulnerabilidades conhecidas e evidencia documental versionada.

## Arquivos alterados

- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/packages.lock.json`
- `tests/Analyzer.Dsp.Tests/Analyzer.Dsp.Tests.csproj`
- `tests/Analyzer.Dsp.Tests/packages.lock.json`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/packages.lock.json`
- `tests/Integration.Smoke/Integration.Smoke.csproj`
- `tests/Integration.Smoke/packages.lock.json`
- `BenchmarkSuite1/BenchmarkSuite1.csproj`
- `BenchmarkSuite1/packages.lock.json`
- `docs/wiki/guides/criticality-context7-audit.md`
- `docs/handoffs/2026-03-03-rsk-003-dotnet-drift-upgrade-track.md`

## Decisoes tomadas

1. Fase 1 (runtime) aplicada apenas no `App.WinUI`, sem alterar `WindowsAppSDK` e `Win2D` nesta rodada:
   - `Microsoft.Extensions.DependencyInjection`: `8.0.1 -> 10.0.3`
   - `Microsoft.Extensions.Logging`: `8.0.1 -> 10.0.3`
   - `Microsoft.Extensions.Logging.Debug`: `8.0.1 -> 10.0.3`
   - `Microsoft.Web.WebView2`: `1.0.3719.77 -> 1.0.3800.47`
2. Fase 2 (toolchain de testes) aplicada nos 3 projetos de teste:
   - `Microsoft.NET.Test.Sdk`: `17.14.1 -> 18.3.0`
   - `coverlet.collector`: `6.0.4 -> 8.0.0`
   - `xunit.runner.visualstudio`: `3.1.4 -> 3.1.5`
   - `xunit` mantido em `2.9.3`.
3. Fase 3 (benchmark lane):
   - `BenchmarkDotNet`: `0.15.2 -> 0.15.8`.
4. Ajuste minimo de confiabilidade no benchmark:
   - `BenchmarkSuite1.csproj` corrigido para `SetConfiguration="Configuration=Debug"` no `ProjectReference` do `App.WinUI`, eliminando erro `MSB3100`.
5. Lockfiles foram sincronizados com `dotnet restore MicaAudio.sln`.

## Validacoes executadas

```text
dotnet restore MicaAudio.sln -> OK
dotnet list MicaAudio.sln package --outdated --include-transitive -> OK (drift residual transitive identificado)
dotnet list MicaAudio.sln package --vulnerable --include-transitive -> OK (sem vulnerabilidades)

dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~WinUiBootstrapSmokeTests|FullyQualifiedName~DevicesPageSmokeTests" -> OK (9/9)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~Hub75VisualizerSessionServiceTests" -> OK (24/24)
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test MicaAudio.sln -c Debug --no-build -> OK (172 pass, 1 skip)

dotnet test tests/Analyzer.Dsp.Tests/Analyzer.Dsp.Tests.csproj -c Debug -> OK (31/31)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug -> OK (119/119)
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug -> OK (22 pass, 1 skip)

dotnet build BenchmarkSuite1/BenchmarkSuite1.csproj -c Debug -> OK
dotnet run --project BenchmarkSuite1/BenchmarkSuite1.csproj -c Debug --framework net8.0-windows10.0.22621.0 -- --list flat -> OK (BenchmarkDotNet validou entrada; avisou sobre build Debug)

powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

## Riscos e rollback

- Risco residual: ainda existe backlog de pacotes transitivos fora do escopo P1 (`Microsoft.Windows.SDK.BuildTools`, `System.*`, `Newtonsoft.Json` em alguns projetos).
- Risco residual: warning `WIN2D0001` permanece no `Integration.Smoke` por build AnyCPU.
- Rollback rapido:
  - reverter pins nos `csproj` para:
    - `App.WinUI`: `Microsoft.Extensions.*=8.0.1`, `WebView2=1.0.3719.77`
    - `tests/*`: `Microsoft.NET.Test.Sdk=17.14.1`, `coverlet.collector=6.0.4`, `xunit.runner.visualstudio=3.1.4`
    - `BenchmarkSuite1`: `BenchmarkDotNet=0.15.2`
  - executar `dotnet restore MicaAudio.sln` para ressincronizar `packages.lock.json`
  - rerodar `dotnet build MicaAudio.sln -c Debug` e `dotnet test MicaAudio.sln -c Debug --no-build`.

## Proximos passos

1. Abrir lote RSK-003.1 para drift transitive com priorizacao por impacto operacional (App.WinUI e Integration.Smoke primeiro).
2. Corrigir warning estrutural de `Integration.Smoke` (plataforma/RID) para remover `WIN2D0001` do pipeline.
3. Rodar benchmark em `Release` para baseline de performance pos-upgrade, sem dependencia da validacao em Debug.
