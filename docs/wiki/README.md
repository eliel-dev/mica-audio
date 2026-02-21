# Wiki Tecnica do Mica Audio

Documentacao tecnica versionada junto com o codigo para acelerar manutencao, onboarding e continuidade por humanos e IA.

## Como usar esta wiki

1. Comece por `architecture/01-system-overview.md` para entender o mapa geral.
2. Leia o modulo alvo em `modules/`.
3. Aplique um guia em `guides/`.
4. Consulte `reference/code-index.md` para achar classes e metodos.
5. Para fluxo solo com IA, leia `ai/agent-entrypoint.md`.
6. Rode a validacao local:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
```

## Navegacao rapida por tarefa

- Quero mexer em captura/analise/render: [architecture/01-system-overview.md](architecture/01-system-overview.md)
- Quero operar dispositivos e servidor: [modules/server-build-and-artifacts.md](modules/server-build-and-artifacts.md)
- Quero baixar firmware pre-compilado: [guides/build-export-firmware.md](guides/build-export-firmware.md)
- Quero debugar falha de download/salvamento de firmware: [guides/debug-ota-http-failure.md](guides/debug-ota-http-failure.md)
- Quero adicionar app no catalogo: [guides/add-app-catalog-item.md](guides/add-app-catalog-item.md)
- Quero configurar modificadores dinamicos de app: [guides/configure-app-modifiers.md](guides/configure-app-modifiers.md)
- Quero resolver busca de cidade no clima: [guides/troubleshoot-city-autocomplete.md](guides/troubleshoot-city-autocomplete.md)
- Quero ver status da documentacao: [reference/docs-health.md](reference/docs-health.md)
- Quero aplicar hardening security-first: [guides/security-quality-hardening.md](guides/security-quality-hardening.md)
- Quero operar release 1.0 com setup assinado: [guides/release-1.0-installer.md](guides/release-1.0-installer.md)
- Quero operar no modo solo + IA: [ai/README.md](ai/README.md)

## Indice

### Arquitetura
- [01 - System overview](architecture/01-system-overview.md)
- [02 - Runtime lifecycle](architecture/02-runtime-lifecycle.md)
- [03 - Data contracts](architecture/03-data-contracts.md)
- [04 - Threading and concurrency](architecture/04-threading-concurrency.md)
- [05 - Device session and reconnect](architecture/05-device-session-and-reconnect.md)
- [06 - Errors, timeouts and recovery](architecture/06-errors-timeouts-and-recovery.md)

### Modulos
- [App.WinUI](modules/app-winui.md)
- [Audio.Loopback](modules/audio-loopback.md)
- [Analyzer.Dsp](modules/analyzer-dsp.md)
- [Visual.Win2D](modules/visual-win2d.md)
- [Output (LED)](modules/output-led.md)
- [Device.Server + Device.Protocol](modules/device-server-protocol.md)
- [Firmware Matrix Portal S3](modules/firmware-matrixportal-s3.md)
- [Settings + Presets + Persistencia](modules/settings-presets-persistence.md)
- [DeviceOperationsCoordinator](modules/device-operations-coordinator.md)
- [Apps catalog + deployment](modules/apps-catalog-deployment.md)
- [Server build + artifacts](modules/server-build-and-artifacts.md)

### Guias
- [Mudar configuracao do visualizador](guides/change-visualizer-settings.md)
- [Adicionar novo renderer](guides/add-new-renderer.md)
- [Adicionar novo comando de dispositivo](guides/add-device-command.md)
- [Download de firmware pre-compilado](guides/build-export-firmware.md)
- [Debug: visualizacao nao aparece](guides/debug-no-visualization.md)
- [Adicionar item no catalogo de apps](guides/add-app-catalog-item.md)
- [Configurar modificadores de apps](guides/configure-app-modifiers.md)
- [Troubleshoot autocomplete de cidade](guides/troubleshoot-city-autocomplete.md)
- [Operar ciclo de vida de dispositivo](guides/operate-device-lifecycle.md)
- [Debug de download/salvamento de firmware](guides/debug-ota-http-failure.md)
- [Checklist de release de documentacao](guides/release-doc-checklist.md)
- [Release 1.0 com setup assinado](guides/release-1.0-installer.md)
- [Hardening de seguranca e qualidade](guides/security-quality-hardening.md)

### IA / Agentes
- [IA index](ai/README.md)
- [Entrypoint do agente](ai/agent-entrypoint.md)
- [Ciclo de vida da tarefa](ai/task-lifecycle.md)
- [Classificacao de mudancas](ai/change-classification.md)
- [Matriz de validacao](ai/validation-matrix.md)
- [Playbooks de incidente](ai/incident-playbooks.md)
- [Viabilidade MCP](ai/mcp-viability.md)

### Referencia
- [Code index](reference/code-index.md)
- [Convencoes de links wiki<->codigo](reference/linking-conventions.md)
- [HTTP API v1](reference/http-api-v1.md)
- [WS protocol v1](reference/ws-protocol-v1.md)
- [Troubleshooting matrix](reference/troubleshooting-matrix.md)
- [Docs health](reference/docs-health.md)
- [Glossario](reference/glossary.md)
- [AI contract (YAML)](reference/ai-contract.v1.yaml)
- [AI contract schema](reference/ai-contract.schema.json)

### Templates
- [Template de modulo](_templates/module-page-template.md)
- [Template de guia](_templates/guide-template.md)
- [Template de handoff IA](_templates/ai-change-handoff-template.md)

## Governanca continua

- ADRs: [docs/adr/README.md](../adr/README.md)
- Politica de PR/documentacao: [ADR 0002](../adr/0002-politica-pr-docs-estrutural.md)
- Estrategia solo+IA/MCP: [ADR 0003](../adr/0003-mcp-viabilidade-e-estrategia-solo-ai.md)
- Template de PR: [PULL_REQUEST_TEMPLATE.md](../../.github/PULL_REQUEST_TEMPLATE.md)
- Workflow CI: [governance.yml](../../.github/workflows/governance.yml)
- Handoffs estruturais: [docs/handoffs/README.md](../handoffs/README.md)

## Fluxo Solo + IA

1. Ler [AGENTS.md](../../AGENTS.md).
2. Ler [Entrypoint do agente](ai/agent-entrypoint.md).
3. Classificar mudanca em [Classificacao de mudancas](ai/change-classification.md).
4. Aplicar validacoes da [Matriz de validacao](ai/validation-matrix.md).
5. Em mudanca estrutural, criar handoff em `docs/handoffs/`.

## Convencao rapida

- Wiki -> codigo: sempre `arquivo + #Llinha`.
- Codigo -> wiki: sempre marcador `DOCS:` em comentario.
- Cada mudanca tecnica relevante deve atualizar wiki e backlinks.
- Politica canonica: `docs/wiki/reference/ai-contract.v1.yaml`.


