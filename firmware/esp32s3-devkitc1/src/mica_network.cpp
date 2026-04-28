// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---rollback-para-ap-first-estavel
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#shadow-retained-de-sessao
// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md
// DOCS: docs/handoffs/2026-04-17-firmware-control-worker-hardening.md
// DOCS: docs/handoffs/2026-04-18-wifi-reconnect-persistence-after-reset.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md

#include "mica_network.h"

#include <esp_heap_caps.h>
#include <math.h>
#include <WiFiUdp.h>

#include "mica_display.h"
#include "mica_globals.h"
#include "mica_ota.h"
#include "mica_panels.h"
#include "mica_prefs.h"
#include "mica_session.h"
#include "mica_visual_udp.h"

#include "mica_commands.h"
#include "mica_provisioning.h"

static WiFiUDP gDiscoveryUdp;
static bool gDiscoveryUdpStarted = false;
static unsigned long gLastDiscoveryBroadcastMs = 0;

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

  http.setConnectTimeout(5000);
  http.setTimeout(15000);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Id", gDeviceId);
  http.addHeader("X-Device-Token", gToken);

  String body;
  serializeJson(doc, body);
  resetTaskWatchdog();
  int code = http.POST(body);
  resetTaskWatchdog();
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

static bool parseServerBaseUrl(const String& rawBaseUrl, String& host, uint16_t& port) {
  String normalized = rawBaseUrl;
  normalized.trim();
  if (normalized.length() == 0) {
    return false;
  }

  if (normalized.startsWith("http://")) {
    normalized = normalized.substring(7);
  } else if (normalized.startsWith("https://")) {
    normalized = normalized.substring(8);
  }

  const int slashIndex = normalized.indexOf('/');
  if (slashIndex >= 0) {
    normalized = normalized.substring(0, slashIndex);
  }

  const int colonIndex = normalized.lastIndexOf(':');
  if (colonIndex >= 0) {
    host = normalized.substring(0, colonIndex);
    const int parsedPort = normalized.substring(colonIndex + 1).toInt();
    if (parsedPort <= 0 || parsedPort > 65535) {
      return false;
    }

    port = static_cast<uint16_t>(parsedPort);
    return host.length() > 0;
  }

  host = normalized;
  port = 5272;
  return host.length() > 0;
}

static bool shouldRunLanDiscovery() {
  return WiFi.status() == WL_CONNECTED
      && (gDeviceId.isEmpty() || gToken.isEmpty())
      && !gProvisioningPortalActive;
}

static void stopLanDiscovery() {
  if (!gDiscoveryUdpStarted) {
    return;
  }

  gDiscoveryUdp.stop();
  gDiscoveryUdpStarted = false;
}

static bool ensureLanDiscoveryStarted() {
  if (gDiscoveryUdpStarted) {
    return true;
  }

  if (gDiscoveryUdp.begin(0) == 0) {
    Serial.println("[discovery] falha ao abrir socket UDP.");
    return false;
  }

  gDiscoveryUdpStarted = true;
  Serial.printf("[discovery] UDP ativo; broadcast_port=%u\n", kDiscoveryUdpPort);
  return true;
}

static void sendLanDiscoveryBroadcast(unsigned long now) {
  if (gLastDiscoveryBroadcastMs != 0 && (now - gLastDiscoveryBroadcastMs) < kDiscoveryRetryMs) {
    return;
  }

  if (!ensureLanDiscoveryStarted()) {
    gLastDiscoveryBroadcastMs = now;
    return;
  }

  JsonDocument request;
  request["protocol"] = "mica.discovery.v1";
  request["deviceMac"] = WiFi.macAddress();
  request["deviceIp"] = WiFi.localIP().toString();
  request["deviceName"] = prefsGetStringOrDefault("name", String(kBoardDisplayName));
  request["firmwareVersion"] = kFirmwareVersion;
  request["boardModel"] = kBoardModel;
  request["panelType"] = kPanelType;
  request["profile"] = kFirmwareProfile;

  String payload;
  serializeJson(request, payload);
  resetTaskWatchdog();
  gDiscoveryUdp.beginPacket(IPAddress(255, 255, 255, 255), kDiscoveryUdpPort);
  gDiscoveryUdp.write(reinterpret_cast<const uint8_t*>(payload.c_str()), payload.length());
  gDiscoveryUdp.endPacket();
  resetTaskWatchdog();
  gLastDiscoveryBroadcastMs = now;
  Serial.printf("[discovery] broadcast enviado bytes=%u\n", static_cast<unsigned>(payload.length()));
}

