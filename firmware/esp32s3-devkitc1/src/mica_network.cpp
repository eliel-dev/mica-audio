// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---rollback-para-ap-first-estavel

#include "mica_network.h"

#include <esp_heap_caps.h>
#include <math.h>

#include "mica_display.h"
#include "mica_globals.h"
#include "mica_ota.h"
#include "mica_panels.h"

#include "mica_commands.h"
#include "mica_provisioning.h"

// ===========================================================================
// HTTP helpers
// ===========================================================================

bool postJsonWithAuth(const String& path, JsonDocument& doc) {
  if (gDeviceId.isEmpty() || gToken.isEmpty() || gServerHost.isEmpty()) {
    return false;
  }

  HTTPClient http;
  String url = "http://" + gServerHost + ":" + String(gServerPort) + path;
  if (!http.begin(url)) {
    return false;
  }

  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Id", gDeviceId);
  http.addHeader("X-Device-Token", gToken);

  String body;
  serializeJson(doc, body);
  int code = http.POST(body);
  http.end();

  return code >= 200 && code < 300;
}

String normalizeLowerHex(const String& value) {
  String normalized = value;
  normalized.trim();
  normalized.toLowerCase();
  return normalized;
}

String bytesToLowerHex(const uint8_t* bytes, size_t length) {
  const char* hex = "0123456789abcdef";
  String result;
  result.reserve(length * 2u);
  for (size_t index = 0; index < length; index++) {
    const uint8_t value = bytes[index];
    result += hex[(value >> 4) & 0x0Fu];
    result += hex[value & 0x0Fu];
  }

  return result;
}

bool beginHttpWithDeviceAuth(HTTPClient& http, const String& path) {
  if (gDeviceId.isEmpty() || gToken.isEmpty() || gServerHost.isEmpty()) {
    return false;
  }

  String normalizedPath = path;
  if (!normalizedPath.startsWith("/")) {
    normalizedPath = "/" + normalizedPath;
  }

  String url = "http://" + gServerHost + ":" + String(gServerPort) + normalizedPath;
  if (!http.begin(url)) {
    return false;
  }

  http.setConnectTimeout(5000);
  http.setTimeout(15000);
  http.addHeader("X-Device-Id", gDeviceId);
  http.addHeader("X-Device-Token", gToken);
  return true;
}

bool beginHttpWithDeviceAuthUrl(HTTPClient& http, const String& url) {
  if (gDeviceId.isEmpty() || gToken.isEmpty() || url.isEmpty()) {
    return false;
  }

  if (!http.begin(url)) {
    return false;
  }

  http.setConnectTimeout(5000);
  http.setTimeout(15000);
  http.addHeader("X-Device-Id", gDeviceId);
  http.addHeader("X-Device-Token", gToken);
  static const char* kHeaderKeys[] = {"Content-Type"};
  http.collectHeaders(kHeaderKeys, 1);
  return true;
}

// ===========================================================================
// MQTT configuration & publish
// ===========================================================================

void normalizeMqttConfig() {
  if (gMqttHost.isEmpty()) {
    gMqttHost = gServerHost;
  }

  if (gMqttPort == 0) {
    gMqttPort = kDefaultMqttPort;
  }

  if (gMqttRootTopic.isEmpty()) {
    gMqttRootTopic = kDefaultMqttRootTopic;
  }
}

void persistMqttConfig() {
  normalizeMqttConfig();
  gPrefs.putString("mqttHost", gMqttHost);
  gPrefs.putString("mqttPort", String(gMqttPort));
  gPrefs.putString("mqttRootTopic", gMqttRootTopic);
}

String buildDeviceMqttTopic(const char* suffix) {
  normalizeMqttConfig();
  return gMqttRootTopic + "/" + gDeviceId + "/" + String(suffix == nullptr ? "" : suffix);
}

bool publishMqttDocument(const String& topic, JsonDocument& doc, bool retained) {
  if (!gMqtt.connected()) {
    return false;
  }

  String payload;
  serializeJson(doc, payload);
  return gMqtt.publish(topic.c_str(), payload.c_str(), retained);
}

