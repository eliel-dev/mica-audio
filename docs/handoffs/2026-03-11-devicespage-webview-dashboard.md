# Handoff - DevicesPage WebView Dashboard

## Objetivo

Substituir o dashboard nativo da `DevicesPage` por um `WebView2` que consome um dashboard HTML/JS servido localmente pelo `DeviceServerHost`, com DTO dedicado para a superficie web e sem mudar interfaces publicas de `Device.Protocol`, `Device.Server` ou `DeviceOperationsCoordinator`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - `DevicesPage` usa `WebView2` no painel direito e continua ocultando a coluna sem device selecionado;
  - `DeviceServerHost` serve `GET /dashboard` + assets estaticos do dashboard;
  - `WS /ws/device/{deviceId}` envia DTO dedicado do dashboard;
  - brilho, `Testar LEDs` e `Remover` continuam executados pelo host WinUI;
  - wiki, code index e referencia de contrato atualizados.

## Arquivos alterados

- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.Selection.cs`
- `src/App.WinUI/Views/DevicesPage.ListState.cs`
- `src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/Device.Server/Device.Server.csproj`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs`
- `src/Device.Server/wwwroot/dashboard/index.html`
- `src/Device.Server/wwwroot/dashboard/dashboard.css`
- `src/Device.Server/wwwroot/dashboard/dashboard.js`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `tests/Output.Tests/DeviceServerHostDashboardTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/device-observability-dashboard.md`

## Decisoes tomadas

1. O pacote `Microsoft.Web.WebView2` ja existia no `App.WinUI`; a entrega reutiliza a referencia atual.
2. O servidor nao envia `DeviceSnapshot` bruto ao dashboard; ele projeta `DeviceDashboardDto` interno.
3. `GET /dashboard` foi tratado antes do middleware de static files para evitar erro ao acessar o diretorio fisico `/dashboard`.
4. A troca de device no painel usa `postMessage` (`select-device` / `clear-selection`) em vez de recarregar a pagina.
5. O slider, `Testar LEDs` e `Remover` continuam com execucao real no host WinUI; `remove-device` preserva `ContentDialog` nativo.
6. `Logs` continuam somente em `Configuracoes`; o dashboard principal fica focado em visualizacao e controle do device.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -m:1 -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceServerHostDashboardTests|FullyQualifiedName~DeviceServerHostMqttTests" -> OK (12 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~SettingsPageSmokeTests" -> OK (14 testes)
```

## Riscos e rollback

- Risco principal:
  - regressao visual/comportamental no painel direito agora depende do host HTTP local e do runtime do `WebView2`.
- Como reverter:
  - restaurar o dashboard WinUI nativo na `DevicesPage`;
  - remover o `WebView2` e os endpoints `/dashboard` / `/ws/device/{deviceId}` do `DeviceServerHost`.

## Proximos passos

1. Validar manualmente lado a lado com `C:\Users\eliels\Documents\nice\mica-dashboard.html`.
2. Refinar o DTO do dashboard conforme a curadoria real do que deve permanecer no painel principal.
3. Se a experiencia ficar estavel, considerar mover parte da configuracao visual do HTML para tokens compartilhados do app.
