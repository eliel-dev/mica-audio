# DevicesPage Keep Offline Visible

## Objetivo

Corrigir o comportamento em que ESPs ja configurados sumiam da `DevicesPage` ao oscilar brevemente entre `Online` e `Offline`, exigindo reset manual para reaparecerem.

## Escopo classificado

`firmware/protocolo` pelo contrato de governanca, pois a mudanca toca `src/Device.Server/` e `src/App.WinUI/`, embora o ajuste seja apenas de comportamento (sem alterar protocolo wire ou firmware).

## Arquivos alterados

- `src/App.WinUI/Services/Devices/DeviceListVisibilityPolicy.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/Controls/DeviceListRowControl.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/DeviceListVisibilityPolicyTests.cs`
- `docs/wiki/modules/device-operations-coordinator.md`
- `docs/wiki/architecture/05-device-session-and-reconnect.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

- A lista da `DevicesPage` deixou de ser `online-only`.
- Devices com `DeviceId` valido continuam visiveis mesmo quando ficam `Offline`.
- A ordenacao passou a priorizar `Online`, depois `Pairing`, e deixar `Offline` por ultimo.
- O timeout de stale/offline do `Device.Server` subiu de `6s` para `15s`.
- Acoes operacionais continuam bloqueadas para devices offline.
- A linha do device ganhou leve reducao de opacidade no bloco de texto quando o status e `Offline`.

## Validacoes executadas

- Pendente apos aplicacao do patch nesta entrega:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - `dotnet build MicaAudio.sln -c Debug`
  - `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
  - `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Devices|FullyQualifiedName~WinUiBootstrap"`

## Riscos e rollback

- Risco: devices offline agora ficam visiveis por mais tempo, o que pode aumentar a percepcao de lista "maior" em ambientes com dispositivos antigos persistidos.
- Risco: se algum fluxo dependia implicitamente do filtro `online-only`, ele agora passara a receber items `Offline`.
- Rollback:
  - restaurar o filtro por `DeviceStatus.Online` em `DeviceOperationsCoordinator`
  - restaurar o timeout de `6s` em `DeviceServerHost`
  - remover `DeviceListVisibilityPolicy`

## Proximos passos

- Validar com um ESP real que oscila de telemetria sem desaparecer da lista.
- Se ainda houver flapping perceptivel, considerar elevar o timeout novamente ou instrumentar melhor a causa raiz de reconnect no firmware/rede.

