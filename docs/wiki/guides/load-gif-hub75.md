# Carregar GIF HUB75 na Loja (AppsPage)

Guia MVP para executar GIF como app de catalogo (`gifhub75`) com preview local e stream para device HUB75.

## Objetivo

Permitir carregar GIF por URL direta ou arquivo local no `AppsPage`, formatar para HUB75 `64x32` e reproduzir em `12 FPS` fixos.

## Regras do MVP

- Fonte remota: apenas URL direta `http/https`.
- Fonte local: arquivo `.gif` da sessao atual (sem persistencia de caminho).
- Limites: timeout `10s`, download maximo `25MB`, decode maximo `720` frames.
- Escala: `Fit`, `Fill` ou `Stretch` (modificador do app).
- Inicio automatico do runtime: apenas em selecao manual (clique no card `gifhub75`).
- Saida: preview local + broadcast para devices online.

## Passos

1. Abrir `AppsPage` e selecionar manualmente o card `GIF HUB75`.
2. Definir `sourceMode`:
   - `url`: preencher `gifUrl` com URL direta GIF.
   - `file`: usar o botao `Abrir arquivo GIF` no painel runtime.
3. Definir `scaleMode` (`fit`, `fill` ou `stretch`).
4. Clicar em `Salvar` para persistir modificadores e reaplicar runtime quando o app GIF estiver selecionado.
5. Trocar para outro app para parar o runtime GIF.

## Checklist rapido

- Card `gifhub75` visivel no catalogo.
- Selecao manual do card inicia runtime quando configuracao estiver valida.
- `sourceMode=url` valida `http/https`.
- `sourceMode=file` inicia apos escolher arquivo da sessao.
- Preview local HUB75 atualizado no painel runtime.
- Device recebendo `messageType=2`.
- Ao parar runtime/trocar app, app envia fallback tipo `1` zerado.

## Compatibilidade

- Firmware antigo ignora `messageType=2`.
- O AppsPage exibe aviso e o runtime local continua funcional.

## Pipeline tecnico

1. `GifCatalogAppRuntimeService` adquire bytes por URL/arquivo.
2. `Hub75GifDecoder` valida assinatura GIF e decodifica frames.
3. `Hub75FrameFormatter` converte para `RgbaColor[64*32]`.
4. `Hub75GifPlayer` publica frames a cada `83ms` (12 FPS).
5. `MatrixPortalLedOutput` serializa RGB565 (`StreamFrameV1` tipo `2`) e faz broadcast.
6. `Stop()` do runtime envia tipo `1` zerado para retorno imediato ao modo barras.
7. Firmware renderiza `drawFrame64x32` (tipo `2`) ou `drawBars` (tipo `1`).

## Referencias de codigo

- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L1)
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1)
- [Hub75GifDecoder](../../../src/App.WinUI/Services/Gif/Hub75GifDecoder.cs#L1)
- [Hub75FrameFormatter](../../../src/App.WinUI/Services/Gif/Hub75FrameFormatter.cs#L1)
- [Hub75GifPlayer](../../../src/App.WinUI/Services/Gif/Hub75GifPlayer.cs#L1)
- [StreamFrameV1](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L1)
- [MatrixPortalLedOutput](../../../src/Output/Led/MatrixPortalLedOutput.cs#L1)
- [Firmware main.cpp](../../../firmware/matrixportal-s3/src/main.cpp#L1)
