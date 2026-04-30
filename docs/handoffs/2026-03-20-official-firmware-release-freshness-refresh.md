# Handoff - Refresh automatico do release oficial de firmware

## Objetivo

Corrigir o backend do app para que o dashboard, o OTA e o wizard USB passem a trabalhar com um release oficial local de firmware realmente fresco em workspace/dev, regenerando o pacote oficial quando `AppData/Firmware` ficar stale em relacao aos fontes do firmware.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - o dashboard nao anuncia mais um `Ultimo release` velho como se estivesse atual quando o workspace estiver stale;
  - OTA e wizard USB executam preflight de frescor antes de usar o release oficial;
  - em workspace/dev, o app chama o script oficial `build-precompiled-firmware.ps1` para regenerar o pacote sidecar;
  - em modo read-only/distribuido sem repo/script, o app continua consumindo apenas o pacote embarcado.

## Arquivos alterados

- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
- `src/App.WinUI/Services/Firmware/OfficialFirmwareRefreshResult.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareCatalogAdapter.cs`
- `src/App.WinUI/Views/DevicesPage.FirmwareUpdate.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/MicaAudio.Core/Config/MicaAudioOptions.cs`
- `scripts/build-precompiled-firmware.ps1`
- `tests/Output.Tests/PrecompiledFirmwareServiceTests.cs`
- `tests/Integration.Smoke/DeviceUsbOnboardingServiceTests.cs`
- `tests/Integration.Smoke/FirmwareCatalogSmokeTests.cs`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`
- `docs/handoffs/2026-03-20-official-firmware-release-freshness-refresh.md`

## Decisoes tomadas

1. O contrato do produto foi preservado:
   - o app continua enviando apenas o release oficial local;
   - nao houve flash direto de binarios arbitrarios da `.pio/build`.
2. O `PrecompiledFirmwareService` passou a separar dois conceitos:
   - resolver artefato bruto por manifesto;
   - expor apenas artefato oficial fresco para dashboard/OTA/wizard.
3. O frescor em workspace/dev passou a usar `manifest.BuiltAtUtc` como carimbo canonico do release oficial, comparado contra os insumos reais do firmware.
4. O build automatico continua usando exclusivamente o script oficial:
   - `scripts/build-precompiled-firmware.ps1`
   - agora com `-OutputRoot` para regenerar no diretório efetivo usado pelo app em runtime.
5. O refresh foi ligado em dois pontos operacionais:
   - warm-up em background no startup;
   - preflight antes de OTA e antes do wizard USB.
6. Quando o pacote oficial local esta stale e ainda nao houve regeneracao com sucesso, o catalogo oficial fica indisponivel por desenho.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "PrecompiledFirmwareServiceTests"
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FirmwareCatalogSmokeTests|DeviceUsbOnboardingServiceTests|WinUiBootstrapSmokeTests"
```

## Riscos e rollback

- Risco principal: em workspace/dev, o primeiro refresh automatico pode demorar alguns segundos por depender do script oficial e do toolchain local.
- Risco secundario: falhas no script oficial agora bloqueiam OTA/wizard, o que e intencional para evitar publicar um release velho como se fosse atual.
- Rollback:
  - remover a trilha `EnsureOfficialFirmwareFreshAsync(...)`;
  - fazer o catalogo voltar a resolver `TryResolveArtifact(...)` diretamente;
  - remover o preflight de startup/OTA/wizard.

## Proximos passos

1. Validar manualmente no app real:
   - editar `main.cpp`;
   - abrir a app;
   - confirmar que o `Ultimo release` passa a refletir o manifesto regenerado.
2. Rodar o wizard USB com o release novo e confirmar que o texto `Firmware selecionado` mostra a versao fresca.
3. Se necessario, evoluir a telemetria do refresh para expor um breadcrumb mais explicito no dashboard tecnico do app.