bool publishDeviceStats() {
  if (!gMqtt.connected() || gDeviceId.isEmpty()) {
    return false;
  }

  JsonDocument stats;
  stats["deviceId"] = gDeviceId;
  stats["chipModel"] = ESP.getChipModel();
  stats["chipRevision"] = ESP.getChipRevision();
  stats["chipCores"] = ESP.getChipCores();
  stats["cpuFreqMHz"] = ESP.getCpuFreqMHz();
  stats["sdkVersion"] = ESP.getSdkVersion();
  stats["heapTotalBytes"] = ESP.getHeapSize();
  stats["psramTotalBytes"] = ESP.getPsramSize();
  stats["flashTotalBytes"] = ESP.getFlashChipSize();
  stats["sketchSizeBytes"] = ESP.getSketchSize();
  stats["freeSketchBytes"] = ESP.getFreeSketchSpace();
  return publishMqttDocument(buildDeviceMqttTopic("stats"), stats, true);
}

bool publishDeviceLog(
    const char* level,
    const char* category,
    const char* eventCode,
    const String& message,
    bool includeTelemetrySequence) {
  if (!gMqtt.connected() || gDeviceId.isEmpty()) {
    return false;
  }

  String normalizedMessage = message;
  normalizedMessage.trim();
  if (normalizedMessage.length() == 0) {
    return false;
  }

  JsonDocument log;
  log["deviceId"] = gDeviceId;
  log["sequence"] = ++gDeviceLogSequence;
  log["level"] = (level != nullptr && level[0] != '\0') ? level : "info";
  log["category"] = (category != nullptr && category[0] != '\0') ? category : "device";
  if (eventCode != nullptr && eventCode[0] != '\0') {
    log["eventCode"] = eventCode;
  }

  log["message"] = normalizedMessage;
  log["uptimeSeconds"] = millis() / 1000UL;
  if (includeTelemetrySequence && gTelemetrySequence > 0) {
    log["telemetrySequence"] = gTelemetrySequence;
  }

  return publishMqttDocument(buildDeviceMqttTopic("logs"), log, false);
}

String buildPresencePayload(const char* state) {
  JsonDocument presence;
  presence["deviceId"] = gDeviceId;
  presence["state"] = state == nullptr ? "offline" : state;

  String payload;
  serializeJson(presence, payload);
  return payload;
}

bool publishPresence(const char* state) {
  if (gDeviceId.isEmpty() || !gMqtt.connected()) {
    return false;
  }

  JsonDocument presence;
  presence["deviceId"] = gDeviceId;
  presence["state"] = state == nullptr ? "offline" : state;
  return publishMqttDocument(buildDeviceMqttTopic("presence"), presence, true);
}

void disconnectMqtt(bool publishOffline) {
  if (!gMqtt.connected()) {
    return;
  }

  if (publishOffline) {
    (void)publishDeviceLog("warning", "mqtt", "disconnect", "Controle MQTT desconectando.", false);
    (void)publishPresence("offline");
    delay(20);
  }

  gMqtt.disconnect();
  gLastTelemetryMs = 0;
  gMqttDisconnectedSinceMs = millis();
}

void sendCommandProgress(
    const String& commandId,
    uint8_t progressPercent,
    const char* stage,
    const String& message,
    int successFlag) {
  if (commandId.isEmpty() || !gMqtt.connected()) {
    return;
  }

  JsonDocument progress;
  progress["deviceId"] = gDeviceId;
  progress["commandId"] = commandId;
  progress["progressPercent"] = progressPercent;
  if (stage != nullptr && stage[0] != '\0') {
    progress["stage"] = stage;
  }
  if (message.length() > 0) {
    progress["message"] = message;
  }
  if (successFlag == 0 || successFlag == 1) {
    progress["success"] = successFlag == 1;
  }

  (void)publishMqttDocument(buildDeviceMqttTopic("command-events"), progress, false);
  if (successFlag == 0 || successFlag == 1 || progressPercent >= 100) {
    (void)publishDeviceLog(
        successFlag == 0 ? "error" : "info",
        "command",
        stage,
        message.length() > 0 ? message : "Atualizacao de comando concluida.");
  }
}

void postCommandAck(
    const String& commandId,
    bool success,
    const String& message,
    int progressPercent,
    const char* stage,
    const char* errorCode) {
  if (commandId.isEmpty()) {
    return;
  }

  JsonDocument ack;
  ack["deviceId"] = gDeviceId;
  ack["commandId"] = commandId;
  ack["success"] = success;
  ack["message"] = message;
  ack["progressPercent"] = progressPercent;
  if (stage != nullptr && stage[0] != '\0') {
    ack["stage"] = stage;
  }
  if (errorCode != nullptr && errorCode[0] != '\0') {
    ack["errorCode"] = errorCode;
  }

  postJsonWithAuth("/api/v1/device/command-ack", ack);
}

