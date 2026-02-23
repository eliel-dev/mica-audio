# Handoff - GIF HUB75 URL/arquivo MVP

## Objetivo

Implementar modo GIF no app com carga por URL direta/arquivo local, formatacao HUB75 64x32, playback fixo 12 FPS, envio para preview local + device e suporte de protocolo/firmware RGB565.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - `messageType=2` (RGB565) funcional no app e firmware.
  - Modo `Audio | GIF` com pausa/retomada automatica do pipeline de audio.
  - Carga GIF por URL e arquivo local com limites MVP.
  - Build e testes obrigatorios verdes.

## Arquivos alterados

- `src/Device.Protocol/Stream/StreamFrameV1.cs`
- `src/Output/Led/MatrixPortalLedOutput.cs`
- `src/App.WinUI/Services/AudioPipelineCoordinator.cs`
- `src/App.WinUI/Services/Gif/GifContentSourceMode.cs`
- `src/App.WinUI/Services/Gif/GifScaleMode.cs`
- `src/App.WinUI/Services/Gif/Hub75GifDecoder.cs`
- `src/App.WinUI/Services/Gif/Hub75FrameFormatter.cs`
- `src/App.WinUI/Services/Gif/Hub75GifPlayer.cs`
- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `firmware/matrixportal-s3/src/main.cpp`
- `tests/Output.Tests/StreamFrameV1Tests.cs`
- `tests/Output.Tests/MatrixPortalLedOutputTests.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/Hub75GifServicesTests.cs`
- `docs/wiki/reference/ws-protocol-v1.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/modules/firmware-matrixportal-s3.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/guides/load-gif-hub75.md`

## Decisoes tomadas

1. Mantido protocolo legado (`messageType=1`) e adicionada via paralela (`messageType=2`) para nao quebrar firmware antigo.
2. Player GIF fixado em 12 FPS para estabilidade no HUB75, ignorando timing original do GIF no MVP.
3. Preview local no modo GIF permanece ativo independentemente do toggle de preview de audio.
4. Limites de seguranca no carregamento por URL: `http/https`, timeout `10s`, max `25MB`.
5. Sem cache persistente: GIF fica apenas em memoria da sessao.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test tests\Output.Tests\Output.Tests.csproj -c Debug -> OK (20 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

## Riscos e rollback

- Risco principal:
  - `System.Drawing` no decode GIF e warnings de plataforma fora de Windows.
  - Firmware antigo sem suporte a `messageType=2` exibira apenas fallback local no app.
- Como reverter:
  - Reverter os arquivos de protocolo/output/firmware para remover `messageType=2`.
  - Manter `MainPage` em modo audio-only removendo painel GIF e serviços novos.

## Proximos passos

1. Adicionar handshake/capability formal para detectar suporte `messageType=2` no firmware.
2. Considerar decoder sem `System.Drawing` (WIC/ImageSharp) para reduzir dependencia de plataforma.
3. Expandir testes de decode com fixture GIF animado multi-frame e cenarios de limite.
