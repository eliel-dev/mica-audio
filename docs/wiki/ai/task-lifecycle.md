# Ciclo de vida da tarefa

## Objetivo

Padronizar o fluxo fim-a-fim para execucao previsivel por IA.

## Etapas

1. **Entender**: objetivo, escopo e criterios de aceite.
2. **Mapear**: localizar modulos/contratos no code index.
3. **Executar**: aplicar mudanca minima com links/backlinks atualizados.
4. **Validar**: rodar checks obrigatorios por escopo.
5. **Handoff**: registrar contexto e riscos para proxima IA.

## Checklist de saida

- [ ] Mudanca classificada.
- [ ] Validacoes obrigatorias executadas.
- [ ] `DOCS:` atualizado quando necessario.
- [ ] Handoff criado (mudanca estrutural).

## Referencias de codigo

- [docs-validate](../../../scripts/docs-validate.ps1#L1) - assinatura: `param()`
- [docs-structural-gate](../../../scripts/docs-structural-gate.ps1#L1) - assinatura: `param(...)`
