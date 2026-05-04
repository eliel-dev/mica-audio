# AGENTS - Mica Audio (solo + IA)

Este arquivo e o contrato canonico para qualquer IA/Agente trabalhando neste repositorio.

## Objetivo

Padronizar execucao de tarefas para reduzir drift e erro em um fluxo solo com IA.

## Regras de execucao

1. Sempre ler `docs/wiki/ai/agent-entrypoint.md` antes de iniciar mudancas.
2. Classificar a mudanca com base em `docs/wiki/ai/change-classification.md`.
3. Executar validacoes obrigatorias definidas em `docs/wiki/reference/ai-contract.v1.yaml`.
4. Em mudanca estrutural, criar handoff em `docs/handoffs/YYYY-MM-DD-<slug>.md` usando template oficial.
5. Atualizar backlinks `DOCS:` em arquivos-chave alterados.

## Regra obrigatoria de qualidade para IA

1. Todo codigo escrito ou alterado por IA deve seguir os padroes de qualidade e melhores praticas atuais da stack alvo.
2. Para temas temporalmente instaveis, a IA deve consultar a data atual do ambiente antes de decidir padrao, recomendacao ou configuracao.
3. Para .NET/C#, a IA deve se basear em documentacao oficial Microsoft/CommunityToolkit compativel com o SDK/TFM do repositorio, hoje `.NET 10` / `C# 14`.
4. A IA nao deve introduzir `NoWarn` amplo, pacotes extras de analyzers ou supressoes locais sem justificativa tecnica objetiva e documentada.
5. Para qualquer integracao do `ESP32-S3` neste repositorio, a IA deve consultar obrigatoriamente a documentacao oficial da Espressif compativel com `ESP-IDF v5.5.4` antes de decidir implementacao, configuracao ou recomendacao tecnica.
6. Fontes primarias obrigatorias para `ESP32-S3`:
   - `https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/index.html`
   - `https://github.com/espressif/esp-idf/blob/v5.5.4/docs/en/index.rst`
   - `https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/api-guides/`
   - `https://docs.espressif.com/projects/arduino-esp32/en/latest/getting_started.html`

## Acoes proibidas sem aprovacao explicita

- `git reset --hard`
- `git checkout -- <arquivo>`
- `git clean -fd`
- comandos que removem historico local sem backup

## Fonte unica de politica

- Manifesto: `docs/wiki/reference/ai-contract.v1.yaml`
- Schema: `docs/wiki/reference/ai-contract.schema.json`
- Playbooks: `docs/wiki/ai/incident-playbooks.md`

## Fluxo rapido

1. Ler objetivo e escopo da tarefa.
2. Localizar pontos de alteracao em `docs/wiki/reference/code-index.md`.
3. Executar mudancas minimas.
4. Rodar validacao:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
```

5. Gerar handoff quando aplicavel.
