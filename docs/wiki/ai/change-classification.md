# Classificacao de mudancas

## Objetivo

Remover ambiguidade sobre escopo e nivel de controle necessario.

## Tipos

1. **Documental**
- muda apenas `docs/`, `README.md`, templates e ADRs.

2. **Funcional**
- altera comportamento em `src/` sem mudar contratos centrais.

3. **Estrutural**
- altera arquitetura, contratos publicos, servicos centrais, scripts de governanca ou workflow.

4. **Firmware/protocolo**
- altera `firmware/`, `Device.Protocol`, `Device.Server` e contratos wire.

## Evidencia minima

- Documental: `docs-validate`.
- Funcional: `docs-validate` + `mvvm-validate` (quando tocar `App.WinUI`) + `dotnet build`.
- Estrutural: `docs-validate` + `ai-governance-check` + `mvvm-validate` + `dotnet build` + handoff.
- Firmware/protocolo: igual estrutural + validacao de referencias em protocolo/wiki.

## Referencias de codigo

- [governance workflow](../../../.github/workflows/governance.yml#L1) - assinatura: `name: governance`
- [PR template](../../../.github/PULL_REQUEST_TEMPLATE.md#L1) - assinatura: `Resumo da mudanca`

