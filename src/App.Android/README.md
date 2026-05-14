# Mica Audio — Android Client

Diretório: `src/App.Android/`

Cliente Android nativo (Kotlin + Jetpack Compose) para gerenciamento remoto
dos dispositivos ESP32 HUB75 via `MicaAudio.Server`.

## Build

Requer Android SDK com API level 35. Abra este diretório no Android Studio
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
- **Painéis**: visualizar/remover painel ativo, listar widgets
- **Apps**: catálogo de widgets, ativar apps nos devices
- **Monitor**: telemetria em tempo real, logs via WebSocket
- **Config**: servidor, token, tema (dark/light/system)

## Escopo

Este cliente e focado em **gerenciamento remoto** e tambem possui captura
local para o visualizador fixo **AudioMotion Clone**. No Android, a captura usa
`MediaProjection`/`AudioPlaybackCapture` e publica `StreamFrameV2` `Bins128`
com `flags=0x11` para espelhar o visual calibrado do Windows. O cliente
Windows continua usando WASAPI loopback.

Observacao: o Android exige o consentimento de `MediaProjection` para capturar
audio reproduzido por outros apps, mesmo quando o app usa apenas
`AudioPlaybackCapture`. O cliente Android nao cria `VirtualDisplay` nem envia
frames de video; usa somente o audio retornado pelo sistema.
