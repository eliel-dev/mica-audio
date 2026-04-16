# Handoff - 2026-04-16 - wizard-serial-boot-logs

## Objetivo

Adicionar diagnostico serial de boot sob demanda ao wizard USB de onboarding, mantendo o fluxo oficial `COM -> flash -> pair code -> AP` e permitindo recapturar o boot a `115200` sem disputar a porta com o flash.

## Escopo classificado

- Classificacao: `estrutural`.
- Inclui: `DevicesPage`, `ISerialMonitorService`, helper comum de `mica.serial.v1`, smoke/unit tests e documentacao operacional do wizard.
- Nao inclui: mudanca funcional no firmware, alteracao do perfil do `esptool` ou retorno do provisioning serial como caminho feliz.

## Arquivos alterados

- `src/App.WinUI/Infrastructure/Serial/MicaSerialProtocol.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialMonitorService.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialProvisioningClient.cs`
- `src/App.WinUI/Infrastructure/Serial/WizardSerialMonitorPolicy.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.Onboarding.cs`
- `src/App.WinUI/Views/DevicesPage.ListState.cs`
- `src/App.WinUI/Views/DevicesPage.WizardSerial.cs`
- `src/App.WinUI/App.xaml.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/SerialMonitorServiceTests.cs`
- `tests/Output.Tests/MicaSerialProtocolTests.cs`
- `tests/Output.Tests/WizardSerialMonitorPolicyTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. O wizard passou a reutilizar o `ISerialMonitorService` existente, em vez de criar uma stack serial paralela.
2. O fluxo continua sequencial: flash primeiro, monitor serial depois.
3. O monitor serial do wizard fica recolhido por padrao em `Ver mais`, mas autoexpande em falha de flash, falta de reconexao da porta ou ausencia de logs de boot.
4. `Recapturar boot` usa reset controlado pela porta serial ja aberta; o comando de flash do `esptool` nao foi alterado.
5. O parse de `mica.serial.v1` foi centralizado em `MicaSerialProtocol` e reaproveitado tanto pelo cliente legado quanto pelo wizard.
6. O encerramento automatico do monitor depende de `hello.deviceId` e do mesmo `deviceId` aparecer como `MqttOnline` na `DevicesPage`.

## Validacoes executadas

- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~SerialMonitorServiceTests|FullyQualifiedName~MicaSerialProtocolTests|FullyQualifiedName~WizardSerialMonitorPolicyTests"` -> OK.
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK.
- `dotnet build MicaAudio.sln -c Debug` -> OK.
- Observacao: `dotnet restore/build` continua emitindo warnings `NU190x` preexistentes para `Magick.NET-Q8-AnyCPU 14.11.1`; nenhum erro novo ficou pendente.

## Riscos e rollback

- Risco: a mesma instancia singleton de monitor serial pode ser disputada se `SettingsPage` e wizard ficarem abertos em sequencia.
  - Mitigacao: o wizard desconecta e limpa a sessao ao abrir, e a libera ao fechar.
- Risco: alguns devices podem reenumerar a porta USB com outro `COMx` apos o flash.
  - Mitigacao: a reconexao tenta `PortName`, `PnpDeviceId`, `VidPid` e fallback de preferencia.
- Rollback:
  1. remover `DevicesPage.WizardSerial.cs` do fluxo do wizard;
  2. retirar o painel `Ver mais` da UI;
  3. manter apenas o monitor serial em `SettingsPage`.

## Proximos passos

1. Validar em bancada se o wizard realmente captura o boot apos o flash em placas com e sem reenumeracao de porta.
2. Confirmar se o `hello` de `mica.serial.v1` aparece cedo o suficiente para descobrir `deviceId` de forma confiavel.
3. Se a bancada mostrar perda recorrente do boot inicial, considerar ajustar somente a janela de reconexao do wizard, sem mexer no perfil padrao do `esptool`.
