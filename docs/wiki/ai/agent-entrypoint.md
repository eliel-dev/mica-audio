# Entrypoint do agente

## Objetivo

Definir o primeiro passo obrigatorio para qualquer IA antes de alterar codigo/documentacao.

## Passos obrigatorios

1. Ler `AGENTS.md`.
2. Ler `docs/wiki/reference/ai-contract.v1.yaml`.
3. Se a tarefa envolver integracao do `ESP32-S3`, consultar obrigatoriamente as fontes oficiais da Espressif para `ESP-IDF v5.5.4` antes de decidir implementacao:
   - `https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/index.html`
   - `https://github.com/espressif/esp-idf/blob/v5.5.4/docs/en/index.rst`
4. Classificar a mudanca em `documental`, `funcional`, `estrutural` ou `firmware/protocolo`.
5. Identificar arquivos-alvo no [Code index](../reference/code-index.md).
6. Aplicar validacoes da [Matriz de validacao](validation-matrix.md).

## Regras de consistencia

1. Evitar refatoracao ampla quando mudanca local resolve.
2. Manter links wiki -> codigo com `#L`.
3. Adicionar/atualizar `DOCS:` em arquivos-chave alterados.
4. Em mudanca estrutural, criar handoff usando template oficial.

## Referencias de codigo

- [AGENTS](../../../AGENTS.md#L1) - assinatura: `AGENTS - Mica Audio (solo + IA)`
- [ai-governance-check](../../../scripts/ai-governance-check.ps1#L1) - assinatura: `param()`