// ===========================================================================
// Connectivity state
// ===========================================================================

void logConnectivityState(const char* eventOverride) {
  const char* eventName = (eventOverride != nullptr && eventOverride[0] != '\0')
      ? eventOverride
      : gLastWifiEvent.c_str();

  Serial.printf("[conn] wifiState=%s portal=%s event=%s\n",
      gWifiState.c_str(),
      gProvisioningPortalActive ? "on" : "off",
      eventName);
}

bool publishDeviceLog(
    const char* level,
    const char* category,
    const char* eventCode,
    const char* message,
    bool includeTelemetrySequence) {
  return publishDeviceLog(
      level,
      category,
      eventCode,
      String(message == nullptr ? "" : message),
      includeTelemetrySequence);
}

void publishConnectivityLog(const char* wifiState, const char* lastEvent, bool changedOrForced) {
  if (!changedOrForced || lastEvent == nullptr || lastEvent[0] == '\0') {
    return;
  }

  const char* category = "wifi";
  String message;
  if (strncmp(lastEvent, "portal_", 7) == 0) {
    category = "portal";
    message = String("Portal de provisioning: ") + (gProvisioningPortalActive ? "ativo" : "inativo");
  } else if (strncmp(lastEvent, "ws_", 3) == 0) {
    category = "ws";
    message = String("Sessao websocket: ") + lastEvent;
  } else if (strncmp(lastEvent, "mqtt_", 5) == 0) {
    category = "mqtt";
    message = String("Controle MQTT: ") + lastEvent;
  } else {
    message = String("Wi-Fi state=") + (wifiState == nullptr ? gWifiState : wifiState) + " event=" + lastEvent;
  }

  (void)publishDeviceLog("info", category, lastEvent, message, false);
}

void flushWsFlapDiagnostics(bool force) {
  if (gWsFlapWindowStartMs == 0) {
    return;
  }

  const unsigned long now = millis();
  const unsigned long elapsed = now - gWsFlapWindowStartMs;
  if (!force && elapsed < kWsFlapReportWindowMs) {
    return;
  }

  if (gWsDisconnectCountInWindow >= kWsFlapReportThreshold) {
    Serial.printf(
        "[ws_diag] window_ms=%lu connects=%u disconnects=%u reconnect_interval_ms=%lu state=flapping\n",
        elapsed,
        gWsConnectCountInWindow,
        gWsDisconnectCountInWindow,
        kWsAutoReconnectIntervalMs);
    (void)publishDeviceLog(
        "warning",
        "ws",
        "flapping",
        String("WS flapping detectado: connects=") + gWsConnectCountInWindow
            + " disconnects=" + gWsDisconnectCountInWindow,
        false);
  }

  gWsFlapWindowStartMs = now;
  gWsConnectCountInWindow = 0;
  gWsDisconnectCountInWindow = 0;
}

void registerWsConnectivitySample(bool connected) {
  if (gWsFlapWindowStartMs == 0) {
    gWsFlapWindowStartMs = millis();
  } else {
    flushWsFlapDiagnostics(false);
  }

  if (connected) {
    gWsConnectCountInWindow++;
  } else {
    gWsDisconnectCountInWindow++;
  }
}

void setConnectivityState(const char* wifiState, const char* lastEvent, bool forceLog, bool publishEvent) {
  bool changed = false;
  if (wifiState != nullptr && wifiState[0] != '\0' && !gWifiState.equals(wifiState)) {
    gWifiState = wifiState;
    changed = true;
  }

  if (publishEvent && lastEvent != nullptr && lastEvent[0] != '\0' && !gLastWifiEvent.equals(lastEvent)) {
    gLastWifiEvent = lastEvent;
    changed = true;
  }

  if (changed || forceLog) {
    logConnectivityState(publishEvent ? nullptr : lastEvent);
    publishConnectivityLog(
        wifiState != nullptr && wifiState[0] != '\0' ? wifiState : gWifiState.c_str(),
        lastEvent,
        true);
  }
}

void setProvisioningPortalActive(bool active, const char* eventName) {
  if (gProvisioningPortalActive == active && (eventName == nullptr || eventName[0] == '\0')) {
    return;
  }

  gProvisioningPortalActive = active;
  setConnectivityState(active ? kWifiStatePortal : kWifiStateConnected, eventName, true);
}

