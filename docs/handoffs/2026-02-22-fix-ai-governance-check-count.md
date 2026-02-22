# Handoff — Correcao ai-governance-check Count

## Objetivo
Corrigir erro de execucao no script `ai-governance-check.ps1` quando a lista de arquivos alterados era tratada como objeto escalar sem propriedade `Count`.

## Escopo classificado
Estrutural.

## Arquivos alterados
- `scripts/ai-governance-check.ps1`
- `docs/handoffs/2026-02-22-fix-ai-governance-check-count.md`

## Decisoes tomadas
1. Normalizar explicitamente o retorno de `Resolve-ChangedFiles` para array com `@(...)` no ponto de atribuicao de `changedFiles`.
2. Manter restante da logica de governanca sem alteracoes funcionais.

## Validacoes executadas
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` (script executa sem excecao de `Count`)

## Riscos e rollback
- Risco baixo; alteracao pontual de tipagem de colecao.
- Rollback: reverter a linha de atribuicao de `changedFiles` para estado anterior.

## Proximos passos
1. Incluir este handoff no commit da correcao.
2. Reexecutar workflow de governanca no GitHub para confirmar sucesso completo.
