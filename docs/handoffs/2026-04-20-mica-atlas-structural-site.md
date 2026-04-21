# Handoff - Mica Atlas structural site

## Objetivo

Criar um portal estatico dedicado para organizar a wiki tecnica do Mica Audio por capacidade, status e leitura curada, mantendo `docs/wiki` como fonte canonica.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - `site/docs-atlas` existe como app isolado em `Astro + Starlight`
  - a home e `status-first`
  - existem paginas por capacidade
  - existe area separada para `future`
  - `docs/wiki/**/*.md` continuam canonicos e sao ingeridos por sync
  - o build do site funciona localmente

## Arquivos alterados

- `site/docs-atlas/package.json`
- `site/docs-atlas/astro.config.mjs`
- `site/docs-atlas/.gitignore`
- `site/docs-atlas/README.md`
- `site/docs-atlas/scripts/sync-wiki-docs.mjs`
- `site/docs-atlas/src/components/AtlasHead.astro`
- `site/docs-atlas/src/components/AtlasDocMeta.astro`
- `site/docs-atlas/src/components/AtlasStatusChip.astro`
- `site/docs-atlas/src/components/Empty.astro`
- `site/docs-atlas/src/data/atlas-schema.mjs`
- `site/docs-atlas/src/data/atlas-catalog-source.mjs`
- `site/docs-atlas/src/data/atlas-data.mjs`
- `site/docs-atlas/src/pages/index.astro`
- `site/docs-atlas/src/pages/future.astro`
- `site/docs-atlas/src/pages/capabilities/[slug].astro`
- `site/docs-atlas/src/styles/atlas.css`
- `docs/wiki/README.md`

## Decisoes tomadas

1. O Atlas foi criado como app separado em `site/docs-atlas`, sem acoplamento ao `Device.Server` nem ao WinUI, para manter a wiki e o produto desacoplados.
2. A fonte canonica continua em `docs/wiki`; o site gera um espelho renderizavel em `src/content/docs/docs/` via `sync:wiki`, sem editar os markdowns originais.
3. O catalogo manual ficou separado da wiki em `src/data/atlas-catalog-source.mjs`, com validacao por `zod`, para que `status`, `capability` e `priority` nao sejam inferidos automaticamente do markdown.
4. As paginas de detalhe da wiki foram mantidas em `/docs/...` usando o proprio Starlight, enquanto `home`, `capabilities` e `future` usam rotas Astro customizadas com `StarlightPage`.
5. Links internos da wiki passam por rewrite para `/docs/...`; links relativos do repositorio sao convertidos para `blob` do GitHub em `origin` quando possivel, evitando quebrar referencias de codigo no site.
6. Diagramas Mermaid foram preservados com render client-side lazy via `mermaid`, evitando um pipeline adicional de transformacao da wiki.
7. A direcao visual foi fechada como dark, tecnica e densa, sem grades suaves, sem degradês e sem copy promocional; `Silkscreen` ficou restrita a labels e chips.

## Validacoes executadas

```text
npm run sync:wiki (site/docs-atlas) -> OK
npm run build (site/docs-atlas) -> OK
```

## Riscos e rollback

- Risco principal: o rewrite de markdown ainda e baseado em regras simples de link e sanitizacao MDX; futuros documentos com markdown mais exotico podem exigir ajuste no sync.
- Como reverter: remover `site/docs-atlas`, retirar o link do Atlas em `docs/wiki/README.md` e manter apenas a wiki textual atual.

## Proximos passos

1. Rodar preview local e revisar visualmente a hierarquia, o search e a legibilidade mobile.
2. Decidir se o Atlas entra em CI proprio com build separado do restante da solucao.
3. Refinar a curadoria do catalogo conforme novas capacidades e checklists entrarem na wiki.
