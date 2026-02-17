# Guia - Falhas de download e salvamento local de firmware

## Objetivo

Diagnosticar falhas no novo fluxo de firmware pre-compilado (sem OTA), quando o app nao consegue localizar ou salvar o BIN.

## Passos

1. Confirmar que os assets existem em `src/App.WinUI/AppData/Firmware/`.
2. Validar publish/output contem os BINs esperados.
3. Na aba `Servidor`, tentar `Baixar stable` e observar status/log.
4. Se falhar antes do dialogo, validar `TryResolveSource`.
5. Se falhar apos dialogo, validar permissao/caminho escolhido.
6. Repetir salvando em `Downloads` como teste base.

## Referencias de codigo

- [PrecompiledFirmwareService.TryResolveSource](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1) - assinatura: `bool TryResolveSource(...)`
- [PrecompiledFirmwareService.CopyToAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1) - assinatura: `Task CopyToAsync(...)`
- [ServerPage.SaveFirmwareAsync](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L1) - assinatura: `private async Task SaveFirmwareAsync(string optionId)`
- [ServerPage.PickDestinationFileAsync](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L1) - assinatura: `private static async Task<StorageFile?> PickDestinationFileAsync(...)`

## Checklist rapido

- [ ] `TryResolveSource` encontra arquivo para `stable` e `dma_exp`.
- [ ] Cancelamento do FileSavePicker nao quebra estado da UI.
- [ ] Erro de escrita aparece em log com mensagem clara.
- [ ] Salvamento bem sucedido atualiza status para `Download: concluido`.
