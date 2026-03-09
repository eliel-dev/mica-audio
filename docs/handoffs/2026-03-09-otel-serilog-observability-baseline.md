# Handoff - Baseline de observabilidade com OpenTelemetry + Serilog

## Objetivo

Adicionar uma trilha tecnica de observabilidade para `App.WinUI` e `Device.Server`, com logs estruturados locais, spans manuais nos fluxos operacionais principais e metricas customizadas para reduzir tempo de diagnostico.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `Serilog` vira provider principal de logs tecnicos, `OpenTelemetry` cobre `traces`/`metrics` quando configurado por env vars, os fluxos HTTP/deploy/onboarding/device-command ficam instrumentados, docs/backlinks sao atualizados e as validacoes estruturais passam.

## Arquivos alterados

- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Infrastructure/Observability/AppObservability.cs`
- `src/App.WinUI/Infrastructure/Observability/ObservabilityOptions.cs`
- `src/App.WinUI/Services/Apps/CityAutocompleteService.cs`
- `src/App.WinUI/Services/Apps/OpenMeteoForecastClient.cs`
- `src/App.WinUI/Services/Apps/WeatherPreviewDataService.cs`
- `src/App.WinUI/Services/Apps/UseCases/DeployAppUseCase.cs`
- `src/App.WinUI/Services/Apps/UseCases/SaveAppConfigUseCase.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs`
- `src/Device.Server/Device.Server.csproj`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerObservability.cs`
- `src/Device.Server/Hosting/PendingTrackedCommand.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/ObservabilityTestHelpers.cs`
- `tests/Output.Tests/ObservabilityOptionsTests.cs`
- `tests/Output.Tests/StructuredLoggingTests.cs`
- `tests/Output.Tests/CityAutocompleteServiceTests.cs`
- `tests/Output.Tests/OpenMeteoForecastClientTests.cs`
- `tests/Output.Tests/WeatherPreviewDataServiceTests.cs`
- `tests/Output.Tests/AppConfigUseCasesTests.cs`
- `tests/Output.Tests/DeviceOperationsCoordinatorBrightnessTests.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/OnboardingObservabilityTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/device-operations-coordinator.md`

## Decisoes tomadas

1. `Serilog` ficou como pipeline unico de logs tecnicos; `OpenTelemetry` nao foi habilitado para logs via `ILogger`, evitando duplicacao.
2. O export OTLP ficou desligado por default e so sobe quando `OTEL_EXPORTER_OTLP_ENDPOINT` esta configurado; sem endpoint, o app continua apenas com logs locais estruturados.
3. A configuracao OTEL foi separada por responsabilidade:
   - `AppObservability` cobre `HttpClient`, UI/startup, deploy e onboarding;
   - `DeviceServerObservability` cobre `AspNetCore`, tracked commands e metricas do host embutido.
4. O host embutido reutiliza o `Serilog.Log.Logger` global, entao app e servidor gravam na mesma trilha `engineering-.clef`.
5. O path de comando tracked agora registra progresso e conclusao dentro do mesmo span via `PendingTrackedCommand`, preservando a correlacao `deviceId + commandId`.
6. A superficie local do usuario foi preservada:
   - `AppLogStore` continua ativo;
   - `crash.log` continua sendo gravado em falhas de startup/UI.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test tests/Output.Tests/Output.Tests.csproj --filter "FullyQualifiedName~ObservabilityOptionsTests|FullyQualifiedName~StructuredLoggingTests|FullyQualifiedName~CityAutocompleteServiceTests|FullyQualifiedName~OpenMeteoForecastClientTests|FullyQualifiedName~WeatherPreviewDataServiceTests|FullyQualifiedName~AppConfigUseCasesTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests|FullyQualifiedName~OnboardingObservabilityTests|FullyQualifiedName~DeviceServerHostMqttTests" --no-build -> OK (37 aprovados)
```

## Riscos e rollback

- Risco principal: a trilha de observabilidade adiciona novos pacotes e aumenta a superficie de bootstrap de DI/logging; regressao tipica seria falha de inicializacao por configuracao incorreta do logger/exporter.
- Como reverter:
  - remover `AppObservability` / `DeviceServerObservability` do bootstrap;
  - restaurar `AddDebug()` como provider simples no app;
  - remover os pacotes `OpenTelemetry.*` e `Serilog.*` adicionados nesta etapa;
  - recolocar os logs operacionais apenas no caminho textual/local preexistente.

## Proximos passos

1. Fazer smoke manual no app cobrindo autocomplete, card de clima, onboarding USB, deploy de app e timeout de comando em device real.
2. Se surgir backend OTLP no ambiente, validar ingestao do `engineering-.clef` local e dos spans exportados com `service.name = mica-audio-desktop`.
3. Expandir a mesma base para futuras integracoes de catalogo/news/scores/finance sem reintroduzir pipelines paralelos de logging.
