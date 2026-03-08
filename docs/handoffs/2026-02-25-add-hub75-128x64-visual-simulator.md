# Handoff — Simulador HUB75 128x64 ao lado do 64x32

## Objetivo

Adicionar no Visualizador um segundo preview HUB75 128x64 ao lado do preview 64x32, sem alterar pipeline, protocolo ou firmware.

## Escopo classificado

Estrutural (alteracao em `src/` + docs + handoff).

## Arquivos alterados

- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/handoffs/2026-02-25-add-hub75-128x64-visual-simulator.md`

## Decisoes tomadas

1. O preview 128x64 e simulacao local derivada do frame 64x32 existente.
2. O mapeamento e nearest-neighbor 2x (`x128/2`, `y128/2`) para preservar pixel art HUB75.
3. A visibilidade do 128x64 segue o mesmo toggle do HUB75 atual (`ShouldShowHubPreview()`).
4. A invalidação dos canvases foi centralizada em `InvalidateHubPreviews()`.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` (OK)
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` (OK)
- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug` (OK, sem erros)

## Riscos e rollback

- Risco baixo: mudanca isolada ao preview do Visualizador.
- Sem impacto em contrato wire, DSP ou output real.
- Rollback simples: reverter apenas `MainPage.xaml` e `MainPage.xaml.cs` para layout/canvas unico.

## Proximos passos

1. Validar manualmente alternancia de `Modo HUB75` (mostrar/ocultar os dois previews juntos).
2. Verificar sincronia visual em audio e modo GIF.
3. Se aprovado, avaliar etapa futura para preview 128x64 nativo desacoplado do snapshot 64x32.