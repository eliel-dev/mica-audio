# Guia - Debug quando visualizacao nao aparece

## Objetivo

Identificar rapidamente por que a tela do visualizador fica parada/preta e restaurar o fluxo de render sem reiniciar o projeto.

## Passos

1. Verificar se `MainPage` retomou sessao apos navegacao entre abas.
2. Verificar status de captura/pipeline no texto de status da UI.
3. Confirmar que existe frame recente no coordinator/pipeline.
4. Conferir `crash.log` em `%LocalAppData%\MicaAudio\crash.log`.
5. Validar captura loopback e reagir a troca de device.

## Referencias de codigo

- [MainPage.OnLoaded](../../../src/App.WinUI/Views/MainPage.xaml.cs#L140) - assinatura: retomada de sessao
- [MainPage.OnUnloaded](../../../src/App.WinUI/Views/MainPage.xaml.cs#L37) - assinatura: pausa de timers/salvamento
- [AudioPipelineCoordinator.StartAsync](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L43) - assinatura: start captura/loop
- [AudioPipelineCoordinator.PipelineLoopAsync](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L74) - assinatura: leitura do canal e envio de frame
- [WasapiLoopbackCaptureService.StartAsync](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L36) - assinatura: start loopback

## Checklist rapido

- [ ] Alternar abas e voltar para Visualizador.
- [ ] Testar F11/ESC.
- [ ] Conferir se stream continua indo para output remoto.
