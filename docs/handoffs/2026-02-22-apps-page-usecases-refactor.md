# Handoff - AppsPage use cases

## Objetivo

Extrair regras de negocio de salvar/instalar/validacao/runtime local GIF de `AppsPage` para casos de uso em `Services/Apps/UseCases`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `AppsPage` delega operacoes de negocio para use cases e nao conhece payloads de comando de dispositivo.

## Arquivos alterados

- src/App.WinUI/Views/AppsPage.xaml.cs
- src/App.WinUI/App.xaml.cs
- src/App.WinUI/Services/Apps/UseCases/SaveAppConfigUseCase.cs
- src/App.WinUI/Services/Apps/UseCases/AppConfigValidationUseCase.cs
- src/App.WinUI/Services/Apps/UseCases/DeployAppUseCase.cs
- src/App.WinUI/Services/Apps/UseCases/StartLocalRuntimeUseCase.cs
- docs/wiki/modules/apps-catalog-deployment.md

## Decisoes tomadas

1. `AppsPage` manteve leitura dos controles e binding, mas passou a delegar persistencia/deploy/runtime para use cases.
2. A validacao de `configJson` foi centralizada em `AppConfigValidationUseCase` para reutilizacao e isolamento da regra de parse tipado.
3. `StartLocalRuntimeUseCase` encapsulou estado de runtime (busy/status/file/cancellation) para reduzir acoplamento da pagina.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File ./scripts/docs-validate.ps1 -> falhou (powershell indisponivel no ambiente)
powershell -ExecutionPolicy Bypass -File ./scripts/ai-governance-check.ps1 -> falhou (powershell indisponivel no ambiente)
pwsh -ExecutionPolicy Bypass -File ./scripts/docs-validate.ps1 -> falhou (pwsh indisponivel no ambiente)
dotnet build MicaAudio.sln -c Debug -> falhou (dotnet indisponivel no ambiente)
```

## Riscos e rollback

- Risco principal: regressao em fluxo do runtime GIF por mudanca de ownership do estado de UI para use case.
- Como reverter: restaurar `AppsPage.xaml.cs` para fluxo inline anterior e remover wiring dos novos use cases em `App.xaml.cs`.

## Proximos passos

1. Executar checks obrigatorios em ambiente com PowerShell + .NET SDK.
2. Adicionar testes unitarios para `AppConfigValidationUseCase` e `DeployAppUseCase`.
