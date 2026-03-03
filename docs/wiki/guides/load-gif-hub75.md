# Guia - Load GIF HUB75

## Objetivo

Descrever o fluxo ativo de GIF HUB75 nativo em `128x64`.

## Passos

1. carregar GIF por URL direta ou arquivo local
2. formatar para HUB75 `128x64`
3. reproduzir em `12 FPS`
4. enviar frame RGB565 via `StreamFrameV2` tipo `2`
5. firmware renderiza `drawFrame128x64` ou `drawBars`

## Referencias de codigo

- [Hub75FrameFormatter](../../../src/App.WinUI/Services/Gif/Hub75FrameFormatter.cs#L1)
- [GifCatalogAppRuntimeService](../../../src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs#L1)
- [StreamFrameV2](../reference/ws-protocol-v2.md)

## Checklist rapido

- GIF chega ao preview HUB75 local.
- Device recebe `Frame128x64`.
- Nao ha upscale derivado de `64x32` no fluxo ativo.
