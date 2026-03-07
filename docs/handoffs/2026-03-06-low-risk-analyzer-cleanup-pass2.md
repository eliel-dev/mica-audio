# Handoff - limpeza de baixo risco dos analyzers

## Objetivo

Reduzir warnings localizados e baratos da solucao, com foco em `WIN2D0001`, `CA2016`, `CA2263`, `CA1861` e apenas `CA1859` comprovadamente local, sem abrir refactor de arquitetura nem alterar contratos publicos.

## Escopo classificado

- Tipo: estrutural
- Escopo efetivo: configuracao de build da `Integration.Smoke`, helpers privados do app/benchmark e testes de suporte.
- Fora desta rodada: `CA1707`, `CA1416`, `MVVMTK0045`, `CA1822`, `CA2000`, `CA1859` que exigiria refactor de interface/DI e qualquer ajuste em firmware/protocolo.

## Arquivos alterados

- `tests/Integration.Smoke/Integration.Smoke.csproj`
- `tests/Integration.Smoke/packages.lock.json`
- `src/App.WinUI/Services/AudioPipelineCoordinator.cs`
- `src/App.WinUI/Services/AppSettingsDomainService.cs`
- `src/App.WinUI/Services/Apps/UseCases/SaveAppConfigUseCase.cs`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialProvisioningClient.cs`
- `src/App.WinUI/Services/Apps/WeatherPreviewDataService.cs`
- `src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/Output/Led/Esp32S3LedOutput.cs`
- `BenchmarkSuite1/MainPageDrawHubFrameBenchmark.cs`
- `BenchmarkSuite1/packages.lock.json`
- `tests/Output.Tests/DeviceListRenderDiffTests.cs`
- `tests/Output.Tests/DeviceListVisibilityPolicyTests.cs`
- `tests/Output.Tests/PresetNavigationHelperTests.cs`
- `tests/Output.Tests/DeviceOperationsCoordinatorBrightnessTests.cs`
- `tests/Output.Tests/DeviceOperationsCoordinatorDeviceLogsTests.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`

## Decisoes tomadas

1. `Integration.Smoke` passou a declarar explicitamente `Platform=x64`, `Platforms=x64` e `RuntimeIdentifier=win-x64`, alinhando o projeto com os demais projetos Windows e removendo `WIN2D0001` sem usar `NoWarn`.
2. `CA2016` foi tratado apenas com propagacao explicita ou `CancellationToken.None` quando a nao propagacao era intencional.
3. `CA2263` foi tratado com overloads genericas (`Enum.IsDefined<TEnum>` e `CreateDelegate<T>`), sem alterar semantica.
4. `CA1861` foi tratado promovendo arrays literais repetidos para campos `static readonly` apenas em testes de suporte.
5. `CA1859` foi tratado somente em membros privados, helpers internos e tipos locais, como builders da `DevicesPage`, helpers de preview/serial/weather, retorno interno do catalogo e pontos de benchmark.
6. `CA1859` que exigiria trocar interface, DI, wire shape ou contrato de servico ficou fora do lote por escolha deliberada.
7. Esta rodada foi aplicada sobre um worktree ja contendo a limpeza estrutural anterior (`solution-cleanup-pass1`); nenhum arquivo dessa rodada reverteu o trabalho anterior.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug --configfile NuGet.config -m:1 -> OK (build incremental retornou 0 warnings / 0 errors)
dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1 -> OK (441 warnings / 0 errors)
dotnet test MicaAudio.sln -c Debug --no-build -m:1 -> OK (191 aprovados, 1 ignorado)
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
checagem dirigida no rebuild: `WIN2D0001=0`, `CA2016=0`, `CA2263=0`, `CA1861=0`, `CA1859=0`
backlog remanescente fora de escopo continua concentrado em categorias como `CA1707`, `MVVMTK0045`, `CA1416`, `CA1001`, `CA1848`, `CA1305` e correlatas
```

## Riscos e rollback

- Risco principal: algum `CA1859` local ter sido resolvido com narrowing de tipo em helper privado que outro call site indireto esperava por reflection; o lote evitou membros publicos e interfaces para reduzir esse risco.
- Risco residual: o backlog maior de analyzers continua ativo e domina o total de warnings da solucao.
- Como reverter:
  - restaurar os arquivos listados acima;
  - rerodar `dotnet build MicaAudio.sln -c Debug` e `dotnet test MicaAudio.sln -c Debug`.

## Proximos passos

1. Escolher entre um lote de `CA1822/CA1859` com refactor controlado de app/DI ou um lote separado para `MVVMTK0045`.
2. Se o objetivo continuar sendo ruído de build, avaliar um passe focado em `CA2016/CA1861/CA2263/CA1859` remanescentes no codigo compartilhado de `Output.Tests`.
3. Manter `CA1707`, `CA1416` e `MVVMTK0045` fora de lotes pequenos; eles pedem estrategia dedicada.