static void applyLanDiscoveryResponse(JsonDocument& response) {
  String deviceId = response["deviceId"] | "";
  String token = response["token"] | "";
  String httpBase = response["httpBase"] | "";
  String mqttHost = response["mqttHost"] | "";
  int mqttPort = response["mqttPort"] | 0;
  String mqttRootTopic = response["mqttRootTopic"] | "";

  if (deviceId.isEmpty() || token.isEmpty()) {
    Serial.println("[discovery] resposta sem credenciais.");
    return;
  }

  String parsedHost;
  uint16_t parsedPort = 0;
  if (httpBase.length() > 0 && parseServerBaseUrl(httpBase, parsedHost, parsedPort)) {
    gServerHost = parsedHost;
    gServerPort = parsedPort;
    gPrefs.putString("host", gServerHost);
    gPrefs.putString("port", String(gServerPort));
  }

  gDeviceId = deviceId;
  gToken = token;
  gPrefs.putString("deviceId", gDeviceId);
  gPrefs.putString("token", gToken);

  gMqttHost = mqttHost.length() > 0 ? mqttHost : gServerHost;
  gMqttPort = mqttPort > 0 ? static_cast<uint16_t>(mqttPort) : kDefaultMqttPort;
  gMqttRootTopic = mqttRootTopic.length() > 0 ? mqttRootTopic : kDefaultMqttRootTopic;
  persistMqttConfig();

  stopLanDiscovery();
  gMqttDisconnectedSinceMs = 0;
  gWsDisconnectedSinceMs = 0;
  setConnectivityState(kWifiStateConnected, "discovery_success", true);
  Serial.printf("[discovery] registrado deviceId=%s http=%s:%u mqtt=%s:%u\n",
      gDeviceId.c_str(),
      gServerHost.c_str(),
      gServerPort,
      gMqttHost.c_str(),
      gMqttPort);
  connectMqtt();
  connectWebSocket();
}

static void receiveLanDiscoveryResponse() {
  if (!gDiscoveryUdpStarted) {
    return;
  }

  const int packetSize = gDiscoveryUdp.parsePacket();
  if (packetSize <= 0) {
    return;
  }

  char buffer[kDiscoveryPacketMaxBytes + 1] = {};
  const int bytesRead = gDiscoveryUdp.read(buffer, kDiscoveryPacketMaxBytes);
  if (bytesRead <= 0) {
    return;
  }

  buffer[bytesRead] = '\0';
  JsonDocument response;
  if (deserializeJson(response, buffer) != DeserializationError::Ok) {
    Serial.println("[discovery] resposta JSON invalida.");
    return;
  }

  String protocol = response["protocol"] | "";
  if (!protocol.equalsIgnoreCase("mica.discovery.v1")) {
    return;
  }

  applyLanDiscoveryResponse(response);
}

