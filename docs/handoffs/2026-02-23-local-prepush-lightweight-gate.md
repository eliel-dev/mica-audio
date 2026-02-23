# Handoff - gate local leve para pre-push

## Objetivo

Destravar o `git push` local em maquinas sem SDK/UAP completo, mantendo rigor estrito no CI para merge.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: hook `pre-push` roda gate local leve sem depender do build completo da solucao e o CI continua validando `MicaAudio.sln`.

## Arquivos alterados

- scripts/local-prepush-gate.ps1
- .githooks/pre-push
- .gitignore
- docs/wiki/reference/troubleshooting-matrix.md
- docs/handoffs/2026-02-23-local-prepush-lightweight-gate.md

## Decisoes tomadas

1. Substituir o hook `pre-push` para chamar um gate local dedicado (`scripts/local-prepush-gate.ps1`) em vez de build da solucao inteira.
2. Manter no gate local: `docs-validate`, `ai-governance-check`, build de `App.WinUI` e `Output.Tests` para preservar qualidade sem exigir SDK UAP.
3. Garantir fail-fast no script local com verificacao explicita de exit code apos cada etapa.
4. Manter lockfiles versionados e explicitar isso no `.gitignore` com regra de unignore para evitar confusao futuras.
5. Documentar no troubleshooting a diferenca entre gate local e gate estrito no CI.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\local-prepush-gate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

## Riscos e rollback

- Risco principal: reduzir o gate local pode permitir push de mudancas que falhariam apenas no CI completo.
- Como reverter: restaurar `.githooks/pre-push` para `dotnet build MicaAudio.sln -c Debug` e remover `scripts/local-prepush-gate.ps1`.

## Proximos passos

1. Confirmar branch protection exigindo checks `governance-ai-guardrails` e `governance-build-debug` no GitHub.
2. Se necessario, adicionar um modo opcional no gate local para rodar build completo quando ambiente tiver SDK UAP instalado.
3. Monitorar falhas de CI nas proximas PRs para calibrar se o gate local precisa de mais um teste rapido.
