# Handoff - Fase 1 de qualidade .NET 10

## Objetivo

Estabelecer a primeira baseline de qualidade alinhada ao `.NET 10`, com governanca obrigatoria para IA, regras de analyzer por escopo e eliminacao dos grupos de warnings priorizados nesta fase.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta fase:
  - governanca de IA (`AGENTS`, manifesto, schema, consistencia e script de validacao);
  - baseline de qualidade por escopo em `.editorconfig`;
  - adequacao de codigo para zerar `MVVMTK0045`, `CA1416`, `CA1848`, `CA1873`, `CA2254`, `CA2208`, `CA1513`, `CA1001`, `CA2000` e `CA1305`;
  - manter `CA1707` fora da baseline de testes e `CA5394` fora da baseline de benchmark.
- Fora desta fase:
  - backlog residual de `CA1822`, `CA1805`, `CA1725`, `CA1826`, `CA1865`, `CA1716`, `CA1068`, `xUnit1030`;
  - refactor amplo de arquitetura/DI;
  - renomeacao em massa de testes.

## Arquivos alterados

- Governanca e baseline:
  - `AGENTS.md`
  - `.editorconfig`
  - `docs/wiki/reference/ai-contract.v1.yaml`
  - `docs/wiki/reference/ai-contract.schema.json`
  - `docs/wiki/ai/consistencia-codex.md`
  - `scripts/ai-governance-check.ps1`
- App.WinUI / qualidade .NET 10:
  - `src/App.WinUI/App.WinUI.csproj`
  - `src/App.WinUI/App.xaml.cs`
  - `src/App.WinUI/ViewModels/AppsPageViewModel.cs`
  - `src/App.WinUI/ViewModels/DevicesPageViewModel.cs`
  - `src/App.WinUI/ViewModels/MainPageViewModel.cs`
  - `src/App.WinUI/ViewModels/ShellPageViewModel.cs`
  - `src/App.WinUI/Infrastructure/Serial/SerialProvisioningClient.cs`
  - `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
  - `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
  - `src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs`
  - `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
  - `src/App.WinUI/Services/Apps/AppModifierStateStore.cs`
  - `src/App.WinUI/Services/Apps/UseCases/StartLocalRuntimeUseCase.cs`
  - `src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs`
  - `src/App.WinUI/Services/Gif/Hub75GifDecoder.cs`
  - `src/App.WinUI/Views/AppsPage.xaml.cs`
  - `src/App.WinUI/Views/DevicesPage.xaml.cs`
  - `src/App.WinUI/Views/MainPage.xaml.cs`
  - `src/App.WinUI/Views/MainPage.Dispose.cs`
  - `src/App.WinUI/Views/ServerPage.xaml.cs`
- Bibliotecas compartilhadas:
  - `src/Analyzer.Dsp/Analysis/LogBandMapper.cs`
  - `src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs`
  - `src/Device.Server/Hosting/DeviceServerHost.cs`
- Testes:
  - `tests/Output.Tests/AppConfigUseCasesTests.cs`
  - `tests/Output.Tests/AppModifierStateStoreTests.cs`
  - `tests/Output.Tests/DeviceIntegrationServiceLegacyWsSettingTests.cs`
  - `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
  - `tests/Output.Tests/Esp32S3LedOutputTests.cs`
  - `tests/Output.Tests/GifCatalogAppRuntimeServiceTests.cs`
  - `tests/Output.Tests/Hub75GifServicesTests.cs`

## Decisoes tomadas

- A politica obrigatoria para IA ficou canonizada em `AGENTS.md`, `ai-contract.v1.yaml`, `ai-contract.schema.json` e validada em `ai-governance-check.ps1`.
- A baseline passou a ser controlada por escopo em `.editorconfig`:
  - `[*.cs]` protege categorias ja limpas (`CA2016`, `CA2263`, `CA1861`, `CA1859`);
  - `[src/**/*.cs]` trata os analyzers de qualidade do produto como `error`;
  - `[tests/**/*.cs]` remove `CA1707` da baseline e mantem corretude/lifetime como `error`;
  - `[BenchmarkSuite1/**.cs]` remove `CA5394` do benchmark.
- Os ViewModels WinUI foram convertidos para `partial properties` com `ObservableProperty`.
- O projeto `App.WinUI` passou para `LangVersion=preview` para habilitar `partial properties` do CommunityToolkit.Mvvm 8.4.0. Esta e uma decisao tecnica consciente e localizada, documentada aqui por depender do estado atual do Toolkit em `2026-03-06`.
- O logging foi endurecido com `LoggerMessage` source-generated nos pontos que disparavam `CA1848`, `CA1873` e `CA2254`.
- Os caminhos compartilhados de GIF que usam `System.Drawing` foram anotados como Windows-only para zerar `CA1416` sem supressao ampla.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - OK
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
  - OK
  - baseline inicial desta fase: `441` warnings
  - baseline final desta fase: `86` warnings
  - categorias zeradas nesta fase: `MVVMTK0045`, `CA1416`, `CA1848`, `CA1873`, `CA2254`, `CA2208`, `CA1513`, `CA1001`, `CA2000`, `CA1305`, `CA1707`, `CA5394`, `CA2016`, `CA2263`, `CA1861`, `CA1859`
  - backlog residual dominante: `CA1822 (36)`, `xUnit1030 (16)`, `CA1805 (8)`, `CA1725 (8)`, `CA1826 (4)`, `CA1865 (4)`, `CA1716 (4)`, `CA1068 (4)`, `CA1852 (2)`
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
  - OK
  - `191` aprovados
  - `1` ignorado
- Validacao manual WinUI:
  - `src\App.WinUI\bin\x64\Debug\net10.0-windows10.0.22621.0\win-x64\App.WinUI.exe`
  - iniciou e permaneceu em execucao apos 5s (`PID 5488`)

## Riscos e rollback

- Risco principal desta fase: `App.WinUI` agora depende de `LangVersion=preview` para sustentar `partial properties` do CommunityToolkit.Mvvm 8.4.0.
- Se for necessario rollback rapido:
  - voltar `src/App.WinUI/App.WinUI.csproj` para `LangVersion=latest`;
  - restaurar os quatro ViewModels para o padrao baseado em campos;
  - manter a governanca de IA e a baseline de analyzers, que sao independentes dessa decisao.
- Nao houve mudanca de API funcional publica nem mudanca de protocolo wire.

## Proximos passos

- Wave 2 recomendada:
  - reduzir `CA1822` apenas onde o narrowing para `static` for local e sem churn;
  - limpar `xUnit1030` em `Integration.Smoke`;
  - revisar `CA1725`, `CA1068`, `CA1716`, `CA1805`, `CA1826`, `CA1865` em lotes pequenos;
  - reavaliar a dependencia de `LangVersion=preview` quando houver versao do Toolkit/documentacao oficial que elimine essa necessidade.
