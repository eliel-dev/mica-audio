# Referencia - Dashboard de Observabilidade por Device

## Objetivo

Documentar o dashboard por device agora servido localmente em HTML/JS para o `WebView2` da `DevicesPage`, mantendo diagnostico serial bruto fora do desktop/dashboard e usando um DTO proprio do servidor para a superficie web.

## Superficie da UI

- `DevicesPage`:
  - painel esquerdo continua nativo em WinUI (`ListView`, previews inline, acoes globais de firmware e pairing);
  - painel direito agora hospeda um `WebView2` full-size;
  - sem selecao, a coluna direita continua colapsada.
- `SettingsPage`:
  - permanece restrita a `Geral`, Mica e logs de erro;
  - nao hospeda console serial nem estatisticas do device.

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

- Nome do contrato:
  - `DeviceTelemetryResponse` (em `Device.Protocol/Models`)
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
  - `firmwareVersion`
  - `latestFirmwareVersion`
  - `firmwareUpdateAvailable`
  - `firmwareUpdateSupported`
  - `brightnessCap`
  - `brightnessRequested`
  - `brightnessApplied`
  - `loopHealthyPercent`
  - `loopLoadPercent`
  - `chipTemperatureCelsius`
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
  - `hub75Fps` e calculado no servidor com cache por `deviceId`, usando delta de `Hub75PresentFrames`;
  - `streamFramesApplied` representa payload novo efetivamente exibido ao menos uma vez;
  - ausencia de dado continua sendo `null`.

## API REST de telemetria

Alem do WebSocket push, o servidor expoe telemetria por device via endpoint admin REST:

- `GET /api/v1/admin/devices/{deviceId}/telemetry`
- Autenticacao: `X-Mica-Admin-Token` ou `Authorization: Bearer <token>`.
- Resposta: `DeviceTelemetryResponse` com os mesmos campos do DTO do dashboard.
- Comportamento:
  - `200 OK` + payload JSON quando o device existe;
  - `404 NotFound` quando o device nao e encontrado;
  - `401 Unauthorized` / `503 ServiceUnavailable` conforme regras da Admin API.
- Uso recomendado:
  - clientes moveis (Android/iOS) que precisam consultar estado pontual sem manter WebSocket;
  - integracoes externas que consomem telemetria sob demanda.

## Semantica operacional do dashboard

- O card oficial de saude usa `loopHealthyPercent`:
  - percentual de iteracoes do `loop()` concluidas em ate `25 ms`;
  - calculo feito no firmware em janela fixa de `5 s`;
  - thresholds visiveis no dashboard:
    - `>= 90`: `Saudavel: loop estavel`;
    - `>= 75 e < 90`: `Atencao: latencia moderada`;
    - `< 75`: `Sobrecarregado: latencia elevada`.
- `loopLoadPercent` permanece apenas como campo legado de compatibilidade no DTO e no snapshot:
  - o dashboard HTML/WebView2 nao usa mais esse campo para saude;
  - a permanencia evita quebra de round-trip com payloads antigos.
- O dashboard ganhou um card adicional `Temperatura do chip`:
  - usa `chipTemperatureCelsius`;
  - mostra valor atual com ate `1` casa decimal em `degC`;
  - quando ausente, renderiza `-` com subtitulo `Sensor interno indisponivel`.
- O grafico inferior passou a usar historico de `loopHealthyPercent` e o titulo `Saude do dispositivo (historico)`.
- O dashboard considera que ha metricas quando existir qualquer dado entre:
  - `loopHealthyPercent`;
  - memoria heap;
  - memoria PSRAM;
  - `chipTemperatureCelsius`.
- A area de acoes agora mostra estado de firmware por device:
  - `Firmware atual` vem de `snapshot.FirmwareVersion`;
  - `Ultimo release` vem do catalogo local de pacotes precompilados;
  - `Ultimo release` significa o pacote oficial de firmware embarcado no app, nao uma tag separada de GitHub;
  - quando a versao atual nao existe no snapshot, a UI mostra `Firmware atual nao identificado`;
  - quando nao existir pacote oficial compativel, a UI mostra `Sem release oficial`;
  - o CTA `Atualizar firmware` so aparece quando existir pacote oficial compativel, a versao atual diferir do ultimo release e o device estiver online para OTA agora.
