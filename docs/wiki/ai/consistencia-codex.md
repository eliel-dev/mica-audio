# Consistencia no desenvolvimento com Codex

## Objetivo

Evitar implementacoes pela metade, reduzir validacoes desnecessarias e forcar consulta da documentacao-guia antes de varrer toda a base.

## Contrato operacional recomendado

Use este bloco como briefing em toda solicitacao de implementacao:

```text
Modo de execucao:
1) Classifique a mudanca (documental, funcional, estrutural, firmware/protocolo) antes de codar.
2) Consulte primeiro o manual do projeto para localizar ownership de cada parte (ex.: code index, wiki de AI e ADRs).
3) Se a documentacao nao indicar claramente o ponto de mudanca, ai sim faca varredura ampla com justificativa curta.
4) Trabalhe apenas no escopo minimo necessario.
5) Entregue a implementacao completa: codigo, ajuste de docs afetadas e criterio de aceite atendido.
6) Rode somente as validacoes obrigatorias para a classificacao.
7) Antes de finalizar, reporte: o que faltava, o que foi feito, o que foi validado, e riscos.
8) Todo codigo novo deve seguir as melhores praticas atuais da stack alvo, considerando a data atual do ambiente antes de decidir padrao ou recomendacao temporalmente instavel.
9) Para .NET/C#, use como base primaria a documentacao oficial Microsoft/CommunityToolkit compativel com o SDK/TFM do repo, hoje `.NET 10` / `C# 14`.

Formato de entrega:
- Resumo objetivo da mudanca
- Arquivos alterados
- Validacoes executadas (comando + resultado)
- Pendencias (se houver)
```


## Ordem de descoberta (obrigatoria)

Antes de procurar em toda a base, siga esta ordem:

1. `docs/wiki/ai/agent-entrypoint.md`
2. `docs/wiki/reference/code-index.md` (mapa do que cada modulo faz)
3. Paginas de contrato/escopo relacionadas (wiki, ADRs e manifesto IA)
4. Somente se ainda houver ambiguidade: busca ampla no repositorio

> Regra pratica: **documentacao primeiro, varredura ampla depois**.

## Definicao de pronto (DoD) para IA

- [ ] Escopo classificado e declarado no inicio.
- [ ] Mapeamento feito primeiro no manual do projeto (code index/wiki) antes de busca ampla.
- [ ] Mudanca implementada de ponta a ponta no escopo pedido.
- [ ] Sem TODO aberto no caminho principal da feature/correcao.
- [ ] Validacao minima obrigatoria executada.
- [ ] Codigo novo alinhado a melhores praticas atuais da stack e da data corrente.
- [ ] Resumo final com riscos e proximos passos.

## Quando permitir varredura ampla

So faca analise ampla da base quando existir pelo menos uma destas condicoes:

1. A documentacao oficial (manual + code index) nao aponta ownership suficiente para decidir.
2. Mudanca classificada como **estrutural**.
3. Bug sem origem clara apos triagem local.
4. Pedido explicito para auditoria/refatoracao ampla.

## Politica de validacao enxuta

1. **Documental**: apenas `docs-validate`.
2. **Funcional**: `docs-validate` + `dotnet build`.
3. **Estrutural/Firmware**: `docs-validate` + `ai-governance-check` + `dotnet build` (+ handoff).

## Prompt curto (copiar e usar)

```text
Implemente em modo consistente: classifique o escopo, consulte primeiro o manual do projeto (agent-entrypoint + code-index) para achar os pontos responsaveis, e so faca varredura ampla se a documentacao nao for suficiente. Depois, altere somente o necessario, conclua a implementacao inteira e rode apenas as validacoes obrigatorias para essa classe. No final, mostre resumo, arquivos alterados, comandos executados e pendencias.
```

## Referencias

- [Classificacao de mudancas](change-classification.md)
- [Matriz de validacao](validation-matrix.md)
- [Ciclo de vida da tarefa](task-lifecycle.md)
- [Entrypoint do agente](agent-entrypoint.md)
- [Code index](../reference/code-index.md)
