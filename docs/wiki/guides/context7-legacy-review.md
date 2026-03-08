# Guia - Context7 legacy review

## Objetivo

Padronizar revisao de codigo legado com foco em atualizacao de bibliotecas/APIs usando o MCP `context7`.

## Quando usar

1. Atualizacao de pacote com risco de breaking change.
2. Revisao de API antiga para migracao de assinatura/comportamento.
3. Triagem de deprecations e mudancas de recomendacao da documentacao oficial.

## Pre-requisitos

1. `opencode.json` com MCP `context7` habilitado.
2. Variavel de ambiente `CONTEXT7_API_KEY` definida.
3. Sessao OpenCode ou Codex aberta no repositorio.

## Passos

1. Identifique o pacote/biblioteca alvo e versao atual no projeto.
2. Execute `resolve-library-id` para obter o id canonico da biblioteca.
3. Execute `get-library-docs` para:
   - contexto da versao atual em uso;
   - contexto da versao alvo de upgrade.
4. Registre os achados em checklist de legado:
   - APIs obsoletas;
   - breaking changes;
   - requisitos de migracao;
   - impacto em testes e compatibilidade.
5. Classifique prioridade:
   - `alto`: quebra de runtime/seguranca;
   - `medio`: ajuste funcional com risco controlado;
   - `baixo`: melhoria futura sem urgencia.
6. Defina plano de execucao:
   - alteracoes de codigo;
   - testes necessarios;
   - estrategia de rollback.

## Checklist rapido (copiar para handoff/issue)

- Biblioteca alvo:
- Versao atual:
- Versao alvo:
- Library id (Context7):
- APIs obsoletas encontradas:
- Breaking changes confirmadas:
- Mudancas de configuracao necessarias:
- Testes minimos de regressao:
- Nivel de risco (alto|medio|baixo):
- Rollback proposto:

## Criterio de aceite

1. `resolve-library-id` retorna id valido para a biblioteca alvo.
2. `get-library-docs` retorna documentacao para comparacao atual vs alvo.
3. Checklist de legado preenchido com riscos e plano de teste.
4. Resultado incorporado ao fluxo de governanca local (docs/handoff quando aplicavel).

## Troubleshooting

1. Erro de autenticacao:
   - confirmar `CONTEXT7_API_KEY` no terminal atual;
   - reabrir sessao `opencode`;
   - validar chave ativa em `https://context7.com/dashboard`.
2. Biblioteca nao resolvida:
   - testar nome alternativo oficial do pacote;
   - reduzir ambiguidade (namespace/ecossistema).
3. Retorno incompleto:
   - repetir consulta com foco em modulo especifico da biblioteca;
   - dividir por area (migration guide, changelog, API reference).

## Referencias de codigo

- [opencode.json](../../../opencode.json#L1) - assinatura: `"$schema": "https://opencode.ai/config.json"`
- [Setup OpenCode + ECC + Context7](../ai/opencode-ecc-setup.md#L1) - assinatura: `# Setup OpenCode + ECC + Context7 (local)`
- [Agent entrypoint](../ai/agent-entrypoint.md#L1) - assinatura: `# Entrypoint do agente`
