# ADR 0005 - DI explicita, Options centralizadas e Logging estruturado no App.WinUI

## Contexto

A camada `App.WinUI` estava com bootstrap parcialmente migrado, mistura de acesso estatico e injetado, e configuracoes de path espalhadas.
Esse estado gerou regressoes de ativacao por DI e erros de compilacao ao migrar repositorios para `IOptions<MicaAudioOptions>`.

## Decisao

1. Adotar composition root unico em `App.BuildServiceProvider()`.
2. Resolver paginas de startup por DI com construtor publico DI-friendly.
3. Centralizar paths de persistencia em `MicaAudioOptions` e injetar `IOptions<MicaAudioOptions>` nos repositorios/servicos de estado.
4. Padronizar logging em bootstrap e servicos criticos com `ILogger<T>`, mantendo fallback de crash log em arquivo apenas como ultimo recurso.
5. Manter `App.*` apenas para estado global de janela/chrome (`MainWindow`, `SetShellChromeHidden`) nesta etapa.

## Consequencias

- Reduz acoplamento de borda e melhora previsibilidade de bootstrap.
- Facilita testes de composicao DI e validacao de configuracao.
- Evita repeticao de strings de path fora do composition root.
- Exige disciplina para registrar novos servicos/paginas no container.

## Status

Aceita

## Data

2026-02-23

## Referencias

- docs/wiki/modules/app-winui.md
- docs/wiki/modules/settings-presets-persistence.md
- src/App.WinUI/App.xaml.cs
- src/MicaAudio.Core/Config/MicaAudioOptions.cs
- tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs
