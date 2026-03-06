# Referencia - Device Telemetry v2 Fields

## Objetivo

Definir o contrato de telemetria v2 entre firmware, protocolo, servidor e App.WinUI, incluindo regras de sanitizacao e consumo na `DevicesPage`.

## Campos do payload de telemetria WS

| Campo | Tipo | Semantica |
| --- | --- | --- |
| `uptimeSeconds` | `int?` | uptime do firmware em segundos |
| `loopLoadPercent` | `int?` | carga aproximada do loop principal (0..100 no emissor) |
| `freeHeapBytes` | `long?` | heap livre em bytes |
| `largestHeapBlockBytes` | `long?` | maior bloco contiguo de heap livre |
| `psramAvailable` | `bool?` | indica se o build/device possui PSRAM utilizavel |
| `freePsramBytes` | `long?` | psram livre em bytes |
| `largestPsramBlockBytes` | `long?` | maior bloco contiguo de psram livre |
| `wifiConnected` | `bool?` | estado de conectividade Wi-Fi reportado pelo firmware |
| `wifiState` | `string?` | estado canonico de conectividade (`connecting|connected|portal|disconnected`) |
| `provisioningPortalActive` | `bool?` | indica se o portal de provisioning esta ativo no firmware |
| `auxLedAvailable` | `bool?` | disponibilidade do LED auxiliar no hardware/build atual |
| `testLedAvailable` | `bool?` | disponibilidade efetiva de LED de teste (onboard e/ou auxiliar) |
| `lastWifiEvent` | `string?` | ultimo evento curto de conectividade reportado pelo firmware |
| `telemetrySequence` | `uint?` | contador monotono de heartbeats de telemetria por sessao |
| `brightnessCap` | `int?` | limite de brilho aplicado pelo device (`30..160`) |
| `brightnessRequested` | `int?` | brilho solicitado pelo stream/comando antes do cap |
| `brightnessApplied` | `int?` | brilho efetivamente aplicado no painel |
| `testLedEnabled` | `bool?` | estado legado/compatibilidade do LED auxiliar continuo |
| `testLedDuty` | `int?` | duty atual do LED auxiliar em escala 8-bit (quando aplicavel) |

## Regras de sanitizacao e pass-through

1. Sanitizacao de `largestHeapBlockBytes` e `largestPsramBlockBytes` ocorre apenas no firmware emissor.
2. O servidor deve tratar os campos v2 em pass-through (sem clamp/renormalizacao).
3. Campos permanecem `nullable` para compatibilidade com firmware legado.
4. `wifiState` usa valores canonicos em minusculo para facilitar consumo no dashboard.

## Persistencia local

- `DeviceSnapshot` e `DeviceRecord` carregam os campos v2 para uso em UI e store.
- `JsonDeviceRegistryStore` faz round-trip desses campos no `devices.json`.
- Em offline, a UI pode mostrar o ultimo snapshot conhecido sem depender de nova telemetria.

## Consumo na DevicesPage (Entrega 3)

- A `DevicesPage` usa `DeviceMetricsFormatter` para converter snapshot bruto em apresentacao.
- O card principal da `DevicesPage` exibe status, carga do loop, uptime, heap, PSRAM e rede em painel seguro baseado em texto, barras e grade simples.
- Barras derivadas de fragmentacao sao exibidas apenas quando os dados sao coerentes.
- `RSSI` deve aparecer apenas quando o `snapshot.Status` esta online; para offline a UI exibe estado de rede sem sinal numerico.
- O card de logs usa `GetDeviceLogs(deviceId)` e exibe somente o device selecionado.
- Sem selecao, dashboard e logs exibem placeholders dedicados.
- O dashboard exibe heartbeat (`telemetrySequence`) e estado de LED de teste sem depender de log textual.
- O slider de brilho usa `brightnessCap` e o card mostra `brightnessApplied/brightnessCap`.
- O dashboard tambem mostra:
  - estado canonico de Wi-Fi;
  - portal ativo/inativo;
  - idade do ultimo heartbeat;
  - ultimo evento curto de conectividade por dispositivo;
  - contadores de stream em tabela textual segura.

## Checklist rapido de validacao

- Device online atualiza o painel seguro com status `Online`.
- Device offline exibe aviso `Offline (ultimo snapshot)` e mantem dados do ultimo snapshot.
- Sem selecao, o dashboard mostra placeholder de selecao.
- Logs trocam junto com a selecao do device e nao misturam eventos de outros devices.

## Referencias de codigo

- [firmware telemetry sender](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [DeviceTelemetryMessage](../../../src/Device.Protocol/Models/DeviceTelemetryMessage.cs#L1)
- [DeviceSnapshot](../../../src/Device.Protocol/Models/DeviceSnapshot.cs#L1)
- [DeviceRecord](../../../src/Device.Protocol/Models/DeviceRecord.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceMetricsFormatter](../../../src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs#L1)
- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
