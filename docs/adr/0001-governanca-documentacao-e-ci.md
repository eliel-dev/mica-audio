# ADR 0001 - Governanca de documentacao e CI

## Contexto

A base cresceu rapidamente (wiki tecnica, servidor local, firmware, UI multipagina) e depende de continuidade por humano e IA. Era necessario tornar a qualidade documental verificavel automaticamente.

## Decisao

Adotar governanca continua com:

1. Wiki versionada e validada por script local (`docs-validate`).
2. Workflow de CI para gate estrutural, validacao documental e build Debug.
3. Checklist de documentacao no template de PR.
4. Uso de backlinks `DOCS:` em arquivos-chave.

## Consequencias

- PRs ganham trilha objetiva de validacao.
- Mudancas estruturais sem docs deixam de passar despercebidas.
- Ha custo adicional de manutencao de docs e templates.

## Status

Aceita

## Data

2026-02-16

## Referencias

- `docs/wiki/reference/docs-health.md`
- `scripts/docs-validate.ps1`
- `.github/workflows/governance.yml`