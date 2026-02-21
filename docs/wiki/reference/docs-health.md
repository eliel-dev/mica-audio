# Docs Health

## Objetivo

Medir saude da documentacao e garantir que links/backlinks continuam rastreaveis no fluxo local e em CI.

## Indicadores

- Total de arquivos wiki.
- Total de links wiki -> codigo validados.
- Total de backlinks `DOCS:` encontrados.
- Cobertura minima de arquivos-chave com `DOCS:` >= 2.
- Conformidade do contrato IA (`ai-contract.v1.yaml`).
- Conformidade de handoff para mudanca estrutural.

## Comandos locais

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\git-hooks-bootstrap.ps1
dotnet build MicaAudio.sln -c Debug
```

## Gate CI

Checks esperados no GitHub Actions:

1. `governance-structural-docs`
2. `governance-ai-guardrails`
3. `governance-build-debug`

Arquivo de workflow:

- [governance.yml](../../../.github/workflows/governance.yml)
- [release.yml](../../../.github/workflows/release.yml)

## Politica de governanca

1. Falha no validador bloqueia merge.
2. Mudanca estrutural sem update de docs e bloqueada.
3. Mudanca estrutural sem handoff e bloqueada.
4. Excecao em PR pode usar label `docs-exempt` apenas para regra de evidencia de docs.

## Referencias de codigo

- [docs-validate.ps1](../../../scripts/docs-validate.ps1#L1)
- [docs-structural-gate.ps1](../../../scripts/docs-structural-gate.ps1#L1)
- [ai-governance-check.ps1](../../../scripts/ai-governance-check.ps1#L1)
- [git-hooks-bootstrap.ps1](../../../scripts/git-hooks-bootstrap.ps1#L1)
- [AI contract](ai-contract.v1.yaml)
- [Wiki index](../README.md)

