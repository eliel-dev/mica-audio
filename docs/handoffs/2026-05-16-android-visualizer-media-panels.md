# Handoff - Android visualizer, media cache, and panel editor

## Objetivo

Corrigir o preview Android do visualizador, remover dependencia de modo HUB75 para exibir o canvas, acelerar a emissao visual, cachear midias do servidor e aproximar o editor de paineis da interacao de launchers.

## Escopo classificado

- Tipo: funcional em `src/`, com alteracao aditiva de API admin (`GET media/{mediaId}`) e handoff exigido pela governanca local.
- Contrato wire HUB75: sem mudanca em `StreamFrameV2`; Android continua enviando `Bins128` com `flags=0x11`.

## Arquivos alterados

- `src/App.Android/app/src/main/java/com/micaaudio/android/data/audio/AudioCaptureService.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/audio/AudioProcessor.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/settings/AppSettings.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/ui/screens/visualizer/VisualizerScreen.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/ui/screens/visualizer/VisualizerViewModel.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/api/MicaServerApi.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/repository/PanelRepository.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/ui/screens/panels/PanelsViewModel.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/ui/screens/panels/PanelEditorScreen.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/ui/screens/panels/WidgetConfigScreen.kt`
- `src/Device.Server/Hosting/DeviceServerHost.MediaStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`

## Decisoes tomadas

1. O Android foi mantido em `Visualizer(0)` + `RECORD_AUDIO`, sem `MediaProjection`, para remover o prompt de compartilhamento de tela.
2. A FFT do Android atualiza alvos conforme o callback do `Visualizer`; a publicacao/preview agora rodam em loop de `60 FPS`, reaproveitando buffers e evitando coroutine por frame capturado.
3. O canvas local fica sempre renderizado e usa barras centradas, linha central sutil e queda por gravidade no estilo NextGenVisualizer/Windows.
4. `visualizerMaxFreq` e `visualizerFftSmoothing` viraram settings persistidos e ajustaveis pela tela de FFT.
5. O servidor ganhou `GET /api/v1/admin/devices/{deviceId}/media/{mediaId}` para o Android cachear arquivos de midia e exibir grade de thumbs no widget GIF.
6. O editor Android entra em landscape, move widgets por long press e redimensiona por oito alcas.
7. O WinUI passa a importar o catalogo remoto no carregamento para enxergar paineis criados no Android.

## Validacoes executadas

- `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter FullyQualifiedName~DeviceServerHostAdminApiTests --no-restore` -> PASS.
- `dotnet build MicaAudio.sln -c Debug --no-restore -m:1` -> PASS; warnings NU1902 existentes e CA1869 em WinUI.
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> PASS.
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> PASS com `XDG_CONFIG_HOME` temporario para evitar warning de permissao do Git global.
- Android Gradle nao executado neste ambiente: o wrapper tentou baixar `gradle-9.4.1-bin.zip`, a rede do sandbox bloqueou, e a solicitacao de escalacao foi recusada automaticamente pelo ambiente.

## Riscos e rollback

- `Visualizer(0)` evita MediaProjection, mas depende da compatibilidade do OEM/Android para capturar mix global; alguns aparelhos podem limitar a fonte ou taxa de captura.
- A grade de midia cacheia arquivos por device; paineis ainda precisam de um device alvo para upload de midia.
- Rollback: remover a rota GET de midia, voltar `WidgetConfigScreen` para lista textual, remover o merge remoto no `PanelsPage` e restaurar o processamento direto no callback de FFT.

## Proximos passos

1. Rodar `:app:testDebugUnitTest` e `:app:assembleDebug` em ambiente com Gradle/Android SDK ja cacheado ou com rede liberada.
2. Validar em device fisico se `Visualizer(0)` captura a fonte desejada sem prompt de tela no aparelho alvo.
3. Ajustar o cache de midia para escopo global caso o servidor passe a expor midia independente de device.
