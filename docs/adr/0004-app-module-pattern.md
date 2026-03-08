# ADR 0004 - Padrao App Module para apps de catalogo

## Contexto

O fluxo de apps no Mica Audio cresceu com manifestos, preview renderers, modificadores e runtimes especificos. Sem um contrato comum, novos apps podem introduzir acoplamento indevido (UI chamando servicos concretos diretamente), validacao inconsistente de schema e deploy incompleto.

Precisamos de um padrao explicito para evoluir apps com previsibilidade, reaproveitando os artefatos atuais (`AppCatalogItem`, preview registry, state store e deploy service) e preparando extensao para runtimes opcionais.

## Decisao

Adotar o padrao **App Module**, com tres contratos arquiteturais:

1. `AppModuleDefinition`
   - Define identidade e metadados do app (manifest), preview e schema de configuracao.
   - Mapeia para o modelo de catalogo (`AppCatalogItem`) e seus subobjetos (`AppPreviewDefinition`, `AppModifierDefinition`).
2. `AppModuleConfigSchema`
   - Formaliza validacao da configuracao de entrada/saida de modificadores.
   - Serve de base para UI dinamica, persistencia de draft e payload de deploy (`set_app_config`).
3. `AppModuleRuntime` (opcional)
   - Encapsula comportamento de execucao local/preview avancado quando o app exige runtime dedicado (ex.: GIF).
   - Nao substitui o deploy remoto; complementa experiencia local quando necessario.

O detalhamento operacional (estrutura minima, contratos por camada, dependencias e criterios de aceitacao) fica padronizado no guia wiki de App Module.

## Consequencias

- Positivas:
  - Reduz ambiguidade para criar novos apps.
  - Impoe fronteiras de dependencia claras entre UI, contratos e servicos concretos.
  - Melhora governanca de qualidade com criterios minimos de aceite.
- Trade-offs:
  - Exige disciplina para manter schema e preview sincronizados.
  - Introduz checklists adicionais no onboarding de novos apps.

## Status

Aceita

## Data

2026-02-22

## Referencias

- docs/wiki/ai/app-module-pattern.md
- src/App.WinUI/Models/Apps/AppCatalogItem.cs
- src/App.WinUI/Models/Apps/AppPreviewDefinition.cs
- src/App.WinUI/Models/Apps/AppModifierDefinition.cs
- src/App.WinUI/Services/Apps/AppDeploymentService.cs
- src/App.WinUI/Services/Apps/AppModifierStateStore.cs
- src/App.WinUI/Views/Controls/AppPreviewRendererRegistry.cs
- src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs
