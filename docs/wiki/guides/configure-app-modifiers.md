# Guia - Configurar modificadores de apps

## Objetivo

Explicar como editar, salvar e aplicar modificadores dinamicos por `dispositivo + app` na aba `Apps`.

## Passos

1. Abra a aba `Apps` e selecione um app no catalogo.
2. Selecione um dispositivo online no combo `Dispositivo online`.
3. Edite os campos em `Modificadores`.
4. Clique `Salvar` para persistir localmente em `%AppData%/MicaAudio/apps/modifiers.json`.
5. Clique `Aplicar` para enviar `set_app_config` ao dispositivo.
6. Clique `Restaurar` para limpar o draft salvo desse `deviceId+appId`.

## Referencias de codigo

- [AppsPage.OnSaveModifiersClicked](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L470) - assinatura: `private async void OnSaveModifiersClicked(...)`
- [AppsPage.OnApplyModifiersClicked](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L494) - assinatura: `private async void OnApplyModifiersClicked(...)`
- [AppsPage.TryBuildConfigFromEditor](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L706) - assinatura: `private bool TryBuildConfigFromEditor(...)`
- [AppModifierStateStore.SetDraftAsync](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L88) - assinatura: `Task SetDraftAsync(...)`
- [AppDeploymentService.SetConfigAsync](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L37) - assinatura: `Task<CommandDispatchResult> SetConfigAsync(...)`

## Checklist rapido

- [ ] Campos aparecem de acordo com o schema do app.
- [ ] `Salvar` persiste e recarrega ao voltar para o app/dispositivo.
- [ ] `Aplicar` envia comando tracked e atualiza progresso/log.
- [ ] `Restaurar` limpa o draft e volta para defaults.
