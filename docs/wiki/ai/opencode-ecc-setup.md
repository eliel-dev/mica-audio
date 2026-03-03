# Setup OpenCode + ECC (local)

## Objetivo

Configurar o repositorio para usar OpenCode com o plugin `ecc-universal`, mantendo o contrato IA local do projeto.

## O que foi versionado

1. `opencode.json` na raiz com plugin e instrucoes canonicas.
2. Este guia para uso diario e troubleshooting rapido.

## Como usar

1. Instale o OpenCode CLI (se necessario):

```bash
npm install -g opencode-ai
```

2. No repo, execute:

```bash
opencode
```

3. Se for o primeiro uso, configure provider com `/connect`.
4. O OpenCode vai carregar `opencode.json` do projeto e usar o plugin `ecc-universal`.
5. Use os comandos do plugin no fluxo diario (`/plan`, `/tdd`, `/code-review`, `/security`, `/verify`).

## Fluxo recomendado neste repositorio

1. Classifique a mudanca em `documental`, `funcional`, `estrutural` ou `firmware/protocolo`.
2. Consulte primeiro `docs/wiki/reference/code-index.md`.
3. Rode as validacoes da classe de mudanca antes de finalizar.
4. Em mudanca estrutural, gere handoff em `docs/handoffs/YYYY-MM-DD-<slug>.md`.

## Troubleshooting rapido

1. Plugin nao carregou:
   - confirme internet e rode `opencode` novamente;
   - confirme se `opencode.json` esta na raiz do repositorio.
2. Comando ECC nao aparece:
   - reabra a sessao `opencode`;
   - confira se `plugin` inclui `ecc-universal`.
3. Contexto muito carregado:
   - reduza a lista de `instructions` no `opencode.json` para manter apenas o essencial.

## Referencias de codigo

- [opencode.json](../../../opencode.json#L1) - assinatura: `"$schema": "https://opencode.ai/config.json"`
- [AGENTS](../../../AGENTS.md#L1) - assinatura: `AGENTS - Mica Audio (solo + IA)`
- [Entrypoint do agente](agent-entrypoint.md#L1) - assinatura: `# Entrypoint do agente`
- [Matriz de validacao](validation-matrix.md#L1) - assinatura: `# Matriz de validacao`
