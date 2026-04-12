# Guia - Load GIF HUB75

## Objetivo

Descrever o fluxo ativo de GIF HUB75 no modelo atual, onde o GIF entra como widget `gifhub75` dentro de `Paineis`.

## Premissas Do V1

- A entrada preferencial e imagem/GIF ja preformatado externamente para `128x64`.
- O compositor desktop continua sendo a origem do `Frame128x64` enviado ao ESP32.
- O playback usa `30 Hz` como teto de apresentacao.
- GIF animado respeita os delays reais do arquivo; o loop nao usa mais um indice global fixo de tick.
- Sob carga, o pipeline prioriza o frame mais novo (`newest-wins`) na fila WebSocket do device.

## Passos

1. Abra o editor de um painel em `Paineis`.
2. Adicione um widget `gifhub75` ao canvas.
3. Selecione o widget e defina a fonte do GIF no inspetor.
4. Prefira GIF/imagem ja tratado para `128x64` antes de salvar o painel.
5. Salve o painel e ative-o para um device.
6. O runtime desktop compoe o frame `128x64` final, resolve o frame ativo por timeline da midia e o envia ao ESP32.

## Referencias de codigo

- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsFrameComposer](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [Hub75FrameFormatter](../../../src/App.WinUI/Services/Gif/Hub75FrameFormatter.cs#L1)
- [StreamFrameV2](../reference/ws-protocol-v2.md)

## Checklist rapido

- [ ] O widget GIF aceita arquivo ou pasta.
- [ ] O preview do painel mostra o poster frame ou a animacao quando ativo.
- [ ] GIF animado respeita os delays reais do arquivo.
- [ ] O runtime apresenta ate `30 FPS`.
- [ ] O device recebe `Frame128x64`.
- [ ] Nao existe mais dependencia da antiga sessao `Apps` para executar GIF HUB75.
