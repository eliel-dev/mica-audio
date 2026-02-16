# Playbooks de incidente

## Objetivo

Fornecer resposta rapida para falhas recorrentes no fluxo com IA.

## Incidente: gate estrutural falhou

1. Verifique se houve alteracao em `src/`, `firmware/`, `scripts/`, `global.json`, `MicaAudio.sln`.
2. Adicione evidencia de docs (`docs/wiki`, `docs/adr` ou `README.md`).
3. Se mudanca estrutural, gere handoff em `docs/handoffs/`.

## Incidente: docs-validate falhou

1. Corrigir links quebrados wiki -> codigo.
2. Garantir `DOCS:` em arquivos-chave exigidos.
3. Confirmar ADR listado em `docs/adr/README.md`.

## Incidente: ai-governance-check falhou

1. Validar `ai-contract.v1.yaml` e schema.
2. Revisar handoff (secoes obrigatorias).
3. Revisar arquivo-chave alterado sem backlinks suficientes.

## Referencias de codigo

- [docs-structural-gate](../../../scripts/docs-structural-gate.ps1#L1) - assinatura: `param(...)`
- [docs-validate](../../../scripts/docs-validate.ps1#L1) - assinatura: `param()`
- [ai-governance-check](../../../scripts/ai-governance-check.ps1#L1) - assinatura: `param(...)`
