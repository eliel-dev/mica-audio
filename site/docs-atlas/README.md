# Mica Atlas

Portal estatico para organizar a wiki tecnica do Mica Audio por capacidade, status e leitura curada, sem mover a fonte canonica em `docs/wiki`.

## Como rodar localmente

```powershell
cd site/docs-atlas
npm install
npm run dev
```

O `dev` e o `build` executam `npm run sync:wiki` antes de subir o site ou gerar o bundle.

## Como o sync funciona

1. Le `docs/wiki/**/*.md` no repositorio raiz.
2. Gera um espelho renderizavel em `src/content/docs/docs/`.
3. Copia o markdown bruto para `public/canonical/wiki/`.
4. Reescreve links internos da wiki para `/docs/...`.
5. Converte links relativos do repositorio para URLs `blob` do GitHub de `origin` quando possivel.

Nada em `docs/wiki` e alterado por esse processo.

## Como atualizar o catalogo manual

O Atlas usa um catalogo curado para definir:

- status principal
- capacidade
- prioridade
- resumo
- lacunas e proximos passos

Arquivos envolvidos:

- `src/data/atlas-catalog-source.mjs`
- `src/data/atlas-schema.mjs`
- `src/data/atlas-data.mjs`

Ao adicionar ou alterar um item:

1. Atualize `atlas-catalog-source.mjs`.
2. Rode `npm run sync:wiki`.
3. Rode `npm run build`.

## O que o Atlas organiza

- `Home`: panorama curto e status-first.
- `Status`: uma pagina por grupo para ver tudo que esta implementado, parcial, futuro ou checklist.
- `Capabilities`: uma pagina por area curada.
- `Future`: target-state e checklists prospectivos.
- `Docs`: leitura renderizada a partir da wiki canonica.
