# Handoff - Reducao do catalogo para 2 apps (clima e relogio)

## Objetivo

Reduzir o catalogo da aba Apps para apenas 2 apps ativos (`clima` e `relogio`) mantendo miniaturas/modificadores dinamicos.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: UI de Apps exibe somente 2 itens e usuarios com `catalog.json` antigo tambem passam a ver apenas os 2 apps suportados.

## Arquivos alterados

- src/App.WinUI/AppData/apps-catalog.seed.json
- src/App.WinUI/Services/Apps/AppCatalogService.cs
- docs/handoffs/2026-02-21-reducao-catalogo-2-apps.md

## Decisoes tomadas

1. Filtragem em runtime por `EnabledAppIds` no `AppCatalogService` para garantir compatibilidade com `catalog.json` legado sem exigir migracao manual de arquivo.
2. Seed oficial simplificado para 2 apps (`accuweather` como Clima e `analogclock` como Relogio) com schema v2 e modificadores completos.
3. Mantido sem fallback v1 por decisao previa do projeto; foco em schema v2 e catalogo minimo.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
```

## Riscos e rollback

- Risco principal: usuarios que esperavam apps antigos nao os verao mais na UI.
- Como reverter: restaurar `apps-catalog.seed.json` anterior e remover filtro `EnabledAppIds` em `AppCatalogService`.

## Proximos passos

1. Se quiser trocar os 2 apps suportados, alterar apenas `EnabledAppIds` e o seed.
2. Opcional: adicionar aviso na UI explicando que o catalogo atual esta em modo reduzido.
