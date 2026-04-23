#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md

#include <Arduino.h>
#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <WebSocketsClient.h>
#include <stdint.h>

// ---------------------------------------------------------------------------
// Connectivity state
// ---------------------------------------------------------------------------
void logConnectivityState(const char* eventOverride = nullptr);
void publishConnectivityLog(const char* wifiState, const char* lastEvent, bool changedOrForced);
void flushWsFlapDiagnostics(bool force = false);
void registerWsConnectivitySample(bool connected);
void setConnectivityState(const char* wifiState, const char* lastEvent, bool forceLog = false, bool publishEvent = true);
void setProvisioningPortalActive(bool active, const char* eventName);
void registerInvalidStreamFrame(const char* reason);

// ---------------------------------------------------------------------------
// HTTP helpers
// ---------------------------------------------------------------------------
bool postJsonWithAuth(const String& path, JsonDocument& doc);
String normalizeLowerHex(const String& value);
String bytesToLowerHex(const uint8_t* bytes, size_t length);
bool beginHttpWithDeviceAuth(HTTPClient& http, const String& path);
bool beginHttpWithDeviceAuthUrl(HTTPClient& http, const String& url);

// ---------------------------------------------------------------------------
// MQTT configuration & publish
// ---------------------------------------------------------------------------
void normalizeMqttConfig();
void persistMqttConfig();
String buildDeviceMqttTopic(const char* suffix);
bool publishMqttDocument(const String& topic, JsonDocument& doc, bool retained);
bool publishDeviceStats();
bool publishDeviceLog(
    const char* level,
    const char* category,
    const char* eventCode,
    const String& message,
    bool includeTelemetrySequence = true);
bool publishDeviceLog(
    const char* level,
    const char* category,
    const char* eventCode,
    const char* message,
    bool includeTelemetrySequence = true);
String buildPresencePayload(const char* state);
bool publishPresence(const char* state);
void disconnectMqtt(bool publishOffline);

// ---------------------------------------------------------------------------
// Command progress & acknowledgment
// ---------------------------------------------------------------------------
void sendCommandProgress(
    const String& commandId,
    uint8_t progressPercent,
    const char* stage,
    const String& message,
    int successFlag = -1);
void postCommandAck(
    const String& commandId,
    bool success,
    const String& message,
    int progressPercent,
    const char* stage,
    const char* errorCode = nullptr);

// ---------------------------------------------------------------------------
// Telemetry & performance
// ---------------------------------------------------------------------------
bool trySanitizeLargestFreeBlock(uint32_t freeBytes, size_t largestRawBytes, uint32_t& largestSanitizedBytes);
uint32_t elapsedMicrosSince(uint32_t startUs);
void updateLoopHealthyPercent(uint32_t loopDurationUs);
void reportPerfMetrics();
void sendTelemetry(bool force);
bool applyStreamBinaryFrame(const uint8_t* payload, size_t len, bool cancelPanelsPlaybackForStream);

// ---------------------------------------------------------------------------
// WebSocket handler & connection
// ---------------------------------------------------------------------------
void onWsEvent(WStype_t type, uint8_t* payload, size_t len);
void connectWebSocket();

// ---------------------------------------------------------------------------
// MQTT callback & connection
// ---------------------------------------------------------------------------
void onMqttMessage(char* topic, uint8_t* payload, unsigned int length);
void connectMqtt();

// ---------------------------------------------------------------------------
// Network poll
// ---------------------------------------------------------------------------
void processNetworkPoll();
