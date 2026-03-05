# Handoff - 2026-03-05 - esptool-install-progress

## Objetivo
Padronizar o onboarding USB no perfil canonicamente solicitado de `esptool` e exibir progresso real de flashing (`0..100%`) no wizard de `Novo dispositivo`.

## Escopo classificado
- Classificacao: `funcional` (App.WinUI).
- Inclui:
  - ajuste da linha de comando de flash para `115200`, `before/after reset`, `--no-compress`;
  - parser de percentual robusto para formatos `NN%` e `NN %`;
  - barra + percentual no wizard durante etapa `Flashing`;
  - testes de perfil de comando/parser e contrato visual do wizard.
- Nao inclui:
  - mudanca de protocolo wire (`HTTP/WS/serial`);
  - mudanca de firmware;
  - `erase_flash` automatico.

## Arquivos alterados
- `src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `tests/Integration.Smoke/EspToolFlashServiceTests.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`

## Decisoes tomadas
1. Perfil de flash fixo: `--baud 115200`, `--before default_reset`, `--after hard_reset`, `write_flash --no-compress 0x0`.
2. Manter estrategia de resolucao do executavel:
   - bundle local em `tools/esptool/win-x64`;
   - fallback `python -m esptool`.
3. Nao executar `erase_flash`.
4. Exibir progresso visual somente na etapa `Flashing`.

## Validacoes executadas
1. `dotnet build MicaAudio.sln -c Debug`
2. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~EspToolFlashServiceTests"`
3. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceOperationsCoordinator|FullyQualifiedName~DeviceServerHostSecurityTests"`
4. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
5. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`

## Riscos e rollback
- Risco: flashing mais lento por usar `115200` e `--no-compress`.
- Mitigacao: progresso visual explicito no wizard para feedback operacional.
- Rollback:
  1. Reverter `EspToolFlashService` para perfil anterior (`921600` + `-z`).
  2. Manter wizard funcional (barra de progresso e nao invasiva para protocolo).

## Proximos passos
1. Smoke manual em bancada com ao menos 2 devices (COM diferentes).
2. Coletar tempo medio de flash para definir SLA operacional.
3. Se necessario, abrir trilha separada para modo rapido opcional com fallback automatico.
