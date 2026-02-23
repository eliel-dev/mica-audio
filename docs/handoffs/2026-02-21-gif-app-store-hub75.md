# Handoff - GIF App Store HUB75 (MVP)

## Objetivo

Migrar o fluxo de GIF para o catalogo/loja (`AppsPage`) como app `gifhub75`, com suporte a URL direta e arquivo local, formatacao HUB75 64x32, playback fixo em 12 FPS e stream RGB565 para devices.

## Escopo classificado

- Classificacao: `firmware/protocolo` (impacta `src/`, `Device.Protocol`, `Output`, `firmware` e documentacao).
- Entrega focada no MVP de runtime desktop no AppsPage.

## Arquivos alterados

- `src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/AppData/apps-catalog.seed.json`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.Ui.cs`
- `src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs`
- `src/App.WinUI/Views/Controls/AppCatalogCardControl.cs`
- `src/App.WinUI/Views/Controls/Renderers/GifPreviewRenderer.cs`
- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/GifCatalogAppRuntimeServiceTests.cs`
- `tests/Output.Tests/AppCatalogServiceTests.cs`
- `docs/wiki/modules/apps-catalog-deployment.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/modules/firmware-matrixportal-s3.md`
- `docs/wiki/reference/ws-protocol-v1.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/guides/load-gif-hub75.md`

## Decisoes tomadas

- O app GIF ficou centralizado na loja (`gifhub75`) e com preview/runtime dedicados no `AppsPage`.
- Auto-start do GIF ocorre apenas em clique manual no card (na selecao programatica nao inicia).
- `sourceMode=url|file` + `gifUrl` + `scaleMode=fit|fill|stretch` sao os modificadores oficiais do MVP.
- Ao `Stop()` do runtime GIF, e enviado payload legado tipo `1` com `bins64` zerado para forcar retorno imediato do firmware ao modo barras.
- `MainPage` teve o fluxo GIF ocultado para evitar duplicidade de entrada.

## Validacoes executadas

- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug` (sucesso)
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug` (sucesso; 26 testes aprovados)
- Cobertura adicionada:
  - `AppCatalogService` inclui `gifhub75` quando habilitado.
  - `GifCatalogAppRuntimeService` valida URL invalida, limite de download, timeout, cap de frames (via decoder cap) e `Stop()` com bins zerado.

## Riscos e rollback

- Risco: navegacao sem clique manual pode deixar runtime GIF aguardando configuracao (comportamento esperado do requisito).
- Risco: testes de GIF com `System.Drawing` sao dependentes de ambiente Windows.
- Rollback rapido:
  1. Remover `gifhub75` de `EnabledAppIds`/seed.
  2. Remover painel runtime e chamadas de `GifCatalogAppRuntimeService` no `AppsPage`.
  3. Restaurar visibilidade do seletor de modo GIF na `MainPage` (se desejado).

## Proximos passos

- Adicionar capability handshake opcional para detectar firmware sem suporte a `messageType=2`.
- Evoluir persistencia opcional de ultimo arquivo local escolhido (com opt-in).
- Incluir testes de UI/integracao para fluxo de selecao manual + salvar + troca de app.
