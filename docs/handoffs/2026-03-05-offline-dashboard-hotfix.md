# Handoff - 2026-03-05 - offline-dashboard-hotfix

## Objetivo
Eliminar o crash do `App.WinUI` ao selecionar dispositivo offline na `DevicesPage`, aplicando fallback seguro de dashboard e hardening no caminho de renderizacao WinUI/XAML.

## Escopo classificado
- Classificacao: `funcional` (App.WinUI) com alteracao em `src/`.
- Inclui:
  - fallback visual offline simplificado (resumo + logs);
  - protecao de excecao no caminho `ApplySelectionDetails -> ApplyDashboard`;
  - sanitizacao de calculos/valores numericos antes de atribuicao em controles;
  - teste de smoke para robustez no caminho offline.
- Nao inclui:
  - mudancas de protocolo HTTP/WS/serial;
  - mudancas de firmware;
  - refatoracao ampla da UI.

## Arquivos alterados
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`

## Decisoes tomadas
1. Estrategia P0: hotfix imediato com degradacao controlada para offline.
2. Offline selecionado nao renderiza blocos avancados (`ESP-DASH`, conectividade detalhada e charts).
3. Falhas de render nao podem derrubar o app; devem cair em fallback seguro e registrar log local.
4. O fluxo online continua com dashboard completo.

## Validacoes executadas
1. `dotnet build MicaAudio.sln -c Debug`
2. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~DevicesPage"`
3. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceOperationsCoordinator|FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~DeviceMetricsFormatterTests"`
4. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
5. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`

## Riscos e rollback
- Risco: ocultar blocos avancados no offline reduz densidade de diagnostico visual.
- Mitigacao: manter logs e resumo ativos, preservar dashboard completo no online.
- Rollback: reverter commit deste hotfix (`DevicesPage.xaml.cs` + smoke) para comportamento anterior.

## Proximos passos
1. Validar em bancada alternando online/offline repetidamente.
2. Se necessario, abrir fase 2 para diagnostico forense fino (dump/simbolos) da excecao WinUI.
3. Reavaliar reabilitacao gradual de blocos offline apos estabilizacao.
