# Guia - Configurar modificadores de apps

## Objetivo

Explicar como editar, salvar e aplicar modificadores dinamicos por `dispositivo + app` na aba `Apps`.

## Passos

1. Abra a aba `Apps` e selecione um app no catalogo.
2. Selecione um dispositivo online no combo `Dispositivo online`.
3. Edite os campos em `Modificadores`.
4. Clique `Salvar` para persistir localmente em `%AppData%/MicaAudio/apps/modifiers.json`.
5. Clique `Instalar` para enviar a configuracao atual junto do deploy ao dispositivo.

## Referencias de codigo

- [AppsPage.OnSaveModifiersClicked](../../../src/App.WinUI/Views/AppsPage.Deployment.cs#L1) - assinatura: `private async void OnSaveModifiersClicked(...)`
- [AppsPage.OnInstallClicked](../../../src/App.WinUI/Views/AppsPage.Deployment.cs#L1) - assinatura: `private async void OnInstallClicked(...)`
- [AppsPage.TryBuildConfigFromEditor](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1) - assinatura: `private bool TryBuildConfigFromEditor(...)`
- [AppModifierStateStore.SetDraftAsync](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L88) - assinatura: `Task SetDraftAsync(...)`
- [AppDeploymentService.SetConfigAsync](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L37) - assinatura: `Task<CommandDispatchResult> SetConfigAsync(...)`

## Checklist rapido

- [ ] Campos aparecem de acordo com o schema do app.
- [ ] `Salvar` persiste e recarrega ao voltar para o app/dispositivo.
- [ ] `Instalar` envia deploy com a configuracao atual e atualiza progresso/log.
