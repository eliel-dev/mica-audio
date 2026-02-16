# Handoff - Fase 4 AI-first governance

## Objetivo

Finalizar governanca para fluxo solo com IA com fail-close local e CI.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: contrato IA, scripts, hooks, workflow e wiki atualizados com validacao local passando.

## Arquivos alterados

- AGENTS.md
- docs/wiki/ai/*
- docs/wiki/reference/ai-contract.v1.yaml
- docs/wiki/reference/ai-contract.schema.json
- scripts/ai-governance-check.ps1
- scripts/git-hooks-bootstrap.ps1
- .githooks/pre-commit
- .githooks/pre-push
- .github/workflows/governance.yml
- docs/adr/0003-mcp-viabilidade-e-estrategia-solo-ai.md

## Decisoes tomadas

1. Escopo MCP fica design-only nesta fase.
2. Hooks locais versionados com bootstrap obrigatorio.
3. Handoff exigido para mudanca estrutural.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> esperado sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> esperado sucesso
dotnet build MicaAudio.sln -c Debug -> esperado sucesso
```

## Riscos e rollback

- Risco principal: falso positivo de gate por classificacao incorreta.
- Como reverter: ajustar manifesto IA + script de governanca no mesmo commit.

## Proximos passos

1. Ativar branch protection exigindo novo job governance-ai-guardrails.
2. Revisar necessidade de POC MCP read-only apos 2-3 ciclos de uso.
