# Handoff - 2026-03-04 - devices-page-ui-declutter-logs-priority

## Objetivo
Aplicar cleanup P0 na `DevicesPage` para priorizar visibilidade dos logs e remover informacao redundante no painel de detalhes.

## Escopo classificado
`estrutural` (mudanca em `src/App.WinUI` + testes + documentacao wiki/handoff, sem mudanca de protocolo/firmware nesta entrega).

## Arquivos alterados
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/guides/setup-new-device.md`
- `docs/handoffs/2026-03-04-devices-page-ui-declutter-logs-priority.md`

## Decisoes tomadas
1. Removido totalmente o card visual `Comandos:` da area de detalhes.
2. Removidos blocos redundantes do dashboard: chips de online/Wi-Fi/snapshot/RSSI e bloco de conectividade/eventos.
3. Adicionado `SelectedDeviceSignalText` no topo do card de resumo, ao lado das acoes `Testar LED` e `Remover`.
4. Reorganizado `rightGrid` para 3 linhas: resumo, dashboard tecnico e logs.
5. Mantida logica interna de estado de comandos (`DeviceOperationsState.CommandStatus`), apenas sem renderizacao visual no card removido.
6. Card de logs recebeu prioridade de espaco (`logsCard.MinHeight = 280`) para melhorar leitura continua.

## Validacoes executadas
1. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"`
- Resultado: OK (4 aprovados, 0 falhas).
2. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceOperationsCoordinator|FullyQualifiedName~DeviceServerHostSecurityTests"`
- Resultado: OK (28 aprovados, 0 falhas).
3. `dotnet build MicaAudio.sln -c Debug`
- Resultado: OK (sem erros).
4. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- Resultado: OK (`nenhuma falha encontrada`).
5. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- Resultado: OK (`governanca IA valida`).

Observacao: warnings de analise (CA/MVVMTK/WIN2D) permanecem pre-existentes e nao bloquearam a entrega.

## Riscos e rollback
Riscos:
1. Perda de visibilidade imediata de progresso/comandos na UI detalhada.
2. Menos telemetria visual no dashboard pode exigir consulta direta aos logs em diagnosticos especificos.

Rollback:
1. Reverter este lote de alteracoes para restaurar card `Comandos` e blocos de conectividade/chips.
2. Nao ha migracao de dados; rollback e apenas de UI/codigo.

## Proximos passos
1. Validacao manual rapida da tela com um device online e um offline para confirmar legibilidade de logs e RSSI no topo.
2. Se necessario, expor status de comando de forma compacta no rodape da tela (fora do painel de detalhes), sem reintroduzir ruido visual.
