# Handoff - Android AudioMotion Clone parity

## Objetivo

Alinhar o visualizador Android ao pipeline calibrado do Windows para o modo `AudioMotion Clone`.

## Escopo classificado

- Tipo: funcional com handoff de governanca exigido pelo gate local para alteracoes em `src/`.
- Criterio de aceite: Android captura audio local, gera os mesmos `128` bins enviados ao device, publica `StreamFrameV2` tipo `Bins128` com `flags=0x11` e mostra preview fixo com linhas espelhadas/rainbow.

## Arquivos alterados

- `src/App.Android/app/src/main/java/com/micaaudio/android/data/audio/AudioMotionCloneAnalyzer.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/audio/VisualFrameRateLimiter.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/audio/AudioCaptureService.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/websocket/VisualStreamFrameEncoder.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/data/websocket/VisualStreamSocket.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/ui/screens/visualizer/VisualizerScreen.kt`
- `src/App.Android/app/src/main/java/com/micaaudio/android/ui/screens/visualizer/VisualizerViewModel.kt`
- `src/App.Android/app/src/test/java/com/micaaudio/android/data/audio/AudioMotionCloneAnalyzerTest.kt`
- `src/App.Android/app/src/test/java/com/micaaudio/android/data/audio/VisualFrameRateLimiterTest.kt`
- `src/App.Android/app/src/test/java/com/micaaudio/android/data/websocket/VisualStreamFrameEncoderTest.kt`
- `src/App.Android/app/build.gradle.kts`
- `src/App.Android/gradle/libs.versions.toml`
- `src/App.Android/README.md`
- `docs/wiki/reference/code-index.md`
- `docs/handoffs/2026-05-14-android-audiomotion-clone-parity.md`

## Decisoes tomadas

1. O Android ficou com modo fixo `AudioMotion Clone`, sem presets/paletas locais, para evitar drift visual em relacao ao Windows.
2. O analisador Android porta os defaults calibrados do Windows (`48 kHz`, FFT `2048`, hop `256`, Bark, weighting B, linear boost e envelope) sem venderizar `NextGenVisualizer`.
3. O socket Android passou a montar o payload por `VisualStreamFrameEncoder`, com timestamp monotonic e `flags=0x11` (`MirrorLines` + `Rainbow`) no contrato `StreamFrameV2` existente.
4. A publicacao Android foi limitada a `60 FPS` e o socket descarta frames quando ha backlog, priorizando tempo real em vez de enfileirar atraso.
5. O prompt de "compartilhar tela" nao foi removido porque o Android exige consentimento de `MediaProjection` para `AudioPlaybackCapture`; o app nao cria `VirtualDisplay` nem envia video.

## Validacoes executadas

```text
cd src/App.Android; gradle :app:testDebugUnitTest :app:assembleDebug --no-daemon -> PASS
cd src/App.Android; gradle :app:testDebugUnitTest --tests com.micaaudio.android.data.audio.VisualFrameRateLimiterTest --no-daemon -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> PASS
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> PASS apos criacao deste handoff
dotnet build MicaAudio.sln -c Debug -> PASS no retry isolado; warnings NU1902 existentes em pacotes OpenTelemetry
```

## Riscos e rollback

- Risco principal: pequenas diferencas perceptuais podem permanecer por limitacoes do `AudioPlaybackCapture` Android e pela diferenca de fonte/dispositivo de audio.
- Como reverter: restaurar o hot path anterior em `AudioCaptureService`, remover `AudioMotionCloneAnalyzer`/`VisualStreamFrameEncoder`, recuperar os controles locais na tela Android e remover os testes/docs desta entrega.

## Proximos passos

1. Validar em device fisico com a mesma musica usada no Windows, confirmando linhas espelhadas e paleta rainbow no HUB75.
2. Se houver diferenca perceptual, comparar a fonte capturada pelo Android com a fonte WASAPI do Windows antes de ajustar constantes.
