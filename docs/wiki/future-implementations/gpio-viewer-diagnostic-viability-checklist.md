# GPIOViewer - viabilidade como diagnostico local opcional

## Status

`future-state / nao implementado`

Avaliado em `2026-04-20` apenas como possibilidade futura para o firmware oficial `esp32s3-devkitc1`.

## Objetivo e escopo

Registrar a viabilidade do projeto [GPIOViewer](https://github.com/thelastoutpostworkshop/gpio_viewer) como superficie opcional de diagnostico local para bancada.

Fica fora de escopo deste documento:

- implementar qualquer dependencia nova no firmware;
- trocar o dashboard nativo do Mica;
- substituir o portal AP de provisioning;
- transformar o GPIOViewer em requisito do firmware oficial.

## Decisao resumida

- `Sim` como experimento de laboratorio para inspecao local de GPIO, heap/PSRAM basicos e metadados de board.
- `Nao` como substituto da telemetria estruturada do Mica.
- `Nao` como base do fluxo oficial de provisioning/AP.
- `Nao` como dependencia sempre ativa do firmware de producao.

## O que o GPIOViewer agrega

O GPIOViewer e atraente como ferramenta de bancada porque entrega uma web UI pronta para:

- atividade de GPIO em tempo real;
- leitura basica de heap e PSRAM;
- informacoes de chip, flash e board;
- descoberta por IP local e mDNS;
- inspeccao rapida sem depender do app desktop do Mica.

Na pratica, isso o posiciona mais como um osciloscopio/logico leve de firmware do que como um sistema de observabilidade completo.

## O que o Mica ja tem hoje

O Mica ja possui duas camadas de observabilidade que cobrem parte importante do problema:

1. Dashboard por device servido pelo host local e mostrado no `WebView2`, documentado em [dashboard nativo de observabilidade por device](../reference/device-observability-dashboard.md).
2. Telemetria estruturada e persistida no host, documentada em [Firmware HUB75 (DevKitC-1)](../modules/firmware-esp32s3-devkitc1.md) e [Device.Server + Device.Protocol](../modules/device-server-protocol.md).

O baseline atual do projeto ja expoe, entre outros:

- `loopHealthyPercent`;
- `chipTemperatureCelsius`;
- heap livre, heap total e maior bloco;
- PSRAM livre, total e maior bloco;
- `hub75Fps`;
- contadores de stream;
- `resetReason`;
- estado e profundidade do `control worker` e do `panels worker`;
- tempos maximos de decode/present no caminho de `Paineis`.

Conclusao parcial: o GPIOViewer nao resolve uma ausencia total de metricas. Ele adiciona principalmente uma superficie local pronta para diagnostico de pinos e estado bruto da placa.

## Fatores de viabilidade

### Fatores favoraveis

- O projeto e focado em ESP32 com Arduino, o mesmo universo geral do firmware atual do Mica.
- O README do GPIOViewer o descreve como ferramenta de observacao ao vivo de GPIO e informacoes da placa, o que combina com uso de bancada.
- O projeto upstream declara suporte amplo a boards ESP32 e, no momento desta avaliacao, usa release `v1.7.1`.

### Limitadores estruturais

O GPIOViewer nao entra como dependencia neutra. Pela documentacao e pelo header principal, ele:

- depende de `ESP Async WebServer` e `Async TCP`;
- abre servidor HTTP proprio;
- abre `mDNS`;
- abre um canal proprio de eventos server-side;
- cria task propria de monitoramento;
- exige `WIFI_STA`;
- nao suporta `WIFI_AP` nem `WIFI_AP_STA`;
- adiciona cerca de `50 KB` ao projeto;
- carrega assets web a partir de GitHub Pages.

Esses pontos colidem com a arquitetura atual do Mica em varios niveis:

- o provisioning oficial do Mica usa AP portal e depende de coexistencia com modo AP;
- o firmware atual nao usa `ESPAsyncWebServer` como stack oficial;
- o runtime ja e apertado em heap interna/DMA por causa de `HUB75`, `Wi-Fi`, batches `WebP` e workers;
- o projeto ja tem telemetria estruturada propria e nao precisa terceirizar isso para uma UI externa sempre ativa.

## Inferencia central sobre Wi-Fi e runtime

As fontes oficiais da Espressif para `ESP32-S3` documentam suporte a `STA`, `AP` e `station/AP-coexistence mode`, alem de provisioning oficial por `SoftAP + HTTP`.

Por outro lado, o GPIOViewer declara explicitamente que trabalha apenas com `WIFI_STA` e nao suporta `WIFI_AP` nem `WIFI_AP_STA`.

Inferencia arquitetural:

- para o Mica, essa restricao inviabiliza o GPIOViewer como runtime padrao;
- no melhor caso, ele so cabe como modo de debug separado, acionado em device ja provisionado e fora do fluxo oficial de provisioning.

## Conclusao arquitetural

Se o projeto resolver experimentar GPIOViewer no futuro, a integracao recomendada e:

1. build ou profile experimental separado do firmware oficial;
2. ativacao apenas em device ja provisionado e operando em `STA-only`;
3. recurso desligado por default;
4. recurso ausente do caminho oficial de `AP portal`, pairing e recover de Wi-Fi;
5. sem substituir `stats`, `logs`, `status` e o dashboard nativo do Mica.

Em outras palavras, o GPIOViewer pode ser util como ferramenta de engenharia. Ele nao deve virar o centro da observabilidade do produto.

## Checklist futuro

Antes de qualquer spike real, validar:

- [ ] se `ESP Async WebServer` e `Async TCP` convivem sem regressao com o firmware atual;
- [ ] se o custo extra de heap, PSRAM e task nao reabre problemas de `ESP_ERR_NO_MEM` no boot;
- [ ] se o impacto no loop do `HUB75`, `present`, `hub75Fps` e batches `WebP` e aceitavel;
- [ ] se a feature pode ser isolada em profile de laboratorio sem contaminar o firmware oficial;
- [ ] se o modo `STA-only` e suficiente para o caso de uso de bancada;
- [ ] se a convivencia com `WiFiManager` e com o AP portal atual e explicitamente bloqueada no profile de debug;
- [ ] se a dependencia de assets em GitHub Pages e aceitavel;
- [ ] se os assets precisam ser self-hosted ou mirrored antes de qualquer uso fora da bancada;
- [ ] se a UI do GPIOViewer realmente agrega algo que o dashboard nativo do Mica nao cobre;
- [ ] se o criterio de aceite do experimento esta fechado.

## Criterio de aceite para um futuro modo debug oficial

Um modo debug baseado em GPIOViewer so deve ser considerado oficial se cumprir todos os pontos abaixo:

- nao alterar o comportamento do firmware oficial por default;
- nao participar do fluxo de provisioning;
- nao reduzir a estabilidade do `Wi-Fi` e do `HUB75`;
- nao substituir a telemetria estruturada do Mica;
- poder ser removido ou desligado sem impacto em `status`, `stats`, `logs` e `Paineis`.

## Referencias

### Baseline atual do Mica

- [Dashboard nativo de observabilidade por device](../reference/device-observability-dashboard.md)
- [Firmware HUB75 (DevKitC-1)](../modules/firmware-esp32s3-devkitc1.md)
- [Device.Server + Device.Protocol](../modules/device-server-protocol.md)

### GPIOViewer

- [GPIOViewer README](https://github.com/thelastoutpostworkshop/gpio_viewer/blob/main/README.md)
- [GPIOViewer latest release](https://github.com/thelastoutpostworkshop/gpio_viewer/releases/latest)
- [GPIOViewer library.properties](https://raw.githubusercontent.com/thelastoutpostworkshop/gpio_viewer/main/library.properties)
- [GPIOViewer header principal](https://raw.githubusercontent.com/thelastoutpostworkshop/gpio_viewer/main/src/gpio_viewer.h)

### Fontes oficiais Espressif

- [ESP-IDF v5.5.4 - Wi-Fi Driver (ESP32-S3)](https://docs.espressif.com/projects/esp-idf/en/v5.5.4/esp32s3/api-guides/wifi.html)
- [ESP-IDF - Wi-Fi Provisioning (SoftAP + HTTP)](https://docs.espressif.com/projects/esp-idf/en/v5.0.3/esp32s3/api-reference/provisioning/wifi_provisioning.html)
- [ESP-IDF - Support for External RAM / restrictions](https://docs.espressif.com/projects/esp-idf/en/v5.4.1/esp32s3/api-guides/external-ram.html)
