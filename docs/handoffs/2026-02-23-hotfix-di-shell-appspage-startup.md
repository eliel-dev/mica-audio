# Handoff - hotfix DI ShellPage/AppsPage startup

## Objetivo

Corrigir falha de inicializacao do app causada por ativacao DI com construtores nao publicos em paginas registradas no container.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: app inicia sem fallback de erro por `InvalidOperationException` em `ShellPage` e os PRs recentes permanecem ativos.

## Arquivos alterados

- src/App.WinUI/Views/ShellPage.xaml.cs
- src/App.WinUI/Views/AppsPage.xaml.cs
- tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs
- docs/handoffs/2026-02-23-hotfix-di-shell-appspage-startup.md

## Decisoes tomadas

1. Introduzir construtor publico via `IServiceProvider` em `ShellPage` e `AppsPage`, preservando o construtor detalhado `internal` para manter encapsulamento dos servicos internos.
2. Manter a composicao DI em `App.xaml.cs` sem refatoracao ampla, evitando rollback das melhorias recentes.
3. Adicionar guardrail em `WinUiBootstrapSmokeTests` para exigir construtor publico nas paginas criticas do startup (`ShellPage` e `AppsPage`).

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK
dotnet build MicaAudio.sln -c Debug -> FALHOU (APPX3217 em tests/Integration.Smoke por SDK UAP ausente no ambiente local)
```

## Riscos e rollback

- Risco principal: o construtor publico com `IServiceProvider` pode mascarar falhas de registro se novos servicos forem adicionados sem testes.
- Como reverter: remover o construtor publico e voltar para ativacao por factory explicita (`AddTransient<T>(sp => new T(...))`) ou tornar todos os tipos injetados publicos (nao recomendado).

## Proximos passos

1. Validar manualmente a inicializacao do app e navegacao entre Visualizador, Dispositivos, Apps e Servidor.
2. No CI, usar `dotnet build MicaAudio.sln -c Debug` como gate final (ambiente completo com SDK requerido).