// ===========================================================================
// Telemetry & performance
// ===========================================================================

bool trySanitizeLargestFreeBlock(uint32_t freeBytes, size_t largestRawBytes, uint32_t& largestSanitizedBytes) {
  if (freeBytes == 0 || largestRawBytes == 0) {
    return false;
  }

  size_t normalizedLargest = largestRawBytes;
  if (normalizedLargest > freeBytes) {
    normalizedLargest = freeBytes;
  }

  if (normalizedLargest == 0) {
    return false;
  }

  largestSanitizedBytes = static_cast<uint32_t>(normalizedLargest);
  return true;
}

uint32_t elapsedMicrosSince(uint32_t startUs) {
  return static_cast<uint32_t>(micros() - startUs);
}

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#saude-oficial-do-loop
void updateLoopHealthyPercent(uint32_t loopDurationUs) {
  const unsigned long nowMs = millis();
  if (gLoopWindowStartMs == 0) {
    gLoopWindowStartMs = nowMs;
  }

  gLoopWindowIterationCount++;
  if (loopDurationUs <= kHealthyLoopThresholdUs) {
    gLoopWindowHealthyCount++;
  }

  const unsigned long windowElapsedMs = nowMs - gLoopWindowStartMs;
  if (windowElapsedMs < kLoopHealthWindowMs) {
    return;
  }

  uint32_t healthyPercent = 0;
  if (gLoopWindowIterationCount > 0) {
    healthyPercent = static_cast<uint32_t>(
        (static_cast<uint64_t>(gLoopWindowHealthyCount) * 100ULL
            + static_cast<uint64_t>(gLoopWindowIterationCount / 2u))
        / static_cast<uint64_t>(gLoopWindowIterationCount));
  }

  if (healthyPercent > 100u) {
    healthyPercent = 100u;
  }

  gLoopHealthyPercent = static_cast<uint8_t>(healthyPercent);
  gLoopHealthyPercentReady = true;
  gLoopWindowIterationCount = 0;
  gLoopWindowHealthyCount = 0;
  gLoopWindowStartMs = nowMs;
}

void reportPerfMetrics() {
  const unsigned long now = millis();
  if (gPerfLastReportMs != 0 && (now - gPerfLastReportMs) < kTelemetryIntervalMs) {
    return;
  }

  const uint32_t elapsed = gPerfLastReportMs == 0 ? 0 : static_cast<uint32_t>(now - gPerfLastReportMs);
  const uint32_t presentedSinceLastReport = gHub75PresentFrames - gPerfHub75PresentFramesAtLastReport;
  const uint32_t hub75Fps = (elapsed > 0)
      ? static_cast<uint32_t>((static_cast<uint64_t>(presentedSinceLastReport) * 1000ULL) / elapsed)
      : 0;

  gPerfLastReportLoopMaxUs = gPerfLoopMaxUs;
  gPerfLastReportNetworkMaxUs = gPerfNetworkMaxUs;
  gPerfLastReportRenderMaxUs = gPerfRenderMaxUs;
  gPerfLastReportSerialMaxUs = gPerfSerialMaxUs;

  Serial.printf(
      "[perf] loop_max_us=%lu net_max_us=%lu render_max_us=%lu serial_max_us=%lu hub75_fps=%lu render_skips=%lu\n",
      static_cast<unsigned long>(gPerfLoopMaxUs),
      static_cast<unsigned long>(gPerfNetworkMaxUs),
      static_cast<unsigned long>(gPerfRenderMaxUs),
      static_cast<unsigned long>(gPerfSerialMaxUs),
      static_cast<unsigned long>(hub75Fps),
      static_cast<unsigned long>(gPerfRenderSkipCount));

  if (gPanelsBatchTaskHandle != nullptr) {
    const UBaseType_t batchHwm = uxTaskGetStackHighWaterMark(gPanelsBatchTaskHandle);
    const UBaseType_t loopHwm = uxTaskGetStackHighWaterMark(nullptr);
    Serial.printf(
        "[perf] loopTask_stack_hwm=%u batchTask_stack_hwm=%u\n",
        static_cast<unsigned>(loopHwm),
        static_cast<unsigned>(batchHwm));
  }

  gPerfLoopMaxUs = 0;
  gPerfNetworkMaxUs = 0;
  gPerfRenderMaxUs = 0;
  gPerfSerialMaxUs = 0;
  gPerfRenderSkipCount = 0;
  gPerfHub75PresentFramesAtLastReport = gHub75PresentFrames;
  gPerfLastReportMs = now;
}

