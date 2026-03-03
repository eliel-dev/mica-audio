# Setup OpenCode + ECC + Context7 (local)

## Objetivo

Configurar o repositorio para usar OpenCode com o plugin `ecc-universal` e o MCP remoto `context7`, mantendo o contrato IA local do projeto.

## O que foi versionado

1. `opencode.json` na raiz com plugin, instrucoes canonicas e MCP `context7`.
2. Este guia para uso diario e troubleshooting rapido.

## API key do Context7 (dashboard)

1. Gere a chave em `https://context7.com/dashboard`.
2. Defina a chave na sessao atual do PowerShell:

```powershell
$env:CONTEXT7_API_KEY="SUA_CHAVE"
```

3. Defina a chave de forma persistente no usuario Windows:

```powershell
setx CONTEXT7_API_KEY "SUA_CHAVE"
```

4. Feche e reabra o terminal para carregar a variavel persistente.
5. Nao salve a chave em arquivo versionado (`opencode.json` usa apenas placeholder de ambiente).

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
5. O OpenCode tambem vai carregar o MCP `context7` usando `CONTEXT7_API_KEY`.
6. Use os comandos do plugin no fluxo diario (`/plan`, `/tdd`, `/code-review`, `/security`, `/verify`).

## Checklist de validacao da conexao Context7

1. Abra `opencode` no repo com `CONTEXT7_API_KEY` definida.
2. Execute `resolve-library-id` para uma biblioteca alvo.
3. Execute `get-library-docs` usando o library id resolvido.
4. Confirme retorno sem erro de autenticacao.
5. Em caso de erro, valide:
   - variavel `CONTEXT7_API_KEY` carregada no terminal atual;
   - chave ativa no dashboard;
   - conectividade com `https://mcp.context7.com/mcp`.

## Uso no VSCode/Codex

O MCP do Codex CLI e da extensao Codex no VSCode compartilham a configuracao em:

- `C:\Users\<seu-usuario>\.codex\config.toml`
- atalho equivalente: `$HOME\.codex\config.toml`

Exemplo minimo para Context7 (remoto):

```toml
[mcp_servers.context7]
url = "https://mcp.context7.com/mcp"
env_http_headers = { CONTEXT7_API_KEY = "CONTEXT7_API_KEY" }
```

Passos recomendados:

1. Gere a chave em `https://context7.com/dashboard`.
2. Defina `CONTEXT7_API_KEY` no Windows (sessao atual e persistente) conforme secao anterior.
3. Configure `~/.codex/config.toml` com o bloco acima.
4. Reinicie VSCode/Codex para recarregar MCPs.
5. Valide o registro MCP:

```powershell
codex mcp get context7 --json
```

6. Valide em uma sessao do agente (ex.: `resolve-library-id` e `get-library-docs`).

Observacao: com `env_http_headers`, a chave continua somente no ambiente do usuario e nao fica salva no repositorio.

## Fluxo recomendado neste repositorio

1. Classifique a mudanca em `documental`, `funcional`, `estrutural` ou `firmware/protocolo`.
2. Consulte primeiro `docs/wiki/reference/code-index.md`.
3. Rode as validacoes da classe de mudanca antes de finalizar.
4. Em mudanca estrutural, gere handoff em `docs/handoffs/YYYY-MM-DD-<slug>.md`.
5. Para revisao de legado com bibliotecas externas, siga `docs/wiki/guides/context7-legacy-review.md`.

## Troubleshooting rapido

1. Plugin nao carregou:
   - confirme internet e rode `opencode` novamente;
   - confirme se `opencode.json` esta na raiz do repositorio.
2. Comando ECC nao aparece:
   - reabra a sessao `opencode`;
   - confira se `plugin` inclui `ecc-universal`.
3. Context7 sem autenticar:
   - confirme `CONTEXT7_API_KEY` com `echo $env:CONTEXT7_API_KEY`;
   - feche/reabra o terminal apos `setx`;
   - valide se a chave foi criada em `https://context7.com/dashboard`.
4. Contexto muito carregado:
   - reduza a lista de `instructions` no `opencode.json` para manter apenas o essencial.

## Referencias de codigo

- [opencode.json](../../../opencode.json#L1) - assinatura: `"$schema": "https://opencode.ai/config.json"`
- [AGENTS](../../../AGENTS.md#L1) - assinatura: `AGENTS - Mica Audio (solo + IA)`
- [Entrypoint do agente](agent-entrypoint.md#L1) - assinatura: `# Entrypoint do agente`
- [Matriz de validacao](validation-matrix.md#L1) - assinatura: `# Matriz de validacao`
- [Guia Context7 legado](../guides/context7-legacy-review.md#L1) - assinatura: `# Guia - Context7 legacy review`