static void processLanDiscovery() {
  if (!shouldRunLanDiscovery()) {
    stopLanDiscovery();
    return;
  }

  const unsigned long now = millis();
  sendLanDiscoveryBroadcast(now);
  receiveLanDiscoveryResponse();
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

static const char* resetReasonCodeToString(uint8_t code) {
  switch (code) {
    case 1:
      return "POWERON_RESET";
    case 3:
      return "RTC_SW_SYS_RESET";
    case 5:
      return "DEEPSLEEP_RESET";
    case 7:
      return "TG0WDT_SYS_RESET";
    case 8:
      return "TG1WDT_SYS_RESET";
    case 9:
      return "RTCWDT_SYS_RESET";
    case 10:
      return "INTRUSION_RESET";
    case 11:
      return "TG0WDT_CPU_RESET";
    case 12:
      return "RTC_SW_CPU_RESET";
    case 13:
      return "RTCWDT_CPU_RESET";
    case 15:
      return "RTCWDT_BROWN_OUT_RESET";
    case 16:
      return "RTCWDT_RTC_RESET";
    case 17:
      return "TG1WDT_CPU_RESET";
    case 18:
      return "SUPER_WDT_RESET";
    case 19:
      return "GLITCH_RTC_RESET";
    case 20:
      return "EFUSE_RESET";
    case 21:
      return "USB_UART_CHIP_RESET";
    case 22:
      return "USB_JTAG_CHIP_RESET";
    case 23:
      return "POWER_GLITCH_RESET";
    default:
      return "NO_MEAN";
  }
}

static const char* controlWorkerStateToTelemetry(ControlWorkerState state) {
  switch (state) {
    case ControlWorkerState::PanelsDownloading:
      return "panels_downloading";
    case ControlWorkerState::PanelsValidating:
      return "panels_validating";
    case ControlWorkerState::FetchingFirmware:
      return "fetching_firmware";
    case ControlWorkerState::AwaitingOtaResult:
      return "awaiting_ota_result";
    case ControlWorkerState::ProvisioningPending:
      return "provisioning_pending";
    case ControlWorkerState::Failed:
      return "failed";
    case ControlWorkerState::Idle:
    default:
      return "idle";
  }
}

static const char* panelsWorkerStateToTelemetry(PanelsWorkerState state) {
  switch (state) {
    case PanelsWorkerState::PendingBatch:
      return "pending_batch";
    case PanelsWorkerState::Decoding:
      return "decoding";
    case PanelsWorkerState::Presenting:
      return "presenting";
    case PanelsWorkerState::Cancelled:
      return "cancelled";
    case PanelsWorkerState::Failed:
      return "failed";
    case PanelsWorkerState::Idle:
    default:
      return "idle";
  }
}

static const char* slowCommandKindToTelemetry(SlowCommandKind kind) {
  switch (kind) {
    case SlowCommandKind::EnterProvisioning:
      return "enter_provisioning";
    case SlowCommandKind::UpdateFirmware:
      return "update_firmware";
    case SlowCommandKind::QueuePanelsBatch:
      return "queue_panels_batch";
    case SlowCommandKind::None:
    default:
      return "";
  }
}

static uint32_t currentControlQueueDepth() {
  uint32_t depth = 0;
  if (gControlCommandQueue != nullptr) {
    depth = static_cast<uint32_t>(uxQueueMessagesWaiting(gControlCommandQueue));
  }

  if (gDeferredControlCommand != nullptr) {
    depth++;
  }

  return depth;
}

uint32_t elapsedMicrosSince(uint32_t startUs) {
  return static_cast<uint32_t>(micros() - startUs);
}

static bool isWifiStationModeEnabled() {
  const uint8_t modeBits = static_cast<uint8_t>(WiFi.getMode());
  return (modeBits & static_cast<uint8_t>(WIFI_MODE_STA)) != 0u;
}

static bool startSavedWifiReconnectAttempt(bool& restartedSta) {
  restartedSta = false;
  WiFi.setAutoReconnect(true);
  if (!isWifiStationModeEnabled()) {
    WiFi.mode(WIFI_STA);
    restartedSta = true;
  }

  if (WiFi.reconnect()) {
    return true;
  }

  WiFi.mode(WIFI_STA);
  restartedSta = true;
  return WiFi.begin() != WL_CONNECT_FAILED;
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
  gPerfLastReportPanelsDecodeMaxUs = gPerfPanelsDecodeMaxUs;
  gPerfLastReportPanelsPresentMaxUs = gPerfPanelsPresentMaxUs;

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

  if (kPanelsPerfLoggingEnabled && gPanelsBatchTaskHandle != nullptr) {
    Serial.printf(
        "[perf/panels] decode_max_us=%lu present_max_us=%lu\n",
        static_cast<unsigned long>(gPerfPanelsDecodeMaxUs),
        static_cast<unsigned long>(gPerfPanelsPresentMaxUs));
  }

  gPerfLoopMaxUs = 0;
  gPerfNetworkMaxUs = 0;
  gPerfRenderMaxUs = 0;
  gPerfSerialMaxUs = 0;
  gPerfPanelsDecodeMaxUs = 0;
  gPerfPanelsPresentMaxUs = 0;
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
  telemetry["deviceMac"] = WiFi.macAddress();
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
  telemetry["visualUdpSupported"] = true;
  telemetry["visualUdpPort"] = kVisualUdpPort;
  telemetry["visualUdpMode"] = "bins128";
  telemetry["sessionMode"] = clientSessionModeName(gSessionShadowState.mode);
  if (hasActiveClientOwner(now)) {
    telemetry["sessionActiveClientId"] = gSessionShadowState.activeClientId;
    telemetry["sessionActiveOwnerEpoch"] = gSessionShadowState.activeOwnerEpoch;
    telemetry["sessionOwnerLeaseRemainingMs"] =
        static_cast<uint32_t>(gSessionShadowState.activeOwnerExpiresAtMs - now);
  }
  const bool sessionLockHeld = gSessionShadowState.lock.held
      && gSessionShadowState.lock.expiresAtMs > now;
  telemetry["sessionLockHeld"] = sessionLockHeld;
  if (sessionLockHeld) {
    telemetry["sessionLockClientId"] = gSessionShadowState.lock.clientId;
    telemetry["sessionLockReason"] = gSessionShadowState.lock.reason;
    telemetry["sessionLockLeaseRemainingMs"] =
        static_cast<uint32_t>(gSessionShadowState.lock.expiresAtMs - now);
  }
  telemetry["sessionFallbackState"] = clientSessionFallbackStateName();
  telemetry["resetReason"] = resetReasonCodeToString(gResetReasonCode);
  telemetry["controlQueueDepth"] = currentControlQueueDepth();
  telemetry["controlWorkerState"] = controlWorkerStateToTelemetry(gControlWorkerState);
  telemetry["panelsWorkerState"] = panelsWorkerStateToTelemetry(gPanelsWorkerState);
  if (gLastSlowCommand != SlowCommandKind::None) {
    telemetry["lastSlowCommand"] = slowCommandKindToTelemetry(gLastSlowCommand);
    telemetry["lastSlowCommandDurationMs"] = gLastSlowCommandDurationMs;
  }
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

bool applyStreamBinaryFrame(const uint8_t* payload, size_t len, bool cancelPanelsPlaybackForStream) {
  if (payload == nullptr || len < 2) {
    registerInvalidStreamFrame("payload_short");
    return false;
  }

  const uint8_t streamVersion = payload[0];
  const bool legacyStream = streamVersion == kStreamVersion;
  const bool ownedStream = streamVersion == kStreamVersionOwned;
  if (!legacyStream && !ownedStream) {
    registerInvalidStreamFrame("version_invalid");
    return false;
  }

  uint32_t frameSequence = 0;
  const bool hasSequence = len >= 6;
  if (hasSequence) {
    frameSequence = static_cast<uint32_t>(payload[2]) |
                    (static_cast<uint32_t>(payload[3]) << 8) |
                    (static_cast<uint32_t>(payload[4]) << 16) |
                    (static_cast<uint32_t>(payload[5]) << 24);
  }

  uint32_t ownerEpoch = 0;
  const bool hasOwnerEpoch = ownedStream && len >= 10;
  if (hasOwnerEpoch) {
    ownerEpoch = static_cast<uint32_t>(payload[6]) |
                 (static_cast<uint32_t>(payload[7]) << 8) |
                 (static_cast<uint32_t>(payload[8]) << 16) |
                 (static_cast<uint32_t>(payload[9]) << 24);
  }

  if (!shouldAcceptOwnedStreamFrame(ownerEpoch, hasOwnerEpoch, millis())) {
    registerInvalidStreamFrame(hasOwnerEpoch ? "owner_epoch_stale" : "owner_epoch_missing");
    return false;
  }

  const uint8_t messageType = payload[1];
  if (messageType == kStreamBinsMessageType) {
    const size_t expectedSize = ownedStream ? kStreamFrameV3Size : kStreamFrameSize;
    if (len < expectedSize) {
      registerInvalidStreamFrame("bins_short");
      return false;
    }

    if (cancelPanelsPlaybackForStream) {
      cancelPanelsBatchPlayback();
    }

    gStreamFramesReceived++;
    if (hasSequence) {
      if (gHasStreamLastSequence && frameSequence > gStreamLastSequence + 1u) {
        gStreamSequenceGapCount += frameSequence - (gStreamLastSequence + 1u);
      }

      gStreamLastSequence = frameSequence;
      gHasStreamLastSequence = true;
    }

    const uint8_t levelOffset = ownedStream ? 18u : 14u;
    const uint8_t binsOffset = ownedStream ? 19u : 15u;
    const uint8_t brightnessOffset = ownedStream ? 147u : 143u;
    const uint8_t flagsOffset = ownedStream ? 148u : 144u;
    const uint8_t nextBinsIndex = static_cast<uint8_t>(gBinsActiveIndex ^ 1u);
    portENTER_CRITICAL(&gStreamBufferMux);
    gLevel = payload[levelOffset];
    memcpy(gBinsBuffers[nextBinsIndex], payload + binsOffset, kBinsCount);
    gBinsActiveIndex = nextBinsIndex;
    gStreamBrightness = payload[brightnessOffset];
    gBinsFlags = payload[flagsOffset];
    portEXIT_CRITICAL(&gStreamBufferMux);
    gFrameModeActive = false;
    gLastFrameMs = millis();
    gMatrixSignalTimedOut = false;
    if (ownedStream) {
      noteAcceptedOwnedStreamFrame(ownerEpoch, ClientSessionMode::Visualizer, gLastFrameMs);
    }
    markMatrixFrameDirty(true);
    return true;
  }

  if (messageType == kStreamFrame128x64Rgb565MessageType) {
    const size_t expectedSize = ownedStream ? kStreamFrameV3Frame128x64Rgb565Size : kStreamFrame128x64Rgb565Size;
    if (len < expectedSize) {
      registerInvalidStreamFrame("frame_short");
      return false;
    }

    if (cancelPanelsPlaybackForStream) {
      cancelPanelsBatchPlayback();
    }

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
    const uint8_t brightnessOffset = ownedStream ? 18u : 14u;
    const uint8_t frameOffset = ownedStream ? 19u : 15u;
    gStreamBrightness = payload[brightnessOffset];
    // Payload is already little-endian and ESP32 is little-endian, so we can bulk copy.
    memcpy(frameBackBuffer, payload + frameOffset, static_cast<size_t>(kMatrixPixelCount) * sizeof(uint16_t));

    portENTER_CRITICAL(&gStreamBufferMux);
    gFrameRgb565ActiveIndex = nextFrameIndex;
    portEXIT_CRITICAL(&gStreamBufferMux);

    gFrameModeActive = true;
    gLastFrameMs = millis();
    gMatrixSignalTimedOut = false;
    if (ownedStream) {
      noteAcceptedOwnedStreamFrame(ownerEpoch, ClientSessionMode::Visualizer, gLastFrameMs);
    }
    markMatrixFrameDirty(true);
    return true;
  }

  registerInvalidStreamFrame("message_type_unknown");
  return false;
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

  (void)enqueueIncomingControlCommand(ControlCommandSource::Mqtt, payload, length);
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
    (void)applyStreamBinaryFrame(payload, len, true);
    return;
  }

  if (type != WStype_TEXT || payload == nullptr || len == 0) {
    return;
  }

  (void)enqueueIncomingControlCommand(ControlCommandSource::WebSocket, payload, len);
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
  resetTaskWatchdog();
  gWs.begin(gServerHost.c_str(), gServerPort, path.c_str());
  gWs.onEvent(onWsEvent);
  gWs.setReconnectInterval(kWsAutoReconnectIntervalMs);
  resetTaskWatchdog();
  Serial.println("[ws] begin concluido.");
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
  gMqtt.setSocketTimeout(kMqttConnectSocketTimeoutSeconds);

  String presenceTopic = buildDeviceMqttTopic("presence");
  String offlinePayload = buildPresencePayload("offline");
  Serial.printf("[mqtt] conectando em mqtt://%s:%u (%s)\n", gMqttHost.c_str(), gMqttPort, gMqttRootTopic.c_str());

  resetTaskWatchdog();
  const bool connected = gMqtt.connect(
      gDeviceId.c_str(),
      gDeviceId.c_str(),
      gToken.c_str(),
      presenceTopic.c_str(),
      1,
      true,
      offlinePayload.c_str());
  resetTaskWatchdog();

  if (!connected) {
    gMqttDisconnectedSinceMs = millis();
    Serial.printf("[mqtt] falha ao conectar state=%d\n", gMqtt.state());
    return;
  }

  gMqttDisconnectedSinceMs = 0;
  (void)gMqtt.subscribe(buildDeviceMqttTopic("commands").c_str(), 1);
  (void)publishPresence("online");
  (void)publishDeviceLog("info", "mqtt", "connected", "Controle MQTT conectado.", false);
  (void)publishDeviceStats();
  (void)publishClientSessionShadow(millis(), true);
  sendTelemetry(true);
  publishPendingOtaReportIfNeeded();
  Serial.println("[mqtt] conectado.");
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
    stopVisualUdpReceiver();
    const bool provisioningIncomplete = isProvisioningIncomplete();
    const bool savedWifiConfigured = prefsGetBoolOrDefault("wifiConfigured", false);
    const bool canUseSavedWifi = !provisioningIncomplete || savedWifiConfigured;
    if (shouldRunNetworkStep(gWifiDisconnectedSinceMs == 0)) {
      gWifiDisconnectedSinceMs = millis();
      gLastWifiReconnectAttemptMs = canUseSavedWifi ? gWifiDisconnectedSinceMs : 0;
      if (!canUseSavedWifi) {
        setConnectivityState(kWifiStateDisconnected, "wifi_disconnected", true);
        Serial.println("[wifi] desconectado, aguardando reconexao.");
      } else {
        setConnectivityState(kWifiStateDisconnected, "wifi_waiting_saved_config", true);
        Serial.println("[wifi_waiting_saved_config] credenciais salvas presentes; aguardando reconexao.");
      }
      finishNetworkStep();
    }

    const bool shouldRetrySavedWifi =
        canUseSavedWifi
        && !gProvisioningPortalActive
        && !isProvisioningPortalLaunchPending()
        && gLastWifiReconnectAttemptMs != 0
        && (millis() - gLastWifiReconnectAttemptMs) >= kWifiReconnectRetryMs;
    if (shouldRunNetworkStep(shouldRetrySavedWifi)) {
      bool restartedSta = false;
      const bool reconnectStarted = startSavedWifiReconnectAttempt(restartedSta);
      gLastWifiReconnectAttemptMs = millis();
      setConnectivityState(kWifiStateDisconnected, "wifi_reconnect_retry");
      Serial.printf(
          "[wifi_reconnect_retry] reconnect_started=%s sta_restarted=%s\n",
          reconnectStarted ? "true" : "false",
          restartedSta ? "true" : "false");
      finishNetworkStep();
    }

    const bool shouldStartProvisioningFallback =
        provisioningIncomplete
        && !savedWifiConfigured
        && !gProvisioningPortalActive
        && !isProvisioningPortalLaunchPending()
        && gWifiDisconnectedSinceMs != 0
        && (millis() - gWifiDisconnectedSinceMs) > kWifiDisconnectProvisioningFallbackMs;
    if (shouldRunNetworkStep(shouldStartProvisioningFallback)) {
      const bool requested = requestProvisioningPortalLaunch("", false, "wifi_disconnected_fallback", true);
      if (requested) {
        Serial.println("[wifi] fallback para provisioning agendado apos queda prolongada.");
        gWifiDisconnectedSinceMs = 0;
        gLastWifiReconnectAttemptMs = 0;
      }

      finishNetworkStep();
    }
  } else {
    const bool wifiWasDisconnected = gWifiDisconnectedSinceMs != 0;
    if (wifiWasDisconnected) {
      gWifiDisconnectedSinceMs = 0;
      gLastWifiReconnectAttemptMs = 0;
    }

    if (shouldRunNetworkStep(gProvisioningPortalActive)) {
      setProvisioningPortalActive(false, "portal_closed");
      finishNetworkStep();
    }

    if (shouldRunNetworkStep(true)) {
      if (wifiWasDisconnected) {
        Serial.println("[wifi_reconnected] Wi-Fi reconectado.");
        setConnectivityState(kWifiStateConnected, "wifi_reconnected", true);
      } else {
        setConnectivityState(kWifiStateConnected, "wifi_connected");
      }
      finishNetworkStep();
    }
    if (shouldRunNetworkStep(true)) {
      processLanDiscovery();
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
      processVisualUdpReceiver();
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
