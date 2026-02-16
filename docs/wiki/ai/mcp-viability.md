# Viabilidade MCP (design-only)

## Objetivo

Registrar decisao de viabilidade MCP sem implementar servidor MCP nesta fase.

## Matriz de opcoes

| Opcao | Vantagens | Riscos | Decisao atual |
|---|---|---|---|
| Sem MCP custom | Menor custo de manutencao | Menor automacao contextual | **Atual** |
| MCP local read-only (POC futura) | Busca contextual padronizada para IA | Custo de manutencao adicional | Go condicional |
| MCP completo | Maior automacao | Maior complexidade e risco de drift | Nao nesta fase |

## Criterios de decisao

1. Custo de manutencao solo.
2. Risco de drift entre codigo e contexto.
3. Ganho real de produtividade vs guardrails atuais.

## Ferramentas validadas

1. MCP Inspector: https://github.com/modelcontextprotocol/inspector
2. C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
3. VS Code MCP: https://code.visualstudio.com/api/extension-guides/mcp

## Go/No-go

1. **Go** para POC read-only apenas se dor recorrente persistir apos estabilizacao dos guardrails.
2. **No-go** para MCP completo nesta fase.

## Referencias de codigo

- [ADR 0003](../../adr/0003-mcp-viabilidade-e-estrategia-solo-ai.md#L1) - assinatura: `ADR 0003`
- [AGENTS](../../../AGENTS.md#L1) - assinatura: `AGENTS - Mica Audio`
