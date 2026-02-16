# ADR 0002 - Politica PR docs estrutural

## Contexto

Mudancas em `src/`, `firmware/`, `matrixportal-s3/` e `scripts/` alteram comportamento e operacao do projeto. Sem regra automatica, existe risco alto de drift entre codigo e documentacao.

## Decisao

Aplicar gate automatico para PR/push:

- Se houver mudanca estrutural, exige evidenca de docs (`docs/wiki/`, `docs/adr/` ou `README.md`).
- Em PR, pode haver bypass controlado com label `docs-exempt`.
- Em push na `main`, nao ha bypass por label.

## Consequencias

- Melhor rastreabilidade e menor risco de conhecimento tacito.
- Pequeno atrito adicional em PRs estruturais.
- `docs-exempt` precisa de uso disciplinado para nao virar atalho indevido.

## Status

Aceita

## Data

2026-02-16

## Referencias

- `scripts/docs-structural-gate.ps1`
- `.github/workflows/governance.yml`
- `.github/PULL_REQUEST_TEMPLATE.md`