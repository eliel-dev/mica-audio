# Matriz de validacao

## Objetivo

Definir checks obrigatorios por escopo para diminuir regressao em fluxo solo com IA.

## Matriz

| Escopo | Checks obrigatorios |
|---|---|
| Documental | `docs-validate` |
| Funcional | `docs-validate`, `dotnet build` |
| Estrutural | `docs-validate`, `ai-governance-check`, `dotnet build`, handoff |
| Firmware/protocolo | `docs-validate`, `ai-governance-check`, `dotnet build`, handoff |

## Comandos

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
```

## Referencias de codigo

- [ai-governance-check](../../../scripts/ai-governance-check.ps1#L1) - assinatura: `param(...)`
- [git hooks bootstrap](../../../scripts/git-hooks-bootstrap.ps1#L1) - assinatura: `param(...)`
