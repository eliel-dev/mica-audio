# Handoff - estabilizacao crash de runtime na aba Apps

## Objetivo

Eliminar o crash ao alternar apps (especialmente saindo do `gifhub75`) sem remover as melhorias recentes de Apps/Dispositivos/Servidor.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: alternar entre `gifhub75`, `accuweather` e `analogclock` nao encerra o app; testes e validacoes locais permanecem verdes.

## Arquivos alterados

- src/App.WinUI/Services/Apps/GifHub75RuntimeProvider.cs
- src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs
- src/App.WinUI/Views/AppsPage.xaml.cs
- src/App.WinUI/Services/Apps/AppCatalogService.cs
- src/App.WinUI/App.xaml.cs
- tests/Output.Tests/GifCatalogAppRuntimeServiceTests.cs
- tests/Output.Tests/AppCatalogServiceTests.cs
- docs/handoffs/2026-02-23-apps-runtime-crash-stabilization.md

## Decisoes tomadas

1. Desacoplar assinatura de `FrameUpdated` do ciclo de vida completo do provider e vincular somente ao estado selecionado/desselecionado do app GIF.
2. Tornar a invalidacao de canvas resiliente a corrida de teardown/UI thread (`HasThreadAccess`, enqueue seguro e captura de excecoes de descarte).
3. Corrigir semantica de deselection na `AppsPage` para chamar `OnDeselected` com o item anterior real, evitando stop/restart com contexto incorreto.
4. Adicionar guardrail de regressao no runtime service para callback tardio com excecao nao derrubar o fluxo de parada.
5. Avancar parcialmente a padronizacao arquitetural prevista: `Salvar` e `Instalar` da AppsPage agora passam por use cases dedicados e o catalogo passou para estrategia seed-first (fonte de verdade no seed + merge de override do usuario).

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug -> OK (30 aprovados)
dotnet build MicaAudio.sln -c Debug -> FALHOU (APPX3217 em tests/Integration.Smoke por SDK UAP ausente no ambiente local)
```

## Riscos e rollback

- Risco principal: callbacks de frame podem ainda chegar durante transicao de estado em cenarios extremos de UI, exigindo ajuste adicional de sincronizacao no provider.
- Como reverter: restaurar a estrategia anterior de assinatura em `Attach`, removendo guardas novos; alternativa mais segura e incremental e manter assinatura por selecao e reforcar lock interno no runtime service.

## Proximos passos

1. Validar manualmente alternancia rapida de cards na aba Apps por alguns minutos e conferir ausencia de novas entradas de `InvalidCastException` no `crash.log`.
2. Na proxima etapa, concluir padronizacao arquitetural pendente (remover dependencias estaticas `App.*` da AppsPage e registrar runtime providers via DI).
3. Quando o ambiente local tiver SDK UAP completo, reexecutar `dotnet build MicaAudio.sln -c Debug` para fechar o gate integral fora do CI.
