#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#configuracao-hardcoded

// ===========================================================================
// Wi-Fi credentials (hardcoded for station-only mode)
// ===========================================================================
#define MICA_WIFI_SSID "RAM TCHUUUU"
#define MICA_WIFI_PASSWORD "Glock9mm"

// ===========================================================================
// Server configuration (hardcoded)
// ===========================================================================
#define MICA_SERVER_HOST "192.168.1.16"
#define MICA_SERVER_PORT 5272

// ===========================================================================
// Device name fallback
// ===========================================================================
#define MICA_DEVICE_NAME_FALLBACK "MicaAudio-ESP32S3"
