# Guia - Download de firmware pre-compilado

## Objetivo

Baixar no app um dos BINs pre-compilados (`stable` ou `dma_exp`) e salvar no local escolhido pelo usuario para flash manual externo.

## Passos

1. Abrir aba `Servidor` no app.
2. Clicar em `Baixar stable` ou `Baixar dma_exp`.
3. Escolher pasta/arquivo no dialogo de salvar.
4. Confirmar status `Download: concluido`.
5. Fazer flash manual com ferramenta externa.

## Referencias de codigo

- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1) - assinatura: `internal sealed class PrecompiledFirmwareService`
- [PrecompiledFirmwareService.CopyToAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1) - assinatura: `Task CopyToAsync(string optionId, string destinationPath, CancellationToken)`
- [ServerPage.SaveFirmwareAsync](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L1) - assinatura: `private async Task SaveFirmwareAsync(string optionId)`
- [ServerPage FileSavePicker](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L1) - assinatura: `private static async Task<StorageFile?> PickDestinationFileAsync(...)`

## Checklist rapido

- [ ] Botao abre dialogo de salvar.
- [ ] Nome sugerido vem correto (`matrixportal-s3-stable_merged.bin` ou `matrixportal-s3-dma_exp_merged.bin`).
- [ ] Cancelar nao gera erro.
- [ ] Arquivo salvo fica com tamanho > 0.
