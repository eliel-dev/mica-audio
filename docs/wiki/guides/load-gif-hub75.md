# Guia - Load GIF HUB75

## Objetivo

Descrever o fluxo ativo de GIF HUB75 no modelo atual, onde o GIF entra como widget `gifhub75` dentro de `Paineis`.

## Passos

1. Abra o editor de um painel em `Paineis`.
2. Adicione um widget `gifhub75` ao canvas.
3. Selecione o widget e defina a fonte do GIF no inspetor.
4. Salve o painel e ative-o para um device.
5. O runtime desktop compoe o frame `128x64` final e o envia ao ESP32.

## Referencias de codigo

- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsFrameComposer](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [Hub75FrameFormatter](../../../src/App.WinUI/Services/Gif/Hub75FrameFormatter.cs#L1)
- [StreamFrameV2](../reference/ws-protocol-v2.md)

## Checklist rapido

- [ ] O widget GIF aceita arquivo ou pasta.
- [ ] O preview do painel mostra o poster frame ou a animacao quando ativo.
- [ ] O device recebe `Frame128x64`.
- [ ] Nao existe mais dependencia da antiga sessao `Apps` para executar GIF HUB75.