void sendTelemetry(bool force) {
  if (!gMqtt.connected() || gDeviceId.isEmpty()) {
    return;
  }

  unsigned long now = millis();
  if (!force && now - gLastTelemetryMs < kTelemetryIntervalMs) {
    return;
  }

  gLastTelemetryMs = now;

  JsonDocument telemetry;
  const bool wifiConnected = WiFi.status() == WL_CONNECTED;
  const uint32_t uptimeSeconds = millis() / 1000UL;
  const uint32_t freeHeapBytes = ESP.getFreeHeap();

  telemetry["deviceId"] = gDeviceId;
  telemetry["wifiConnected"] = wifiConnected;
  telemetry["wifiState"] = gWifiState;
  telemetry["provisioningPortalActive"] = gProvisioningPortalActive;
  telemetry["auxLedAvailable"] = gAuxLedAvailable;
  telemetry["testLedAvailable"] = isTestLedAvailable();
  telemetry["lastWifiEvent"] = gLastWifiEvent;
  telemetry["rssi"] = wifiConnected ? WiFi.RSSI() : -100;
  telemetry["uptimeSeconds"] = uptimeSeconds;
  if (gLoopHealthyPercentReady) {
    telemetry["loopHealthyPercent"] = gLoopHealthyPercent;
  }
  telemetry["freeHeapBytes"] = freeHeapBytes;
  telemetry["streamFramesReceived"] = gStreamFramesReceived;
  telemetry["streamFramesApplied"] = gStreamFramesApplied;
  telemetry["hub75PresentFrames"] = gHub75PresentFrames;
  telemetry["streamSequenceGapCount"] = gStreamSequenceGapCount;
  telemetry["streamInvalidFrameCount"] = gStreamInvalidFrameCount;
  telemetry["networkPollDeferCount"] = gNetworkPollDeferCount;
  telemetry["telemetrySequence"] = ++gTelemetrySequence;
  telemetry["brightnessCap"] = gBrightnessCap;
  telemetry["brightnessRequested"] = gStreamBrightness;
  telemetry["brightnessApplied"] = gAppliedBrightness;
  telemetry["testLedEnabled"] = gTestLedEnabled;
  telemetry["testLedDuty"] = gTestLedDuty;
  if (gHasStreamLastSequence) {
    telemetry["streamLastSequence"] = gStreamLastSequence;
  }

  uint32_t largestHeapBlockBytes = 0;
  if (trySanitizeLargestFreeBlock(freeHeapBytes, heap_caps_get_largest_free_block(MALLOC_CAP_8BIT), largestHeapBlockBytes)) {
    telemetry["largestHeapBlockBytes"] = largestHeapBlockBytes;
  }

  const bool psramAvailable = ESP.getPsramSize() > 0;
  telemetry["psramAvailable"] = psramAvailable;
  if (psramAvailable) {
    const uint32_t freePsramBytes = ESP.getFreePsram();
    telemetry["freePsramBytes"] = freePsramBytes;

#if defined(MALLOC_CAP_SPIRAM)
    uint32_t largestPsramBlockBytes = 0;
    if (trySanitizeLargestFreeBlock(freePsramBytes, heap_caps_get_largest_free_block(MALLOC_CAP_SPIRAM), largestPsramBlockBytes)) {
      telemetry["largestPsramBlockBytes"] = largestPsramBlockBytes;
    }
#endif
  }

  telemetry["firmwareVersion"] = kFirmwareVersion;
  telemetry["ipAddress"] = WiFi.localIP().toString();
  telemetry["boardModel"] = kBoardModel;
  telemetry["panelType"] = kPanelType;
  const float chipTemperatureCelsius = temperatureRead();
  if (!isnan(chipTemperatureCelsius) && isfinite(chipTemperatureCelsius)) {
    telemetry["chipTemperatureCelsius"] = chipTemperatureCelsius;
  }
  if (gActiveAppId.length() > 0) {
    telemetry["activeAppId"] = gActiveAppId;
  }
  if (gActiveAppName.length() > 0) {
    telemetry["activeAppName"] = gActiveAppName;
  }
  telemetry["animatedWebpBatchSupported"] = gAnimatedWebpBatchSupported;
  if (gPerfLastReportMs > 0) {
    telemetry["perfLoopMaxUs"] = gPerfLastReportLoopMaxUs;
    telemetry["perfNetworkMaxUs"] = gPerfLastReportNetworkMaxUs;
    telemetry["perfRenderMaxUs"] = gPerfLastReportRenderMaxUs;
    telemetry["perfSerialMaxUs"] = gPerfLastReportSerialMaxUs;
  }

  (void)publishMqttDocument(buildDeviceMqttTopic("status"), telemetry, true);
}

