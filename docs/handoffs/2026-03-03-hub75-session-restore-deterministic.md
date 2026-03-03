# Handoff - HUB75 session restore deterministic (RSK-001)

## Objetivo

Registrar a correcao P0 do RSK-001 para tornar deterministico o fluxo `disable -> restore app anterior` no `Hub75VisualizerSessionService`, removendo dependencia de eventos externos para retry.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: restore inicial imediato apos disable, retry autonomo por timer sem novo `DevicesChanged`, testes de sessao HUB75 passando.

## Arquivos alterados

- `src/App.WinUI/Services/Devices/Hub75VisualizerSessionService.cs`
- `tests/Output.Tests/Hub75VisualizerSessionServiceTests.cs`
- `docs/wiki/guides/criticality-context7-audit.md`
- `docs/handoffs/2026-03-03-hub75-session-restore-deterministic.md`

## Decisoes tomadas

1. Na transicao `hub75Enabled: true -> false`, o estado de restore nao herda cooldown da ativacao: `NextAttemptUtc` e resetado para `DateTimeOffset.MinValue` com `Status = RestorePending` para sessoes com `PreviousAppId` valido.
2. Mantivemos `DispatchCooldown` global para anti-burst, sem alterar constantes de backoff existentes.
3. Foi adicionado scheduler interno de reconciliacao para pendencias futuras (`delayedReconcileCts` + `delayedReconcileAtUtc`), com coalescencia para o menor horario e cancelamento seguro em `Dispose`.
4. O plano de reconciliacao passou a carregar `NextRetryUtc` para agendamento autonomo sem depender de novo evento de device.
5. A cobertura de testes foi expandida com cenario de falha `busy` + retry automatico sem `SetDevices` adicional.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~Hub75VisualizerSessionServiceTests" -> OK (4/4)
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests" -> OK (4/4)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (7 warnings, 0 errors)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~Hub75VisualizerSessionServiceTests" -> OK (4/4)
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --no-build --filter "FullyQualifiedName~DevicesPageSmokeTests" -> OK (4/4)
```

## Riscos e rollback

- Risco residual: regressao de temporizacao em cenarios de concorrencia com multiplos devices e retries sobrepostos.
- Mitigacao: scheduler coalescido para menor `NextAttemptUtc`, cancelamento de timer anterior e verificacao de instancia ativa por referencia de `CTS`.
- Rollback: reverter apenas `Hub75VisualizerSessionService` e o novo teste `Disable_ShouldRetryRestore_AfterCooldown_WithoutNewDeviceEvent`; restaurar status do backlog RSK-001 para aberto no guia de auditoria.

## Proximos passos

1. Rodar novamente suite focada de `Output.Tests` em CI para monitorar estabilidade temporal do fluxo corrigido.
2. Se houver oscilacao, adicionar metricas internas de trace para timestamps de `NextAttemptUtc` e disparo do scheduler.
