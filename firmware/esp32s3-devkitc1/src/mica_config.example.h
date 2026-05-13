#pragma once

// =============================================================================
// MicaAudio firmware configuration (USER LOCAL FILE)
// =============================================================================
//
// This is the TEMPLATE that ships with the repository.
//
// To prepare your firmware:
//   1. Copy this file to "mica_config.h" (same folder).
//   2. Edit "mica_config.h" with your local Wi-Fi credentials and
//      MicaAudio.Server endpoint.
//   3. Build the firmware with PlatformIO or via
//      `scripts/build-precompiled-firmware.ps1`.
//
// "mica_config.h" is git-ignored so it never reaches the repository. Each
// physical device may carry the same binary because the server's auto-register
// endpoint issues a deterministic deviceId per MAC address.
//
// IMPORTANT: do NOT commit a real "mica_config.h" with credentials.
// =============================================================================

// ----------- Wi-Fi (STA-only, no AP / no provisioning portal) ----------------

// SSID of the 2.4 GHz Wi-Fi network the ESP32 should join.
// The ESP32-S3 radio does NOT support 5 GHz networks.
#define MICA_WIFI_SSID "your-2g4-ssid"

// WPA/WPA2 passphrase (8-63 ASCII chars). For an open network use "".
#define MICA_WIFI_PASSWORD "your-wifi-password"

// ----------- MicaAudio.Server endpoint ---------------------------------------

// IPv4 address (or hostname resolvable on the LAN) of the machine running
// MicaAudio.Server (Docker container or `dotnet run`).
#define MICA_SERVER_HOST "192.168.1.100"

// HTTP port the MicaAudio.Server is listening on (default 5272).
#define MICA_SERVER_PORT 5272

// ----------- Optional: friendly name announced during auto-register ----------

// Free-form name displayed in the MicaAudio desktop app. Leave empty to let the
// server pick a default based on board model.
#define MICA_DEVICE_NAME ""
