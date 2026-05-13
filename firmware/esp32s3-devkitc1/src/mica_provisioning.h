#pragma once

#include <Arduino.h>

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// STA-hardcoded provisioning. Replaces the WiFiManager AP/portal flow with a
// fixed Wi-Fi connect (credentials come from mica_config.h) and an automatic
// device registration call to MicaAudio.Server.
//
// The legacy entry points (`processSerialProvisioning`, `sendSerialHello`,
// `isProvisioningIncomplete`, `resolveProvisioningIncompleteReason`,
// `startProvisioningPortal`, `enterProvisioningMode`) remain as no-op or
// minimal stubs so that the rest of the firmware (mica_network.cpp,
// mica_commands.cpp, main.cpp) compiles without modification while we ride
// the transition.

// ---------------------------------------------------------------------------
// New STA + auto-register API
// ---------------------------------------------------------------------------

// Connects Wi-Fi in STA mode using the credentials baked into mica_config.h.
// Returns true when WiFi.status() == WL_CONNECTED before the timeout.
bool connectStaHardcoded(unsigned long timeoutMs);

// Posts the device MAC to MicaAudio.Server's /api/v1/auto-register endpoint
// when the device does not yet have a deviceId/token persisted in NVS. The
// server returns a deterministic deviceId per MAC (idempotent) plus a token,
// MQTT host/port and root topic. Persists the response in NVS via gPrefs.
//
// Returns true when, after the call, gDeviceId and gToken are populated
// (whether by a fresh registration or by the previously cached values).
bool autoRegisterIfNeeded();

// Returns the MAC address as a lowercased, separator-free hex string
// (12 chars, e.g. "aabbccddeeff"). Empty before WiFi.begin() succeeds.
String getDeviceMacAddressHex();

// ---------------------------------------------------------------------------
// Legacy stubs (kept for source-level compatibility)
// ---------------------------------------------------------------------------
void processSerialProvisioning();
void sendSerialHello();
bool isProvisioningIncomplete();
const char* resolveProvisioningIncompleteReason();
bool startProvisioningPortal(const char* reason);
void enterProvisioningMode(bool clearDeviceCredentials, const char* reason);
