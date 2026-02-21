# ADR 0003 - MCP viabilidade e estrategia solo IA

## Contexto

O projeto evoluiu para fluxo solo com apoio de IA e precisa reduzir erros de contexto sem aumentar custo de manutencao.

## Decisao

1. Nao implementar servidor MCP nesta fase.
2. Priorizar contrato IA, guardrails automaticos e documentacao canonica.
3. Definir POC futura de MCP local read-only em C# apenas se dor persistir.

## Consequencias

- Menor risco de complexidade prematura.
- Melhor previsibilidade com gates e handoff padronizado.
- Ganhos de MCP ficam postergados para avaliacao orientada a evidencia.

## Status

Aceita

## Data

2026-02-16

## Referencias

- docs/wiki/ai/mcp-viability.md
- docs/wiki/reference/ai-contract.v1.yaml
- scripts/ai-governance-check.ps1
