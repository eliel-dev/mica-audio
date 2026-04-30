# Guia - Falhas de download e salvamento local de firmware

## Objetivo

Diagnosticar falhas no fluxo de firmware pre-compilado na aba `Dispositivos`, quando o app nao consegue localizar ou salvar o BIN.

## Passos

1. Confirmar que os assets existem em `src/App.WinUI/AppData/Firmware/`.
2. Validar se a combinacao `placa + painel + perfil` tem artefato embarcado.
3. Na aba `Dispositivos`, testar `Baixar firmware` diretamente na barra superior.
4. Se falhar antes do dialogo, validar `TryResolveSource` e metadados do `PrecompiledFirmwareOption`.
5. Se falhar apos dialogo, validar permissao/caminho escolhido.
6. Repetir salvando em `Downloads` como teste base.

## Referencias de codigo

- [PrecompiledFirmwareService.TryResolveSource](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L105) - assinatura: `bool TryResolveSource(...)`
- [PrecompiledFirmwareService.CopyToAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L144) - assinatura: `Task CopyToAsync(...)`
- [DevicesPage.SaveFirmwareAsync](../../../src/App.WinUI/Views/DevicesPage.Onboarding.cs#L1) - assinatura: `private async Task SaveFirmwareAsync()`
- [DevicesPage.PickFirmwareDestinationFileAsync](../../../src/App.WinUI/Views/DevicesPage.Onboarding.cs#L1) - assinatura: `private static async Task<StorageFile?> PickFirmwareDestinationFileAsync(...)`

## Checklist rapido

- [ ] O release oficial e resolvido sem depender do wizard USB.
- [ ] Cancelamento do `FileSavePicker` nao quebra estado da UI.
- [ ] Erro de escrita aparece em log com mensagem clara.
- [ ] Salvamento bem sucedido atualiza status para `Download: concluido`.