void registerInvalidStreamFrame(const char* reason) {
  gStreamInvalidFrameCount++;
  if (gStreamInvalidFrameCount == 1 || (gStreamInvalidFrameCount % 25u) == 0u) {
    (void)publishDeviceLog(
        "warning",
        "stream",
        reason,
        String("Frame de stream invalido detectado. total=") + gStreamInvalidFrameCount);
  }
}

// ===========================================================================
// WebSocket / MQTT callbacks, connection & network poll
// ===========================================================================

void onMqttMessage(char* topic, uint8_t* payload, unsigned int length) {
  if (topic == nullptr || payload == nullptr || length == 0 || gDeviceId.isEmpty()) {
    return;
  }

  String expectedTopic = buildDeviceMqttTopic("commands");
  if (!expectedTopic.equals(topic)) {
    return;
  }

  JsonDocument control;
  if (deserializeJson(control, payload, length) != DeserializationError::Ok) {
    return;
  }

  handleControlCommandMessage(control);
}

// DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
void onWsEvent(WStype_t type, uint8_t *payload, size_t len) {
  if (type == WStype_CONNECTED) {
    gWsDisconnectedSinceMs = 0;
    registerWsConnectivitySample(true);
    setConnectivityState(kWifiStateConnected, "ws_connected", true, false);
    Serial.println("[ws_connected] sessao websocket estabelecida.");
    sendTelemetry(true);
    return;
  }

  if (type == WStype_DISCONNECTED) {
    registerWsConnectivitySample(false);
    setConnectivityState(WiFi.status() == WL_CONNECTED ? kWifiStateConnected : kWifiStateDisconnected, "ws_disconnected", true, false);
    Serial.println("[ws_disconnected] sessao websocket encerrada.");
    gLastTelemetryMs = 0;
    return;
  }

  if (type == WStype_BIN) {
    if (payload == nullptr || len < 2) {
      registerInvalidStreamFrame("payload_short");
      return;
    }

    if (payload[0] != kStreamVersion) {
      registerInvalidStreamFrame("version_invalid");
      return;
    }

    uint32_t frameSequence = 0;
    const bool hasSequence = len >= 6;
    if (hasSequence) {
      frameSequence = static_cast<uint32_t>(payload[2]) |
                      (static_cast<uint32_t>(payload[3]) << 8) |
                      (static_cast<uint32_t>(payload[4]) << 16) |
                      (static_cast<uint32_t>(payload[5]) << 24);
    }

    const uint8_t messageType = payload[1];
    if (messageType == kStreamBinsMessageType) {
      if (len < kStreamFrameSize) {
        registerInvalidStreamFrame("bins_short");
        return;
      }

      cancelPanelsBatchPlayback();

      gStreamFramesReceived++;
      if (hasSequence) {
        if (gHasStreamLastSequence && frameSequence > gStreamLastSequence + 1u) {
          gStreamSequenceGapCount += frameSequence - (gStreamLastSequence + 1u);
        }

        gStreamLastSequence = frameSequence;
        gHasStreamLastSequence = true;
      }

      const uint8_t nextBinsIndex = static_cast<uint8_t>(gBinsActiveIndex ^ 1u);
      portENTER_CRITICAL(&gStreamBufferMux);
      gLevel = payload[14];
      memcpy(gBinsBuffers[nextBinsIndex], payload + 15, kBinsCount);
      gBinsActiveIndex = nextBinsIndex;
      gStreamBrightness = payload[143];
      gBinsFlags = payload[144];
      portEXIT_CRITICAL(&gStreamBufferMux);
      gFrameModeActive = false;
      gLastFrameMs = millis();
      gMatrixSignalTimedOut = false;
      markMatrixFrameDirty(true);
      return;
    }

    if (messageType == kStreamFrame128x64Rgb565MessageType) {
      if (len < kStreamFrame128x64Rgb565Size) {
        registerInvalidStreamFrame("frame_short");
        return;
      }

      cancelPanelsBatchPlayback();

      gStreamFramesReceived++;
      if (hasSequence) {
        if (gHasStreamLastSequence && frameSequence > gStreamLastSequence + 1u) {
          gStreamSequenceGapCount += frameSequence - (gStreamLastSequence + 1u);
        }

        gStreamLastSequence = frameSequence;
        gHasStreamLastSequence = true;
      }

      const uint8_t nextFrameIndex = static_cast<uint8_t>(gFrameRgb565ActiveIndex ^ 1u);
      uint16_t* frameBackBuffer = gFrameRgb565Buffers[nextFrameIndex];
      gStreamBrightness = payload[14];
      // Payload is already little-endian and ESP32 is little-endian, so we can bulk copy.
      memcpy(frameBackBuffer, payload + 15, static_cast<size_t>(kMatrixPixelCount) * sizeof(uint16_t));

      portENTER_CRITICAL(&gStreamBufferMux);
      gFrameRgb565ActiveIndex = nextFrameIndex;
      portEXIT_CRITICAL(&gStreamBufferMux);

      gFrameModeActive = true;
      gLastFrameMs = millis();
      gMatrixSignalTimedOut = false;
      markMatrixFrameDirty(true);
      return;
    }

    registerInvalidStreamFrame("message_type_unknown");

    return;
  }

  if (type != WStype_TEXT || payload == nullptr || len == 0) {
    return;
  }

  JsonDocument control;
  if (deserializeJson(control, payload, len) != DeserializationError::Ok) {
    return;
  }
  handleControlCommandMessage(control);
}

