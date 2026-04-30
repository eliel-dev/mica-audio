# Handoff - Panels stability static first

## Objetivo

Estabilizar a sessao `Paineis` para eliminar crash e consumo excessivo de CPU/RAM na abertura da galeria e no editor local.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: galeria lazy e estatica, preview do editor opt-in, compositor com poster separado do playback e validacoes obrigatorias passando.

## Arquivos alterados

- src/App.WinUI/Views/PanelsPage.xaml.cs
- src/App.WinUI/Views/PanelsPage.Ui.cs
- src/App.WinUI/Views/Controls/Hub75PanelThumbnailControl.cs
- src/App.WinUI/Views/Controls/Hub75PanelEditorControl.cs
- src/App.WinUI/Services/Panels/PanelsFrameComposer.cs
- src/App.WinUI/Services/Panels/PanelsMediaCache.cs
- src/App.WinUI/Services/Panels/PanelsPlaybackService.cs
- src/App.WinUI/Services/Gif/Hub75GifDecoder.cs
- tests/Integration.Smoke/PanelsPageSmokeTests.cs
- tests/Integration.Smoke/PanelsFrameComposerTests.cs
- docs/wiki/modules/paineis.md
- docs/wiki/reference/code-index.md

## Decisoes tomadas

1. A galeria passou a ser `static first`, com posters lazy via `ItemsSource` + `ContainerContentChanging`, sem rebuild global de thumbnails no `Loaded`.
2. O editor abre com preview desligado e so cria `PanelCompositionSession` quando o toggle `Preview` e ativado pelo usuario.
3. O compositor separa `CreatePosterAsync(...)` de `CreateSessionAsync(...)`, usando `PanelsMediaCache` para compartilhar poster/frame animado e evitar redecodificacao pesada.
4. O poster de `gifhub75` agora decodifica apenas o primeiro frame util; slideshow animado continua restrito ao playback real ou preview manual.
5. O hot path de frame foi simplificado para reduzir clones redundantes entre playback e controles HUB75.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1
dotnet build MicaAudio.sln -c Debug
dotnet test tests\Output.Tests\Output.Tests.csproj -c Debug --no-build
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-build
```

## Riscos e rollback

- Risco principal: poster estatico mascarar regressao que antes aparecia apenas no preview animado automatico.
- Como reverter: restaurar o fluxo anterior de preview continuo em `PanelsPage`, removendo `PanelsMediaCache` e o gate `Preview`, no mesmo conjunto de arquivos.

## Proximos passos

1. Medir tempo de abertura da galeria com um conjunto maior de paineis e midias locais pesadas.
2. Se ainda houver pico perceptivel, trocar os posters para geracao em background com limite explicito de concorrencia.
3. Considerar counters simples de debug para poster hits/misses e sessoes de preview ativas.
