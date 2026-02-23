## Objetivo

Recuperar o App.WinUI (build + startup) e consolidar pilares de qualidade para continuidade: DI explicita, options centralizadas, logging estruturado e cobertura de regressao em composicao/use cases.

## Escopo classificado

Estrutural (bootstrap, composicao DI, configuracao de persistencia e testes de integracao de startup).

## Arquivos alterados

- `src/App.WinUI/App.xaml.cs`
- `src/MicaAudio.Core/Config/MicaAudioOptions.cs`
- `src/App.WinUI/Services/PresetRepository.cs`
- `src/App.WinUI/Services/SettingsRepository.cs`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Services/Apps/AppModifierStateStore.cs`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/ServerPage.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Output.Tests/AppCatalogServiceTests.cs`
- `tests/Output.Tests/AppCatalogRuntimeKindTests.cs`
- `tests/Output.Tests/AppModifierStateStoreTests.cs`
- `tests/Output.Tests/AppConfigUseCasesTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `docs/adr/0005-di-options-logging-canonical.md`

## Decisoes tomadas

1. `MicaAudioOptions` ampliado para concentrar paths de persistencia/diagnostico.
2. Repositorios/servicos de estado migrados para `IOptions<MicaAudioOptions>`.
3. Paginas principais registradas e resolvidas por DI, com construtor publico DI-friendly.
4. `WriteCrashLog` passou a priorizar `ILogger` e usar arquivo apenas como fallback.
5. Smoke tests reforcados para validar resolucao DI, construtores publicos e options preenchidas.
6. Testes de use case/store adicionados para reduzir risco de regressao funcional na aba Apps.

## Validacoes executadas

- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug` (ok)
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug` (ok)
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` (ok)
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` (ok)
- `dotnet build MicaAudio.sln -c Debug` (falha local esperada por `APPX3217` em `tests/Integration.Smoke` sem SDK UAP)

## Riscos e rollback

- Risco de regressao de bootstrap se novos servicos nao forem registrados no container.
- Risco de falha de ativacao se paginas voltarem a construtores nao compativeis com DI.
- Rollback: reverter os commits desta iniciativa e restaurar composition root anterior.

## Proximos passos

1. Resolver `APPX3217` no ambiente local (instalar SDK UAP exigido) para alinhamento com gate completo.
2. Reduzir warnings de analyzers (`CA1848`, `CA2254`) com `LoggerMessage` em pontos de maior volume.
3. Evoluir pages para construtores publicos 100% explicitos (sem `IServiceProvider`) quando os tipos internos forem promovidos/organizados.
