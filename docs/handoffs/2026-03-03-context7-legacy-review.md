# Handoff - Context7 legacy review + API key no OpenCode/Codex

## Objetivo

Habilitar o uso operacional do Context7 para auditoria de codigo legado, com autenticacao por chave via variavel de ambiente, cobrindo OpenCode e VSCode/Codex sem expor segredo no repositorio.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: Context7 autenticado por `CONTEXT7_API_KEY` em OpenCode/Codex, guia de revisao de legado publicado e wiki indexada sem links quebrados.

## Arquivos alterados

- `opencode.json`
- `docs/wiki/ai/opencode-ecc-setup.md`
- `docs/wiki/guides/context7-legacy-review.md`
- `docs/wiki/README.md`
- `docs/handoffs/2026-03-03-context7-legacy-review.md`

## Decisoes tomadas

1. O MCP remoto `context7` no `opencode.json` usa apenas placeholder de ambiente (`{env:CONTEXT7_API_KEY}`) no header, sem chave hardcoded.
2. Para VSCode/Codex, a orientacao foi padronizada com `env_http_headers` em `~/.codex/config.toml`, mantendo segredo no ambiente do usuario.
3. O fluxo de legado foi formalizado em guia dedicado com passos de `resolve-library-id` e `get-library-docs`, checklist de risco e criterio de aceite.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (1 warning WIN2D0001 preexistente no projeto Integration.Smoke)
```

## Riscos e rollback

- Risco principal: variacao de configuracao local do Codex (`~/.codex/config.toml`) pode causar falha de autenticacao se `CONTEXT7_API_KEY` nao estiver carregada na sessao.
- Como reverter: remover bloco `mcp.context7` de `opencode.json`, remover links/guia da wiki e excluir este handoff.

## Proximos passos

1. Validar em sessao real OpenCode e VSCode/Codex com 1 chamada `resolve-library-id` e 1 chamada `get-library-docs`.
2. Priorizar bibliotecas legadas mais sensiveis e abrir issues usando o checklist do guia novo.
