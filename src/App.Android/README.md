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

Este cliente é focado em **gerenciamento remoto**. Captura de áudio
(WASAPI loopback) é exclusiva do cliente Windows (`App.WinUI`).
