# Handoff - Preview HUB75 para apps de clima e relogio

## Objetivo

Alinhar as miniaturas da aba Apps com preview realista de HUB75 para os apps Clima e Relogio.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: miniaturas de `accuweather` e `analogclock` exibem preview estilo matriz LED (64x32), sem cair no espectro generico, com modificadores disponiveis.

## Arquivos alterados

- src/App.WinUI/Services/Apps/AppCatalogService.cs
- src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs
- src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs
- src/App.WinUI/Views/Controls/Renderers/ClockPreviewRenderer.cs
- src/App.WinUI/Views/Controls/Renderers/WeatherPreviewRenderer.cs
- docs/handoffs/2026-02-21-preview-hub75-clima-relogio.md

## Decisoes tomadas

1. Adicionado fallback por `appId` no `AppCatalogService` para garantir `preview/modifiers` quando o `catalog.json` antigo nao possui esses campos.
2. Expandido o registry para categorias PT-BR (`clima`, `relogio`) e fallback por id (`accuweather`, `analogclock`) para evitar renderer generico.
3. Substituidos renderers de clock/weather por versoes pixeladas estilo HUB75 com grade 64x32 e texto 5x7.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

## Riscos e rollback

- Risco principal: custo de desenho maior nos previews animados.
- Como reverter: restaurar `ClockPreviewRenderer` e `WeatherPreviewRenderer` anteriores e remover `Hub75PreviewHelper`.

## Proximos passos

1. Ajustar pixel-art dos icones com feedback visual real do seu painel.
2. Opcional: adicionar opcao para simular brilho/gamma do HUB75 no preview.
