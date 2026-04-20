# ESPConnect - viabilidade como ferramenta externa de flash e USB

## Status

`future-state / nao implementado`

Avaliado em `2026-04-20` como ferramenta externa opcional para o ecossistema do Mica, sem integracao direta no repositorio.

## Objetivo e escopo

Registrar a viabilidade do projeto [ESPConnect](https://github.com/thelastoutpostworkshop/ESPConnect) como ferramenta externa de flash manual, backup e diagnostico USB para placas ESP32 usadas no Mica.

Fica fora de escopo deste documento:

- embutir o ESPConnect no app WinUI;
- trocar o onboarding atual do Mica;
- substituir `pair code`, portal AP ou servidor do Mica;
- adicionar qualquer dependencia nova ao firmware.

## Decisao resumida

- `Sim` como ferramenta externa opcional para flash manual, console serial, backup e exploracao USB.
- `Sim` como complemento ao fluxo `Baixar firmware` + `Parear`.
- `Nao` como substituto do servidor, do `pair code`, do portal AP ou do control plane do Mica.
- `Nao` como dependencia obrigatoria do onboarding atual do app.

## O que o ESPConnect agrega

Pela documentacao upstream, o ESPConnect roda no navegador moderno ou em embalagem Electron, sem backend proprio, e oferece uma bancada USB bastante completa para ESP32.

Entre os recursos mais relevantes para o Mica:

- flash manual de arquivos `.bin`;
- erase, download e backup de flash;
- leitura de informacoes de chip, flash e particoes;
- navegacao de `SPIFFS`, `LittleFS` e `FATFS`;
- serial monitor;
- session log;
- inspetor experimental de `NVS`.

Conclusao parcial: o ESPConnect e mais proximo de uma “mesa de manutencao USB” do que de um componente de produto.

## O que o Mica ja tem hoje

O Mica ja cobre partes importantes do fluxo oficial por meios proprios:

1. Setup oficial do device por USB + AP portal, documentado em [setup-new-device](../guides/setup-new-device.md).
2. Download do firmware precompilado oficial, documentado em [build-export-firmware](../guides/build-export-firmware.md).
3. Emissao de `pair code` fora do wizard pelo botao `Parear`.
4. Dashboard e telemetria estruturada no host, documentados em [dashboard nativo de observabilidade por device](../reference/device-observability-dashboard.md).

Isso significa que o ESPConnect nao entra para “criar” um fluxo inexistente. Ele entra, no melhor caso, para melhorar a experiencia de flash/manual e diagnostico USB fora do app.

## Onde ele encaixa bem no Mica

O encaixe mais natural no ecossistema do Mica e este:

1. usuario baixa o firmware oficial pelo app Mica;
2. usuario flashea manualmente o `.bin` via ESPConnect;
3. usuario gera `pair code` no app Mica;
4. usuario conclui o AP portal do firmware com `Servidor` + `pair code`.

Esse desenho preserva as responsabilidades certas:

- ESPConnect cuida do USB/flash;
- o Mica continua dono do firmware oficial, do servidor e do pareamento;
- o firmware continua dono do AP portal e do provisioning.

## Limitadores e guard rails

Apesar de ser muito util, o ESPConnect nao deve ser confundido com parte do runtime do produto.

Limitadores importantes:

- depende de navegador Chromium com `Web Serial` no desktop;
- disputa a porta USB com qualquer outro app, incluindo monitor serial do Mica;
- nao resolve `pairing`, backend ou onboarding de produto sozinho;
- nao substitui OTA;
- nao substitui observabilidade estruturada do Mica;
- nao deve ser tratado como componente do firmware.

Em termos praticos:

- se o Mica estiver com a serial aberta, o ESPConnect nao consegue usar a mesma porta;
- se o ESPConnect estiver conectado, o wizard/monitor serial do Mica nao deve tentar capturar a mesma COM;
- a experiencia oficial do produto nao deve depender dele para funcionar.

## Inferencia central

O ESPConnect combina melhor com o futuro desenho do Mica “flash externo + pair code no app + AP portal no firmware” do que com qualquer tentativa de substituir o onboarding/provisioning do produto.

Ele e um frontend local sobre serial/flash. Nao e um sistema de backend, nao e um control plane e nao substitui o contrato operacional do Mica.

## Conclusao arquitetural

Se o projeto decidir adota-lo no futuro, a recomendacao e:

1. manter ESPConnect como ferramenta externa recomendada e opcional de bancada;
2. nao vendorizar nem embutir no WinUI nesta fase;
3. nao reescrever o onboarding atual so por causa dele;
4. documentar claramente o fluxo oficial alternativo:
   - `download firmware -> flash externo -> pair code -> AP portal`.

Em outras palavras, o ESPConnect parece excelente para reduzir atrito em flash, backup e diagnostico USB. Ele nao deve virar o centro do setup do produto.

## Checklist futuro

Antes de qualquer spike real, validar:

- [ ] se o `merged.bin` oficial do Mica funciona sem surpresa no fluxo de flash do ESPConnect;
- [ ] se o perfil `esp32s3-devkitc1` responde bem a `erase-all` + write nesse caminho;
- [ ] se a reenumeracao do `USB CDC` apos flash/reset continua previsivel;
- [ ] se os logs seriais capturados pelo ESPConnect ajudam no diagnostico de boot do Mica;
- [ ] se backup e restore de particoes podem ser usados sem contaminar cenarios de teste;
- [ ] se o `NVS Inspector` deve ficar restrito a engenharia e nunca a suporte de primeiro nivel;
- [ ] se a wiki do Mica precisa documentar conflito de porta USB quando o app local estiver com serial aberta;
- [ ] se o ESPConnect realmente reduz atrito o suficiente para ser promovido a ferramenta recomendada de bancada;
- [ ] se vale documentar um passo a passo oficial “Mica + ESPConnect” sem alterar o onboarding interno.

## Criterio de aceite para recomendacao futura

O ESPConnect so deve virar ferramenta explicitamente recomendada pelo projeto se cumprir todos os pontos abaixo:

- funcionar bem com o artefato oficial atual do Mica;
- nao exigir alteracao de firmware para flash manual basico;
- nao conflitar com o fluxo de `pair code` e AP portal;
- ter beneficio pratico claro sobre o wizard USB interno para casos de bancada;
- poder ser apresentado como opcional, sem fragilizar o caminho oficial do produto.

## Referencias

### Baseline atual do Mica

- [Setup de novo dispositivo](../guides/setup-new-device.md)
- [Download de firmware pre-compilado](../guides/build-export-firmware.md)
- [Dashboard nativo de observabilidade por device](../reference/device-observability-dashboard.md)

### ESPConnect

- [ESPConnect README](https://github.com/thelastoutpostworkshop/ESPConnect/blob/main/README.md)
- [ESPConnect latest release](https://github.com/thelastoutpostworkshop/ESPConnect/releases/latest)
- [ESPConnect web app](https://thelastoutpostworkshop.github.io/ESPConnect/)

### Fontes oficiais

- [esptool - Basic Commands](https://docs.espressif.com/projects/esptool/en/latest/esp32/esptool/basic-commands.html)
- [esptool - Advanced Commands](https://docs.espressif.com/projects/esptool/en/latest/esp32/esptool/advanced-commands.html)
- [Chrome for Developers - Web Serial API](https://developer.chrome.com/articles/serial)