- O CTA de firmware fica acima de `Testar LEDs`:
  - device online abre dialogo nativo com OTA como unica acao;
  - device offline continua vendo `Firmware atual` e `Ultimo release`, mas nao recebe CTA de update;
  - o dialogo nativo usa os rotulos `Firmware atual` e `Ultimo release`.
- A barra superior da tela de dispositivos expõe `Copiar link do dashboard`:
  - gera um link LAN para o device selecionado com `/dashboard?deviceId=<id>`;
  - usa o host/porta publicos do app, sem reescrever para `127.0.0.1`;
  - nao usa `embedded=1`.
- O fluxo de OTA na UI usa o resultado final do comando tracked:
  - `rebooting` e apenas progresso intermediario;
  - sucesso real so acontece quando o firmware novo publica `validated`;
  - `rolled-back` e `timeout` sao tratados como falha da atualizacao;
  - polling de `FirmwareVersion` fica apenas como diagnostico auxiliar apos o sucesso tracked.
- Memoria no dashboard HTML usa fallback hibrido:
  - com `heapFreePercent`/`psramFreePercent` reais, usa o DTO do servidor;
  - sem `stats`, calcula percentual localmente a partir de `freeHeapBytes` e `psramFreeBytes`;
  - baselines locais de compatibilidade visual:
    - heap: `320000` bytes;
    - PSRAM: `8000000` bytes.
- O bloco de stream/HUB75 agora tem a semantica:
  - `streamFramesReceived`: payloads aceitos do stream;
  - `streamFramesApplied`: payloads novos efetivamente exibidos ao menos uma vez;
  - `hub75Fps`: taxa de apresentacao efetiva do HUB75, nao apenas chegada de payload.
- A grade de metricas do dashboard HTML usa:
  - `4` colunas em largura ampla;
  - `2` colunas ate `1200 px`;
  - `1` coluna abaixo de `860 px`.

## Bridge WinUI <-> HTML

- Host -> JS:
  - `ready-ack`
  - `select-device`
  - `clear-selection`
- JS -> Host:
  - `ready`
  - `update-firmware`
  - `set-brightness`
  - `test-led`
  - `remove-device`
- Regras:
  - a pagina HTML navega uma unica vez;
  - troca de device usa `postMessage`, sem recarregar o documento;
  - o slider faz preview local no JS e o commit real continua no host WinUI;
  - `remove-device` continua usando `ContentDialog` nativo no host.

## Logs e estatisticas fora do dashboard

- `Logs` estruturados continuam no `DeviceLogBook`, mas nao sao mais a superficie mostrada na `SettingsPage`.
- Diagnostico serial bruto foi removido da `SettingsPage` e deve ser feito por ferramenta externa quando necessario.
- `stats` MQTT continuam persistidos em `DeviceRecord` e `DeviceSnapshot`.
- O dashboard HTML consome somente o DTO projetado pelo servidor; ele nao conhece `DeviceSnapshot` nem `DeviceLogEntry`.

## Atualizacao 2026-03 - Link direto do dashboard no celular

- A acao global da `DevicesPage` para acesso externo ao dashboard agora e `Copiar link do dashboard`.
- O link compartilhavel usa:
  - o host/porta de `currentState.ServerBaseAddress`;
  - a rota `/dashboard`;
  - a query `?deviceId=<idDoDeviceSelecionado>`.
- O fluxo externo nao usa `127.0.0.1` e nao usa `embedded=1`.
- O botao fica desabilitado sem device selecionado ou quando o host ainda nao for compartilhavel na LAN.
- Fora do `WebView2`, o dashboard entra em modo leitura:
  - auto-seleciona o device via `?deviceId=...`;
  - mantem telemetria e historicos;
  - oculta ou desabilita brilho e comandos;
  - mostra a nota `Controles disponiveis apenas no app desktop`.

## Referencias de codigo

- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DevicesPage WebView dashboard](../../../src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs#L1)
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [DeviceServerHost routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1)
- [DeviceServerHost dashboard](../../../src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs#L1)
- [Dashboard index](../../../src/Device.Server/wwwroot/dashboard/index.html#L1)
- [Dashboard CSS](../../../src/Device.Server/wwwroot/dashboard/dashboard.css#L1)
- [Dashboard JS](../../../src/Device.Server/wwwroot/dashboard/dashboard.js#L1)
