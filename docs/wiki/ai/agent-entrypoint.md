# Entrypoint do agente

## Objetivo

Definir o primeiro passo obrigatorio para qualquer IA antes de alterar codigo/documentacao.

## Passos obrigatorios

1. Ler `AGENTS.md`.
2. Ler `docs/wiki/reference/ai-contract.v1.yaml`.
3. Classificar a mudanca em `documental`, `funcional`, `estrutural` ou `firmware/protocolo`.
4. Identificar arquivos-alvo no [Code index](../reference/code-index.md).
5. Aplicar validacoes da [Matriz de validacao](validation-matrix.md).

## Regras de consistencia

1. Evitar refatoracao ampla quando mudanca local resolve.
2. Manter links wiki -> codigo com `#L`.
3. Adicionar/atualizar `DOCS:` em arquivos-chave alterados.
4. Em mudanca estrutural, criar handoff usando template oficial.

## Referencias de codigo

- [AGENTS](../../../AGENTS.md#L1) - assinatura: `AGENTS - Mica Audio (solo + IA)`
- [ai-governance-check](../../../scripts/ai-governance-check.ps1#L1) - assinatura: `param()`
