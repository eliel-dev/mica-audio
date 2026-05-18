# Mica Audio — Android Client

Diretório: `src/App.Android/`

Cliente Android nativo (Kotlin + Jetpack Compose) para gerenciamento remoto
dos dispositivos ESP32 HUB75 via `MicaAudio.Server`.

## Build

Requer Android SDK com API level 36. Abra este diretório no Android Studio
ou use CLI:

```bash
cd src/App.Android
./gradlew assembleDebug
```

## Configuração

No primeiro run, vá em **Config** (tab inferior) e configure:

1. **Endereço do servidor** — IP:porta do MicaAudio.Server (ex: `http://192.168.1.100:5272`)
2. **Admin Token** — token de autenticação (se configurado no servidor)

## Funcionalidades

- **Devices**: lista, parear, controlar brilho, test LED, remover
- **Painéis**: catalogo compartilhado, editor touch com mover/redimensionar widgets, grade/cache de midia GIF
- **Apps**: catálogo de widgets, ativar apps nos devices
- **Monitor**: telemetria em tempo real, logs via WebSocket
- **Config**: servidor, token, tema (dark/light/system)

## Escopo

Este cliente e focado em **gerenciamento remoto** e tambem possui captura
local para o visualizador fixo **AudioMotion Clone**. No Android, a captura usa
`android.media.audiofx.Visualizer` na sessao 0 com a permissao `RECORD_AUDIO`,
sem prompt de compartilhamento de tela. O app publica `StreamFrameV2` `Bins128`
com `flags=0x11`, preview local sempre visivel e loop de emissao a 60 FPS para
espelhar o visual calibrado do Windows.

O cliente Windows continua usando WASAPI loopback. A biblioteca de midia dos
widgets GIF e sincronizada com o servidor e cacheada localmente no Android para
evitar download repetido.
