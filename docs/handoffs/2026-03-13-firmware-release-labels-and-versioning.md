# Handoff - Firmware Atual + Ultimo Release

## Objetivo

Ajustar a semantica de firmware na UI para mostrar `Firmware atual` e `Ultimo release`, onde `Ultimo release` representa o pacote oficial de firmware embarcado no app, e tornar o `firmwareVersion` desse pacote unico por geracao via timestamp UTC.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - dashboard HTML mostra `Firmware atual` e `Ultimo release`;
  - o dialogo WinUI de update usa a mesma linguagem;
  - fallbacks visiveis passam a ser `Firmware atual nao identificado` e `Sem release oficial`;
  - `scripts/build-precompiled-firmware.ps1` gera `firmwareVersion` no formato `vyyyy.MM.dd-HHmmssZ-<tag>-<sha>`;
  - duas execucoes consecutivas do script geram IDs diferentes mesmo no mesmo commit.

## Arquivos alterados

- `scripts/build-precompiled-firmware.ps1`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareCatalogAdapter.cs`
- `src/App.WinUI/Views/DevicesPage.FirmwareUpdate.cs`
- `src/Device.Server/wwwroot/dashboard/index.html`
- `src/Device.Server/wwwroot/dashboard/dashboard.js`
- `tests/Integration.Smoke/DashboardAssetSmokeTests.cs`
- `tests/Output.Tests/DeviceServerHostDashboardTests.cs`
- `docs/wiki/reference/device-observability-dashboard.md`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/guides/criticality-context7-audit.md`

## Decisoes tomadas

1. `Ultimo release` ficou restrito ao pacote oficial de firmware embutido no app; a entrega nao cria conceito separado para tag GitHub release.
2. O contrato interno `firmwareVersion` / `latestFirmwareVersion` foi preservado; a mudanca ficou na semantica exibida e no formato do identificador.
3. O novo formato oficial do pacote passou para `vyyyy.MM.dd-HHmmssZ-<tag>-<sha>`, mantendo `builtAtUtc` separado em ISO 8601.
4. A disponibilidade de update continua por igualdade exata entre `snapshot.FirmwareVersion` e `artifact.Manifest.FirmwareVersion`, sem ordenacao semantica.
5. O pacote oficial foi regenerado nesta entrega; os builds validados produziram:
   - `v2026.03.14-030139Z-untagged-06bd344`
   - `v2026.03.14-030215Z-untagged-06bd344`

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -> OK (segunda execucao com versao diferente)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~DeviceServerHostDashboardTests|FullyQualifiedName~PrecompiledFirmwareServiceTests" -> OK (5 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DashboardAssetSmokeTests" -> OK (1 teste)
```

## Riscos e rollback

- Risco principal: como o identificador oficial agora inclui timestamp, qualquer regeneracao do pacote passa a marcar update disponivel mesmo sem mudanca de codigo no firmware.
- Como reverter:
  - restaurar `Resolve-FirmwareVersion()` para o formato anterior baseado apenas em data;
  - regenerar o pacote oficial;
  - voltar os rotulos da UI para a semantica anterior, se necessario.

## Proximos passos

1. Abrir a `DevicesPage` e confirmar visualmente que o dashboard e o dialogo mostram `Ultimo release` com a nova string do manifesto.
2. Se o time quiser distinguir futuramente release de app vs release de firmware, introduzir isso como campo separado de manifesto/DTO em uma entrega propria.
3. Avaliar se a politica de update por igualdade exata continua adequada quando houver regeneracoes frequentes do pacote oficial sem mudanca funcional no firmware.