void connectWebSocket() {
  if (gDeviceId.isEmpty() || gToken.isEmpty() || gServerHost.isEmpty()) {
    setConnectivityState(kWifiStateConnecting, "ws_missing_auth", true, false);
    return;
  }

  if (WiFi.status() != WL_CONNECTED) {
    setConnectivityState(kWifiStateConnecting, "ws_waiting_wifi", true, false);
    return;
  }

  String extraHeaders = "X-Device-Id: " + gDeviceId + "\r\nX-Device-Token: " + gToken;
  gWs.setExtraHeaders(extraHeaders.c_str());
  String path = "/ws/v1/stream";
  Serial.printf("[ws] conectando em ws://%s:%u%s\n", gServerHost.c_str(), gServerPort, path.c_str());
  setConnectivityState(kWifiStateConnected, "ws_connecting", true, false);
  gWs.begin(gServerHost.c_str(), gServerPort, path.c_str());
  gWs.onEvent(onWsEvent);
  gWs.setReconnectInterval(kWsAutoReconnectIntervalMs);
}

void connectMqtt() {
  normalizeMqttConfig();
  if (gDeviceId.isEmpty() || gToken.isEmpty() || gMqttHost.isEmpty()) {
    return;
  }

  if (WiFi.status() != WL_CONNECTED) {
    return;
  }

  if (gMqtt.connected()) {
    return;
  }

  gMqtt.setServer(gMqttHost.c_str(), gMqttPort);
  gMqtt.setCallback(onMqttMessage);
  gMqtt.setBufferSize(kMqttPacketBufferBytes);

  String presenceTopic = buildDeviceMqttTopic("presence");
  String offlinePayload = buildPresencePayload("offline");
  Serial.printf("[mqtt] conectando em mqtt://%s:%u (%s)\n", gMqttHost.c_str(), gMqttPort, gMqttRootTopic.c_str());

  const bool connected = gMqtt.connect(
      gDeviceId.c_str(),
      gDeviceId.c_str(),
      gToken.c_str(),
      presenceTopic.c_str(),
      1,
      true,
      offlinePayload.c_str());

  if (!connected) {
    gMqttDisconnectedSinceMs = millis();
    return;
  }

  gMqttDisconnectedSinceMs = 0;
  (void)gMqtt.subscribe(buildDeviceMqttTopic("commands").c_str(), 1);
  (void)publishPresence("online");
  (void)publishDeviceLog("info", "mqtt", "connected", "Controle MQTT conectado.", false);
  (void)publishDeviceStats();
  sendTelemetry(true);
  publishPendingOtaReportIfNeeded();
}


