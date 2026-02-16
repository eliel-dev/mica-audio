# Guia - Checklist de release de documentacao

## Objetivo

Padronizar verificacoes antes de release para garantir que documentacao esta coerente com o estado real do codigo.

## Passos

1. Rodar `docs-validate.ps1` e corrigir qualquer falha.
2. Rodar build Debug da solucao para garantir zero impacto funcional.
3. Revisar links novos e `DOCS:` em arquivos alterados na release.
4. Atualizar roadmap e troubleshooting matrix se houve mudanca operacional.
5. Registrar no changelog tecnico os pontos documentados.

## Referencias de codigo

- [docs-validate.ps1](../../../scripts/docs-validate.ps1#L1) - assinatura: script de validacao documental
- [dev-run.ps1 ValidateDocs](../../../scripts/dev-run.ps1#L12) - assinatura: `-ValidateDocs`
- [code-index](../reference/code-index.md) - assinatura: indice de classes/metodos
- [docs health](../reference/docs-health.md) - assinatura: indicadores de cobertura

## Checklist rapido

- [ ] Validacao documental passou.
- [ ] Build da solucao passou.
- [ ] Links wiki->codigo revisados.
- [ ] Backlinks `DOCS:` revisados.