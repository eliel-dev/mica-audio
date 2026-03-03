# Handoff - OpenCode + ECC local setup

## Objetivo

Versionar a configuracao minima para usar OpenCode com ECC neste repositorio, alinhada ao contrato IA local.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: projeto abre com `opencode.json` carregado, plugin `ecc-universal` ativo e guia de uso registrado na wiki.

## Arquivos alterados

- `opencode.json`
- `docs/wiki/ai/opencode-ecc-setup.md`
- `docs/wiki/ai/README.md`
- `docs/wiki/README.md`
- `docs/handoffs/2026-03-03-opencode-ecc-local-setup.md`

## Decisoes tomadas

1. Integracao feita via `plugin: ["ecc-universal"]` no `opencode.json`, sem adicionar `package.json` Node no repositorio.
2. Instrucoes do OpenCode limitadas a documentos canonicos de governanca para reduzir drift.
3. Setup documentado em pagina wiki dedicada para onboarding rapido.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File ./scripts/docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (build com warnings existentes)
```

## Riscos e rollback

- Risco principal: comandos/skills disponiveis no plugin podem variar conforme a versao instalada de `ecc-universal`.
- Como reverter: remover `opencode.json` (ou retirar `ecc-universal` da chave `plugin`) e desfazer os links de wiki desta entrega.

## Proximos passos

1. Validar em maquina do desenvolvedor: abrir `opencode` e executar `/plan`.
2. Ajustar a lista de `instructions` do `opencode.json` se precisar reduzir custo/contexto.