void processNetworkPoll() {
  const uint32_t networkBudgetStartUs = micros();
  bool networkBudgetExhausted = false;
  auto shouldRunNetworkStep = [&](bool eligible) -> bool {
    if (!eligible) {
      return false;
    }

    if (networkBudgetExhausted) {
      gNetworkPollDeferCount++;
      return false;
    }

    return true;
  };
  auto finishNetworkStep = [&]() {
    if (elapsedMicrosSince(networkBudgetStartUs) >= kNetworkPollBudgetUs) {
      networkBudgetExhausted = true;
    }
  };

  const bool wifiConnected = WiFi.status() == WL_CONNECTED;
  if (!wifiConnected) {
    if (shouldRunNetworkStep(gWifiDisconnectedSinceMs == 0)) {
      gWifiDisconnectedSinceMs = millis();
      setConnectivityState(kWifiStateDisconnected, "wifi_disconnected", true);
      Serial.println("[wifi] desconectado, aguardando reconexao.");
      finishNetworkStep();
    }

    bool provisioningStarted = false;
    const bool shouldStartProvisioningFallback =
        !gProvisioningPortalActive
        && gWifiDisconnectedSinceMs != 0
        && (millis() - gWifiDisconnectedSinceMs) > kWifiDisconnectProvisioningFallbackMs;
    if (shouldRunNetworkStep(shouldStartProvisioningFallback)) {
      Serial.println("[wifi] fallback para provisioning apos queda prolongada.");
      (void)startProvisioningPortal("wifi_disconnected_fallback");
      gWifiDisconnectedSinceMs = 0;
      provisioningStarted = true;
      finishNetworkStep();
    }

    if (provisioningStarted) {
      if (shouldRunNetworkStep(true)) {
        connectMqtt();
        finishNetworkStep();
      }
      if (shouldRunNetworkStep(true)) {
        connectWebSocket();
        finishNetworkStep();
      }
    }
  } else {
    if (gWifiDisconnectedSinceMs != 0) {
      gWifiDisconnectedSinceMs = 0;
    }

    if (shouldRunNetworkStep(gProvisioningPortalActive)) {
      setProvisioningPortalActive(false, "portal_closed");
      finishNetworkStep();
    }

    if (shouldRunNetworkStep(true)) {
      setConnectivityState(kWifiStateConnected, "wifi_connected");
      finishNetworkStep();
    }
    if (shouldRunNetworkStep(true)) {
      gMqtt.loop();
      finishNetworkStep();
    }
    if (shouldRunNetworkStep(true)) {
      gWs.loop();
      finishNetworkStep();
    }
    if (shouldRunNetworkStep(true)) {
      flushWsFlapDiagnostics(false);
      finishNetworkStep();
    }

    if (!gMqtt.connected()) {
      if (gMqttDisconnectedSinceMs == 0) {
        gMqttDisconnectedSinceMs = millis();
      }

      const bool shouldReconnectMqtt = (millis() - gMqttDisconnectedSinceMs) >= kMqttReconnectRetryMs;
      if (shouldRunNetworkStep(shouldReconnectMqtt)) {
        connectMqtt();
        gMqttDisconnectedSinceMs = millis();
        finishNetworkStep();
      }
    } else {
      gMqttDisconnectedSinceMs = 0;

      const bool telemetryDue =
          !gDeviceId.isEmpty()
          && (millis() - gLastTelemetryMs) >= kTelemetryIntervalMs;
      if (shouldRunNetworkStep(telemetryDue)) {
        sendTelemetry(false);
        finishNetworkStep();
      }
    }

    if (!gWs.isConnected()) {
      if (shouldRunNetworkStep(gWsDisconnectedSinceMs == 0)) {
        gWsDisconnectedSinceMs = millis();
        setConnectivityState(kWifiStateConnected, "ws_disconnected", false, false);
        finishNetworkStep();
      }

      const bool shouldReconnectWs =
          gWsDisconnectedSinceMs != 0
          && (millis() - gWsDisconnectedSinceMs) > kWsReconnectRetryMs;
      if (shouldRunNetworkStep(shouldReconnectWs)) {
        Serial.println("[ws_disconnected] sem sessao por tempo prolongado; tentando reconectar websocket.");
        connectWebSocket();
        gWsDisconnectedSinceMs = millis();
        finishNetworkStep();
      }
    } else {
      gWsDisconnectedSinceMs = 0;
    }
  }
}
