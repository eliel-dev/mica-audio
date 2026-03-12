# Referencia - Dashboard de Observabilidade por Device

## Objetivo

Documentar o dashboard por device agora servido localmente em HTML/JS para o `WebView2` da `DevicesPage`, mantendo `Logs` em `Configuracoes` e usando um DTO proprio do servidor para a superficie web.

## Superficie da UI

- `DevicesPage`:
  - painel esquerdo continua nativo em WinUI (`ListView`, previews inline, wizard USB);
  - painel direito agora hospeda um `WebView2` full-size;
  - sem selecao, a coluna direita continua colapsada.
- `SettingsPage`:
  - `Logs` permanecem em `Configuracoes`;
  - `Estatisticas` seguem fora da UI por enquanto.

## Transporte do dashboard

- HTTP:
  - `GET /dashboard`
  - redireciona para `/dashboard/index.html`
- Assets estaticos:
  - `/dashboard/index.html`
  - `/dashboard/dashboard.css`
  - `/dashboard/dashboard.js`
- WebSocket:
  - `WS /ws/device/{deviceId}`
  - envia snapshot inicial imediatamente;
  - publica updates quando o host observar mudanca relevante do device.

## DTO do dashboard

- Nome interno:
  - `DeviceDashboardDto`
- Campos:
  - `deviceId`
  - `name`
  - `online`
  - `activeAppName`
  - `wifiState`
  - `signalDbm`
  - `uptimeSeconds`
  - `telemetrySequence`
  - `testLedAvailable`
  - `testLedEnabled`
  - `testLedDuty`
  - `brightnessCap`
  - `brightnessRequested`
  - `brightnessApplied`
  - `loopLoadPercent`
  - `heapFreeBytes`
  - `heapLargestBlockBytes`
  - `heapTotalBytes`
  - `heapFreePercent`
  - `heapFragmentationPercent`
  - `psramAvailable`
  - `psramFreeBytes`
  - `psramLargestBlockBytes`
  - `psramTotalBytes`
  - `psramFreePercent`
  - `streamFramesReceived`
  - `streamFramesApplied`
  - `streamSequenceGapCount`
  - `streamInvalidFrameCount`
  - `streamLastSequence`
  - `hub75Fps`
- Regras:
  - `online` vem de `snapshot.Status == Online`;
  - percentuais de heap/PSRAM sao calculados no servidor quando os totais reais estiverem disponiveis em `stats`;
  - `heapFragmentationPercent` usa `1 - largest/free`;
  - `hub75Fps` e calculado no servidor com cache por `deviceId`, usando delta de `StreamFramesApplied`;
  - ausencia de dado continua sendo `null`.

## Semantica operacional do dashboard

- `loopLoadPercent` representa carga util do app no firmware:
  - tempo gasto em renderizacao, telemetria, controle MQTT/WS e trabalho efetivo do loop;
  - esperas deliberadas e periodos claramente ociosos ficam fora da amostra;
  - o objetivo e evitar o falso `99%` constante de um loop bare-metal sempre ativo.
- Memoria no dashboard HTML usa fallback hibrido:
  - com `heapFreePercent`/`psramFreePercent` reais, usa o DTO do servidor;
  - sem `stats`, calcula percentual localmente a partir de `freeHeapBytes` e `psramFreeBytes`;
  - baselines locais de compatibilidade visual:
    - heap: `320000` bytes;
    - PSRAM: `8000000` bytes.

## Bridge WinUI <-> HTML

- Host -> JS:
  - `ready-ack`
  - `select-device`
  - `clear-selection`
- JS -> Host:
  - `ready`
  - `set-brightness`
  - `test-led`
  - `remove-device`
- Regras:
  - a pagina HTML navega uma unica vez;
  - troca de device usa `postMessage`, sem recarregar o documento;
  - o slider faz preview local no JS e o commit real continua no host WinUI;
  - `remove-device` continua usando `ContentDialog` nativo no host.

## Logs e estatisticas fora do dashboard

- `Logs` estruturados continuam no `DeviceLogBook` e sao exibidos apenas na `SettingsPage`.
- `stats` MQTT continuam persistidos em `DeviceRecord` e `DeviceSnapshot`.
- O dashboard HTML consome somente o DTO projetado pelo servidor; ele nao conhece `DeviceSnapshot` nem `DeviceLogEntry`.

## Referencias de codigo

- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DevicesPage WebView dashboard](../../../src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs#L1)
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [SettingsPage observability](../../../src/App.WinUI/Views/SettingsPage.Observability.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [DeviceServerHost routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1)
- [DeviceServerHost dashboard](../../../src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs#L1)
- [Dashboard index](../../../src/Device.Server/wwwroot/dashboard/index.html#L1)
- [Dashboard CSS](../../../src/Device.Server/wwwroot/dashboard/dashboard.css#L1)
- [Dashboard JS](../../../src/Device.Server/wwwroot/dashboard/dashboard.js#L1)
