# Guia - Build e export de firmware

## Objetivo

Padronizar build local dos perfis de firmware e artefatos para flash manual.

## Passos

1. Abrir aba `Servidor` no app.
2. Selecionar perfil (`stable` ou `dma_exp`).
3. Rodar build/export.
4. Abrir pasta de firmware exportado e flash manual externo.

## Referencias de codigo

- [FirmwareBuildService](../../../src/App.WinUI/Services/Devices/FirmwareBuildService.cs#L7) - assinatura: `internal sealed class FirmwareBuildService`
- [IFirmwareBuildService.BuildAsync](../../../src/App.WinUI/Services/Devices/IFirmwareBuildService.cs#L7) - assinatura: `Task<FirmwareArtifactSet> BuildAsync(...)`
- [platformio stable](../../../firmware/matrixportal-s3/platformio.ini#L19) - assinatura: `[env:matrixportal_s3_stable]`
- [platformio dma_exp](../../../firmware/matrixportal-s3/platformio.ini#L27) - assinatura: `[env:matrixportal_s3_dma_exp]`
- [dev-run.ps1 param](../../../scripts/dev-run.ps1#L2) - assinatura: `param(...)`

## Checklist rapido

- Build conclui sem erro.
- Artefato esperado e gerado.
- Logs ficam salvos no app.
