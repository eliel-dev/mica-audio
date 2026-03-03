# 2026-03-02 - Device Lifecycle Lightweight State Model

## Objetivo

Implementar um modelo leve de estado de device para a `DevicesPage`, capaz de distinguir registro local, conectividade e incerteza de configuracao sem introduzir `shadow`, timeline de eventos ou mudanca de protocolo.

## Escopo classificado

Mudanca estrutural de `App.WinUI` + `Device.Server` + `Device.Protocol` em torno de presenca e apresentacao de devices, mantendo o protocolo wire e o firmware inalterados.

## Arquivos alterados

- `src/Device.Protocol/Models/DeviceConfigState.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/MicaAudio.Core/Presets/AppSettings.cs`
- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/App.WinUI/Services/AppSettingsDomainService.cs`
- `src/App.WinUI/Services/Devices/DeviceRegistryPresenceNormalizer.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecycleThresholds.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecycleTone.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecycleIcon.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecyclePresentation.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecyclePolicy.cs`
- `src/App.WinUI/Services/Devices/DeviceListVisibilityPolicy.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/Controls/DeviceListRowControl.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `scripts/migrate-device-registry-presence-v1.ps1`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/DeviceListVisibilityPolicyTests.cs`
- `tests/Output.Tests/DeviceLifecyclePolicyTests.cs`
- `tests/Output.Tests/DeviceRegistryPresenceMigrationTests.cs`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `docs/wiki/modules/device-operations-coordinator.md`
- `docs/wiki/architecture/05-device-session-and-reconnect.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/settings-presets-persistence.md`
- `docs/wiki/modules/device-server-protocol.md`

## Decisoes tomadas

- `DeviceStatus` continua representando conectividade efetiva; `Pairing` foi tratado explicitamente como compatibilidade legada e mapeado para `Aguardando provisionamento`.
- `LastAuthUtc` passa a nascer no handshake WebSocket concluido, em `HandleWebSocketAsync`, logo apos autenticacao valida.
- `LastTelemetryUtc` e atualizado apenas no processamento de telemetria.
- A UI diferencia `Online | Configurado`, `Offline | Configurado`, `Offline | Configuracao incerta` e `Registrado | Nunca conectado`.
- Os thresholds de lifecycle ficaram configuraveis em `AppSettings` com clamp e ordem coerente (`Fresh < Stale < Dormant`).
- A migracao de registro foi implementada em duas camadas: script explicito + fallback em runtime.
- A solucao permanece deliberadamente leve: sem `shadow`, sem timeline de lifecycle, sem mudanca de protocolo.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Devices|FullyQualifiedName~WinUiBootstrap"`

## Riscos e rollback

Riscos:

- registros muito antigos sem `LastSeenUtc` confiavel ainda podem aparecer com `Ultimo contato: desconhecido`;
- `Configuracao incerta` continua sendo heuristica, nao confirmacao de perda real de configuracao;
- `Pairing` permanece no enum por compatibilidade e ainda exige leitura cuidadosa do dominio.

Rollback:

1. remover `DeviceLifecyclePolicy` da `DevicesPage` e voltar a exibir apenas `DeviceStatus` bruto;
2. ignorar os novos campos de presence em `DeviceRecord`/`DeviceSnapshot`;
3. restaurar thresholds hardcoded;
4. descontinuar o script de migracao e o fallback correspondente.

## Proximos passos

1. Validar com um device real os quatro estados principais na `DevicesPage`.
2. Se a heuristica se mostrar insuficiente, a proxima etapa correta e adicionar um sinal explicito de provisioning no protocolo/firmware.
3. Se surgir necessidade de tuning fino, expor os thresholds de lifecycle em UI administrativa futura.
