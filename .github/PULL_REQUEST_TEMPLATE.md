## Resumo da mudanca

Descreva o que foi alterado e o motivo.

## Tipo de mudanca

- [ ] Correcao de bug
- [ ] Feature
- [ ] Refatoracao
- [ ] Documentacao
- [ ] Infra/CI

## Escopo classificado

- [ ] documental
- [ ] funcional
- [ ] estrutural
- [ ] firmware/protocolo

## Impacto estrutural

- [ ] Sim (altera src/, firmware/, scripts/, MicaAudio.sln, global.json, .github/workflows)
- [ ] Nao

## Handoff IA

- [ ] N/A (mudanca nao estrutural)
- [ ] Criei handoff em `docs/handoffs/YYYY-MM-DD-<slug>.md`
- Link do handoff:

## Checklist obrigatorio de documentacao

- [ ] Atualizei `docs/wiki` e/ou `docs/adr` para mudancas estruturais
- [ ] Rodei `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- [ ] Rodei `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- [ ] Rodei `dotnet build MicaAudio.sln -c Debug`
- [ ] Atualizei `DOCS:` em arquivos-chave quando necessario

## Evidencias (comandos e output resumido)

```text
Cole aqui os comandos executados e resumo dos resultados.
```

## Risco / rollback

- Risco principal:
- Como reverter rapidamente:

## Observacoes

Use label `docs-exempt` apenas para excecoes justificadas em PR.
