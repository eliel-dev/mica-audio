# Handoff - Criticality + Context7 audit (app + servidor + firmware)

## Objetivo

Registrar auditoria tecnica completa priorizada por risco operacional para identificar pontos criticos do projeto e validar atualizacao da implementacao com Context7.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: guia de auditoria publicado, backlog priorizado documentado, evidencias de build/test/dependencias/context7 registradas e wiki indexada.

## Arquivos alterados

- `docs/wiki/guides/criticality-context7-audit.md`
- `docs/wiki/README.md`
- `docs/handoffs/2026-03-03-criticality-context7-audit.md`

## Decisoes tomadas

1. Modelo de criticidade fixado com peso maior em impacto operacional (0.4), seguido de exposicao (0.3), probabilidade (0.2) e lacuna de teste (0.1).
2. Validacao de atualizacao foi definida como completa: API/docs (Context7), versao, breaking-change potencial e vulnerabilidade.
3. `firmware/matrixportal-s3` foi tratado como trilha experimental/incompleta e registrado como risco de cobertura, sem promover para trilha oficial.
4. Nao houve alteracao de codigo runtime nesta fase; entrega ficou restrita a artefatos documentais e backlog.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (1 warning WIN2D0001 em Integration.Smoke AnyCPU)
dotnet test MicaAudio.sln -c Debug --no-build -> FALHOU (2 testes em Hub75VisualizerSessionServiceTests)
dotnet list MicaAudio.sln package --outdated --include-transitive -> OK (drift detectado)
dotnet list MicaAudio.sln package --vulnerable --include-transitive -> OK (sem vulnerabilidades nesta rodada)
$env:PYTHONIOENCODING='utf-8'; platformio pkg list -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> OK
platformio pkg outdated -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> OK (ArduinoJson 7.4.2 -> 7.4.3)
Context7 (4 bibliotecas alvo) -> OK (IDs resolvidos + docs consultadas)
```

## Riscos e rollback

- Risco principal: backlog de atualizacao e duas falhas de teste ativas podem mascarar regressao de sessao HUB75.
- Como reverter: remover os artefatos documentais desta entrega (`guide + handoff + links no README`) sem impacto em runtime.

## Proximos passos

1. Corrigir falhas de `Hub75VisualizerSessionServiceTests` e estabilizar criterio de timeout/reconnect.
2. Abrir fase de hardening para migrar autenticacao WS totalmente para header e desligar query token legado por default.
3. Executar trilha de upgrade de dependencias por lote (P0/P1/P2) usando o backlog desta auditoria como plano mestre.
