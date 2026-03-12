# Handoff - Politica de fontes oficiais ESP32-S3

## Objetivo

Registrar no contrato do repositorio que qualquer integracao do `ESP32-S3` deve consultar obrigatoriamente a documentacao oficial da Espressif para `ESP-IDF v5.5.3` antes de decidir implementacao, configuracao ou recomendacao tecnica.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - `AGENTS.md` passa a exigir consulta obrigatoria das fontes oficiais da Espressif;
  - `agent-entrypoint.md` reforca a consulta no passo inicial do agente;
  - `ai-contract.v1.yaml` passa a registrar as fontes primarias de `ESP32-S3`;
  - a documentacao do modulo de firmware referencia explicitamente essas fontes.

## Arquivos alterados

- `AGENTS.md`
- `docs/wiki/ai/agent-entrypoint.md`
- `docs/wiki/reference/ai-contract.v1.yaml`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

1. A regra foi registrada no `AGENTS.md` porque ele e o contrato canonico para agentes no repositorio.
2. O `agent-entrypoint.md` passou a cobrar a consulta logo no inicio da tarefa para reduzir o risco de a regra ser ignorada em integracoes de firmware.
3. O `ai-contract.v1.yaml` recebeu as URLs como `primary_sources` para manter a politica consumivel por automacoes e checks futuros.
4. A documentacao do modulo de firmware tambem foi atualizada para deixar a referencia visivel no contexto tecnico do `ESP32-S3`.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
```

## Riscos e rollback

- Risco principal: a regra aumenta o rigor do fluxo e pode exigir navegacao/documentacao antes de implementacoes pequenas em firmware.
- Como reverter:
  - remover a regra do `AGENTS.md`;
  - remover a etapa do `agent-entrypoint.md`;
  - excluir `esp32s3_primary_sources` do `ai-contract.v1.yaml`;
  - remover a secao de referencias do modulo de firmware.

## Proximos passos

1. Se surgir politica equivalente para outras plataformas embarcadas, padronizar o mesmo modelo em `ai-contract.v1.yaml`.
2. Se houver checks automatizados para fonte primaria por stack, usar `esp32s3_primary_sources` como base.
