#include <Arduino.h>
#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <Preferences.h>
#include <PubSubClient.h>
#include <WebSocketsClient.h>
#include <WiFi.h>
#include <WiFiManager.h>
#include <esp_heap_caps.h>
#include <soc/soc_caps.h>
#include "firmware_version.h"

#if __has_include("firmware_version.auto.h")
#include "firmware_version.auto.h"
#endif

#ifndef MICA_FIRMWARE_VERSION
#define MICA_FIRMWARE_VERSION MICA_FIRMWARE_VERSION_FALLBACK
#endif

#if defined(MICA_PROFILE_DMA_EXP)
#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>
#endif

namespace {
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md
constexpr uint8_t kBinsCount = MICA_STREAM_BINS;
constexpr size_t kStreamFrameSize = 145;
constexpr size_t kStreamFrame128x64Rgb565Size = 16400;
constexpr uint8_t kStreamVersion = 2;
constexpr uint8_t kStreamBinsMessageType = 1;
constexpr uint8_t kStreamFrame128x64Rgb565MessageType = 2;
constexpr unsigned long kWifiDisconnectProvisioningFallbackMs = 20000;
constexpr unsigned long kMqttReconnectRetryMs = 5000;
constexpr unsigned long kWsReconnectRetryMs = 60000;
constexpr unsigned long kWsAutoReconnectIntervalMs = 2000;
constexpr unsigned long kWsFlapReportWindowMs = 60000;
constexpr uint8_t kWsFlapReportThreshold = 3;
constexpr unsigned long kTelemetryIntervalMs = 2000;
constexpr unsigned long kSerialHelloIntervalMs = 3000;
constexpr size_t kSerialInputMaxLength = 1024;
constexpr unsigned long kWifiConnectAttemptTimeoutMs = 20000;
constexpr uint16_t kDefaultMqttPort = 5273;
constexpr const char* kDefaultMqttRootTopic = "mica/v1/devices";
constexpr uint16_t kMqttPacketBufferBytes = 32768;
constexpr uint8_t kMatrixWidth = MICA_MATRIX_WIDTH;
constexpr uint8_t kMatrixHeight = MICA_MATRIX_HEIGHT;
constexpr uint8_t kMatrixHalfHeight = kMatrixHeight / 2;
constexpr size_t kMatrixPixelCount = static_cast<size_t>(kMatrixWidth) * static_cast<size_t>(kMatrixHeight);
static_assert((kMatrixHeight % 2) == 0, "MICA_MATRIX_HEIGHT must be even.");

constexpr const char* kBoardModel = "esp32s3_devkitc1";
constexpr const char* kBoardDisplayName = "ESP32-S3 DevKitC-1";
constexpr uint8_t kMatrixRgbPins[6] = {4, 5, 6, 7, 15, 16};
constexpr uint8_t kMatrixAddrPins[5] = {18, 8, 3, 42, 41};
constexpr uint8_t kMatrixClockPin = 41;
constexpr uint8_t kMatrixLatchPin = 40;
constexpr uint8_t kMatrixOePin = 2;
constexpr uint8_t kSerialRxPin = 44;
constexpr uint8_t kSerialTxPin = 43;

constexpr const char* kPanelType = "hub75_p2_5_128x64_smd2121_scan32";
#ifndef MICA_TEST_LED_GPIO
#define MICA_TEST_LED_GPIO -1
#endif
constexpr int kTestLedPin = MICA_TEST_LED_GPIO;
#if defined(RGB_BUILTIN)
constexpr int kOnboardTestLedPin = RGB_BUILTIN;
#elif defined(PIN_NEOPIXEL)
constexpr int kOnboardTestLedPin = PIN_NEOPIXEL;
#else
constexpr int kOnboardTestLedPin = -1;
#endif

constexpr unsigned long kTestLedDurationMs = 1500;
constexpr unsigned long kTestLedTogglePeriodMs = 120;
constexpr uint8_t kTestLedPwmChannel = 0;
constexpr uint16_t kTestLedPwmFrequencyHz = 5000;
constexpr uint8_t kTestLedPwmResolutionBits = 8;

constexpr const char* kFirmwareProfile = "dma_exp";
constexpr const char* kFirmwareVersion = MICA_FIRMWARE_VERSION;
constexpr uint8_t kBrightnessSafeMin = 30;
constexpr uint8_t kBrightnessSafeMax = 160;
constexpr uint8_t kBrightnessDefaultCap = 160;
constexpr const char* kWifiStateConnecting = "connecting";
constexpr const char* kWifiStateConnected = "connected";
constexpr const char* kWifiStatePortal = "portal";
constexpr const char* kWifiStateDisconnected = "disconnected";
constexpr const char* kSerialProvisioningProtocol = "mica.serial.v1";

#if defined(MICA_SECURITY_PROFILE_RELEASE)
constexpr const char* kSecurityProfile = "release";
#else
constexpr const char* kSecurityProfile = "dev";
#endif

Preferences gPrefs;
WebSocketsClient gWs;
WiFiClient gMqttNetClient;
PubSubClient gMqtt(gMqttNetClient);
String gServerHost;
uint16_t gServerPort = 5272;
String gMqttHost;
uint16_t gMqttPort = kDefaultMqttPort;
String gMqttRootTopic = kDefaultMqttRootTopic;
String gDeviceId;
String gToken;
String gActiveAppId;
String gActiveAppName;
String gActiveAppConfig;
uint8_t gBins[kBinsCount] = {0};
uint8_t gLevel = 0;
uint8_t gStreamBrightness = 255;
uint8_t gBrightnessCap = kBrightnessDefaultCap;
uint16_t gFrameRgb565[kMatrixPixelCount] = {0};
unsigned long gLastFrameMs = 0;
unsigned long gWsDisconnectedSinceMs = 0;
unsigned long gMqttDisconnectedSinceMs = 0;
unsigned long gWifiDisconnectedSinceMs = 0;
unsigned long gLastTelemetryMs = 0;
unsigned long gTestLedUntilMs = 0;
unsigned long gTestLedNextToggleMs = 0;
bool gTestLedState = false;
bool gTestLedEnabled = false;
bool gTestLedPwmReady = false;
bool gAuxLedAvailable = false;
bool gOnboardTestLedAvailable = false;
uint8_t gTestLedDuty = 0;
uint8_t gTestLedPulseDuty = 0;
bool gMatrixReady = false;
uint8_t gAppliedBrightness = 255;
bool gFrameModeActive = false;
uint64_t gLoopWorkTimeUs = 0;
unsigned long gLoopWindowStartMs = 0;
uint8_t gLoopLoadPercent = 0;
uint32_t gTelemetrySequence = 0;
uint32_t gDeviceLogSequence = 0;
bool gHasStreamLastSequence = false;
uint32_t gStreamLastSequence = 0;
uint32_t gStreamFramesReceived = 0;
uint32_t gStreamFramesApplied = 0;
uint32_t gStreamSequenceGapCount = 0;
uint32_t gStreamInvalidFrameCount = 0;
bool gProvisioningPortalActive = false;
String gWifiState = kWifiStateConnecting;
String gLastWifiEvent = "boot";
String gAuxLedUnavailableReason;
String gSerialInputBuffer;
unsigned long gLastSerialHelloMs = 0;
unsigned long gWsFlapWindowStartMs = 0;
uint16_t gWsConnectCountInWindow = 0;
uint16_t gWsDisconnectCountInWindow = 0;

void connectWebSocket();
void connectMqtt();
void handleControlCommandMessage(const JsonDocument& control);

#if defined(MICA_PROFILE_DMA_EXP)
MatrixPanel_I2S_DMA* gMatrix = nullptr;
#endif

struct RgbColor {
  uint8_t r;
  uint8_t g;
  uint8_t b;
};

RgbColor rainbowColorForColumn(uint16_t column, uint16_t columnCount) {
  if (columnCount <= 1) {
    return {255, 0, 0};
  }

  const uint8_t hue = static_cast<uint8_t>((column * 255u) / (columnCount - 1u));
  const uint8_t region = hue / 43u;
  const uint8_t remainder = static_cast<uint8_t>((hue - (region * 43u)) * 6u);
  const uint8_t q = static_cast<uint8_t>(255u - remainder);
  const uint8_t t = remainder;

  switch (region) {
    case 0:
      return {255, t, 0};
    case 1:
      return {q, 255, 0};
    case 2:
      return {0, 255, t};
    case 3:
      return {0, q, 255};
    case 4:
      return {t, 0, 255};
    default:
      return {255, 0, q};
  }
}

RgbColor rgb565ToRgb888(uint16_t rgb565) {
  const uint8_t r5 = static_cast<uint8_t>((rgb565 >> 11) & 0x1Fu);
  const uint8_t g6 = static_cast<uint8_t>((rgb565 >> 5) & 0x3Fu);
  const uint8_t b5 = static_cast<uint8_t>(rgb565 & 0x1Fu);

  const uint8_t r = static_cast<uint8_t>((static_cast<uint16_t>(r5) * 255u + 15u) / 31u);
  const uint8_t g = static_cast<uint8_t>((static_cast<uint16_t>(g6) * 255u + 31u) / 63u);
  const uint8_t b = static_cast<uint8_t>((static_cast<uint16_t>(b5) * 255u + 15u) / 31u);
  return {r, g, b};
}

bool isReservedHub75Pin(int pin) {
  for (uint8_t gpio : kMatrixRgbPins) {
    if (static_cast<int>(gpio) == pin) {
      return true;
    }
  }

  for (uint8_t gpio : kMatrixAddrPins) {
    if (static_cast<int>(gpio) == pin) {
      return true;
    }
  }

  return pin == static_cast<int>(kMatrixClockPin)
      || pin == static_cast<int>(kMatrixLatchPin)
      || pin == static_cast<int>(kMatrixOePin);
}

bool tryValidateAuxLedPin(int pin, String& reason) {
  if (pin < 0) {
    reason = "desabilitado por build flag";
    return false;
  }

  if (pin >= static_cast<int>(SOC_GPIO_PIN_COUNT)) {
    reason = "fora da faixa de GPIO fisico";
    return false;
  }

  if (isReservedHub75Pin(pin)) {
    reason = "conflito com pinos HUB75";
    return false;
  }

  if (pin == static_cast<int>(kSerialRxPin) || pin == static_cast<int>(kSerialTxPin)) {
    reason = "conflito com serial";
    return false;
  }

  return true;
}

void logConnectivityState(const char* eventOverride = nullptr) {
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
    const String& message,
    bool includeTelemetrySequence = true);

bool publishDeviceLog(
    const char* level,
    const char* category,
    const char* eventCode,
    const char* message,
    bool includeTelemetrySequence = true) {
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

void flushWsFlapDiagnostics(bool force = false) {
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

void setConnectivityState(const char* wifiState, const char* lastEvent, bool forceLog = false, bool publishEvent = true) {
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

void initializeOnboardTestLed() {
  gOnboardTestLedAvailable = false;

#if defined(RGB_BUILTIN) || defined(PIN_NEOPIXEL)
  if (kOnboardTestLedPin >= 0) {
    gOnboardTestLedAvailable = true;
    neopixelWrite(kOnboardTestLedPin, 0, 0, 0);
    Serial.printf("[led] LED onboard habilitado no pino %d.\n", kOnboardTestLedPin);
    return;
  }
#endif

  Serial.println("[led] LED onboard indisponivel neste build.");
}

void initializeAuxLed() {
  gAuxLedAvailable = false;
  gTestLedPwmReady = false;
  gTestLedDuty = 0;
  gAuxLedUnavailableReason = "";

  String validationReason;
  if (!tryValidateAuxLedPin(kTestLedPin, validationReason)) {
    gAuxLedUnavailableReason = validationReason;
    gTestLedEnabled = false;
    gPrefs.putBool("testLedEnabled", false);
    Serial.printf("[led] LED auxiliar indisponivel (GPIO %d): %s\n", kTestLedPin, gAuxLedUnavailableReason.c_str());
    return;
  }

  if (ledcSetup(kTestLedPwmChannel, kTestLedPwmFrequencyHz, kTestLedPwmResolutionBits) <= 0) {
    gAuxLedUnavailableReason = "falha no ledcSetup";
    gTestLedEnabled = false;
    gPrefs.putBool("testLedEnabled", false);
    Serial.printf("[led] Falha ao inicializar PWM do LED auxiliar (GPIO %d).\n", kTestLedPin);
    return;
  }

  pinMode(kTestLedPin, OUTPUT);
  ledcAttachPin(kTestLedPin, kTestLedPwmChannel);
  gTestLedPwmReady = true;
  gAuxLedAvailable = true;
  Serial.printf("[led] LED auxiliar habilitado no GPIO %d.\n", kTestLedPin);
}

bool isTestLedAvailable() {
  return gOnboardTestLedAvailable || gAuxLedAvailable;
}

uint8_t clampBrightnessToSafeRange(int value) {
  if (value < static_cast<int>(kBrightnessSafeMin)) {
    return kBrightnessSafeMin;
  }

  if (value > static_cast<int>(kBrightnessSafeMax)) {
    return kBrightnessSafeMax;
  }

  return static_cast<uint8_t>(value);
}

uint8_t resolveRequestedBrightness() {
  return clampBrightnessToSafeRange(static_cast<int>(gStreamBrightness));
}

uint8_t resolveAppliedBrightness() {
  const uint8_t requested = resolveRequestedBrightness();
  return (requested < gBrightnessCap) ? requested : gBrightnessCap;
}

void applyAuxTestLedDuty(uint8_t duty) {
  if (!gAuxLedAvailable || !gTestLedPwmReady) {
    return;
  }

  ledcWrite(kTestLedPwmChannel, duty);
}

void applyOnboardTestLedDuty(uint8_t duty) {
  if (!gOnboardTestLedAvailable) {
    return;
  }

#if defined(RGB_BUILTIN) || defined(PIN_NEOPIXEL)
  neopixelWrite(kOnboardTestLedPin, duty, duty, duty);
#else
  (void)duty;
#endif
}

void applyTestLedDutyToOutputs(uint8_t duty) {
  applyAuxTestLedDuty(duty);
  applyOnboardTestLedDuty(duty);
}

void applyTestLedState() {
  if (!isTestLedAvailable()) {
    return;
  }

  if (gTestLedUntilMs > 0) {
    applyTestLedDutyToOutputs(gTestLedState ? gTestLedPulseDuty : 0);
    return;
  }

  if (gAuxLedAvailable && gTestLedEnabled) {
    applyAuxTestLedDuty(gTestLedDuty);
  } else {
    applyAuxTestLedDuty(0);
  }

  applyOnboardTestLedDuty(0);
}

void updateTestLedDutyFromBrightness(uint8_t brightness) {
  if (!gAuxLedAvailable) {
    gTestLedDuty = 0;
  } else if (gTestLedDuty != brightness) {
    gTestLedDuty = brightness;
  }

  gTestLedPulseDuty = brightness;
  if (gTestLedUntilMs == 0) {
    applyTestLedState();
  }
}

void setMatrixBrightness(uint8_t brightness) {
  if (gAppliedBrightness == brightness) {
    return;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrixReady && gMatrix != nullptr) {
    gMatrix->setBrightness8(brightness);
  }
#endif

  gAppliedBrightness = brightness;
}

void clearMatrix() {
  if (!gMatrixReady) {
    return;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->clearScreen();
  }
#endif
}

void drawMatrixPixel(uint8_t x, uint8_t y, const RgbColor& color) {

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->drawPixelRGB888(x, y, color.r, color.g, color.b);
  }
#endif
}

void commitMatrixFrame() {
}

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#pontos-de-alteracao-frequente
bool initMatrixDisplay() {

#if defined(MICA_PROFILE_DMA_EXP)
  HUB75_I2S_CFG::i2s_pins pinMap = {
      static_cast<int8_t>(kMatrixRgbPins[0]),
      static_cast<int8_t>(kMatrixRgbPins[1]),
      static_cast<int8_t>(kMatrixRgbPins[2]),
      static_cast<int8_t>(kMatrixRgbPins[3]),
      static_cast<int8_t>(kMatrixRgbPins[4]),
      static_cast<int8_t>(kMatrixRgbPins[5]),
      static_cast<int8_t>(kMatrixAddrPins[0]),
      static_cast<int8_t>(kMatrixAddrPins[1]),
      static_cast<int8_t>(kMatrixAddrPins[2]),
      static_cast<int8_t>(kMatrixAddrPins[3]),
      static_cast<int8_t>(-1),
      static_cast<int8_t>(kMatrixLatchPin),
      static_cast<int8_t>(kMatrixOePin),
      static_cast<int8_t>(kMatrixClockPin)};

  HUB75_I2S_CFG config(kMatrixWidth, kMatrixHeight, 1, pinMap);
  config.i2sspeed = HUB75_I2S_CFG::HZ_10M;
  config.clkphase = false;

  gMatrix = new MatrixPanel_I2S_DMA(config);
  if (gMatrix == nullptr) {
    Serial.println("Falha ao alocar MatrixPanel_I2S_DMA.");
    return false;
  }

  if (!gMatrix->begin()) {
    Serial.println("Falha ao inicializar MatrixPanel_I2S_DMA.");
    delete gMatrix;
    gMatrix = nullptr;
    return false;
  }
#endif

  gMatrixReady = true;
  gAppliedBrightness = 0;
  setMatrixBrightness(resolveAppliedBrightness());
  updateTestLedDutyFromBrightness(gAppliedBrightness);
  clearMatrix();
  commitMatrixFrame();
  return true;
}

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
    int successFlag = -1) {
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
    const char* errorCode = nullptr) {
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

inline void accumulateLoopWorkTime(uint64_t& accumulator, uint32_t phaseStartUs) {
  accumulator += static_cast<uint64_t>(micros() - phaseStartUs);
}

void updateLoopLoadPercent(uint64_t loopWorkTimeUs) {
  gLoopWorkTimeUs += loopWorkTimeUs;

  const unsigned long nowMs = millis();
  if (gLoopWindowStartMs == 0) {
    gLoopWindowStartMs = nowMs;
    return;
  }

  const unsigned long windowElapsedMs = nowMs - gLoopWindowStartMs;
  if (windowElapsedMs < 1000) {
    return;
  }

  const uint64_t windowElapsedUs = static_cast<uint64_t>(windowElapsedMs) * 1000ULL;
  uint64_t loadPercent = 0;
  if (windowElapsedUs > 0) {
    loadPercent = (gLoopWorkTimeUs * 100ULL) / windowElapsedUs;
  }

  if (loadPercent > 100ULL) {
    loadPercent = 100ULL;
  }

  gLoopLoadPercent = static_cast<uint8_t>(loadPercent);
  gLoopWorkTimeUs = 0;
  gLoopWindowStartMs = nowMs;
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
  telemetry["loopLoadPercent"] = gLoopLoadPercent;
  telemetry["freeHeapBytes"] = freeHeapBytes;
  telemetry["streamFramesReceived"] = gStreamFramesReceived;
  telemetry["streamFramesApplied"] = gStreamFramesApplied;
  telemetry["streamSequenceGapCount"] = gStreamSequenceGapCount;
  telemetry["streamInvalidFrameCount"] = gStreamInvalidFrameCount;
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
  if (gActiveAppId.length() > 0) {
    telemetry["activeAppId"] = gActiveAppId;
  }
  if (gActiveAppName.length() > 0) {
    telemetry["activeAppName"] = gActiveAppName;
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

void clearTestLed() {
  if (!isTestLedAvailable()) {
    return;
  }

  gTestLedState = false;
  gTestLedUntilMs = 0;
  gTestLedNextToggleMs = 0;
  applyTestLedState();
}

void triggerTestLed() {
  if (!isTestLedAvailable()) {
    return;
  }

  gTestLedState = false;
  gTestLedPulseDuty = resolveAppliedBrightness();
  gTestLedUntilMs = millis() + kTestLedDurationMs;
  gTestLedNextToggleMs = 0;
  applyTestLedState();
}

void updateTestLed() {
  if (!isTestLedAvailable() || gTestLedUntilMs == 0) {
    return;
  }

  unsigned long now = millis();
  if (now >= gTestLedUntilMs) {
    clearTestLed();
    return;
  }

  if (gTestLedNextToggleMs == 0 || now >= gTestLedNextToggleMs) {
    gTestLedState = !gTestLedState;
    applyTestLedState();
    gTestLedNextToggleMs = now + kTestLedTogglePeriodMs;
  }
}

bool tryParseBooleanParameter(JsonVariantConst value, bool& output) {
  if (value.isNull()) {
    return false;
  }

  if (value.is<bool>()) {
    output = value.as<bool>();
    return true;
  }

  if (value.is<int>()) {
    output = value.as<int>() != 0;
    return true;
  }

  String raw = value.as<String>();
  raw.trim();
  raw.toLowerCase();
  if (raw == "1" || raw == "true" || raw == "on" || raw == "enabled") {
    output = true;
    return true;
  }

  if (raw == "0" || raw == "false" || raw == "off" || raw == "disabled") {
    output = false;
    return true;
  }

  return false;
}

void sendSerialJson(JsonDocument& document) {
  String payload;
  serializeJson(document, payload);
  Serial.println(payload);
}

void sendSerialHello() {
  JsonDocument hello;
  hello["type"] = "hello";
  hello["protocol"] = kSerialProvisioningProtocol;
  hello["deviceId"] = gDeviceId;
  hello["firmwareVersion"] = kFirmwareVersion;
  JsonArray caps = hello["capabilities"].to<JsonArray>();
  caps.add("provision");
  sendSerialJson(hello);
}

void sendSerialProgress(const String& requestId, const char* stage, const char* message) {
  if (requestId.length() == 0) {
    return;
  }

  JsonDocument progress;
  progress["type"] = "progress";
  progress["requestId"] = requestId;
  progress["stage"] = stage;
  progress["message"] = message;
  sendSerialJson(progress);
}

void sendSerialResult(
    const String& requestId,
    bool ok,
    const String& message,
    const char* errorCode = nullptr) {
  if (requestId.length() == 0) {
    return;
  }

  JsonDocument result;
  result["type"] = "result";
  result["requestId"] = requestId;
  result["ok"] = ok;
  result["message"] = message;
  if (errorCode != nullptr && errorCode[0] != '\0') {
    result["errorCode"] = errorCode;
  }

  if (!gDeviceId.isEmpty()) {
    result["deviceId"] = gDeviceId;
  }

  sendSerialJson(result);
}

bool tryParseServerBaseUrl(const String& rawBaseUrl, String& host, uint16_t& port) {
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

  int slashIndex = normalized.indexOf('/');
  if (slashIndex >= 0) {
    normalized = normalized.substring(0, slashIndex);
  }

  int colonIndex = normalized.lastIndexOf(':');
  if (colonIndex >= 0) {
    host = normalized.substring(0, colonIndex);
    String portRaw = normalized.substring(colonIndex + 1);
    int parsedPort = portRaw.toInt();
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

String buildServerBaseUrl(const String& host, uint16_t port) {
  if (host.length() == 0) {
    return "";
  }

  const uint16_t resolvedPort = port == 0 ? 5272 : port;
  return "http://" + host + ":" + String(resolvedPort);
}

bool tryApplyProvisioningPortalServer(
    const String& rawServerBaseUrl,
    const String& savedHost,
    uint16_t savedPort,
    String& errorCode,
    String& errorMessage) {
  String normalizedServerBaseUrl = rawServerBaseUrl;
  normalizedServerBaseUrl.trim();

  if (normalizedServerBaseUrl.length() == 0) {
    if (savedHost.length() > 0 && savedPort != 0) {
      gServerHost = savedHost;
      gServerPort = savedPort;
      gMqttHost = gServerHost;
      gMqttPort = kDefaultMqttPort;
      gMqttRootTopic = kDefaultMqttRootTopic;
      persistMqttConfig();
      setConnectivityState(kWifiStateConnected, "portal_server_empty_kept_saved", true);
      Serial.printf("[provisioning] campo Servidor vazio; mantendo configuracao salva %s.\n",
          buildServerBaseUrl(gServerHost, gServerPort).c_str());
      return true;
    }

    errorCode = "portal_server_missing";
    errorMessage = "Campo Servidor vazio e sem configuracao salva.";
    setConnectivityState(kWifiStateConnected, "portal_server_missing", true);
    Serial.println("[provisioning] campo Servidor vazio e sem configuracao salva.");
    return false;
  }

  String parsedHost;
  uint16_t parsedPort = 0;
  if (!tryParseServerBaseUrl(normalizedServerBaseUrl, parsedHost, parsedPort)) {
    if (savedHost.length() > 0 && savedPort != 0) {
      gServerHost = savedHost;
      gServerPort = savedPort;
      gMqttHost = gServerHost;
      gMqttPort = kDefaultMqttPort;
      gMqttRootTopic = kDefaultMqttRootTopic;
      persistMqttConfig();
      errorCode = "portal_server_invalid_kept_saved";
      errorMessage = "Campo Servidor invalido; mantendo configuracao salva.";
      setConnectivityState(kWifiStateConnected, "portal_server_invalid_kept_saved", true);
      Serial.printf("[provisioning] campo Servidor invalido '%s'; mantendo configuracao salva %s.\n",
          normalizedServerBaseUrl.c_str(),
          buildServerBaseUrl(gServerHost, gServerPort).c_str());
      return true;
    }

    errorCode = "portal_server_invalid";
    errorMessage = "Campo Servidor invalido no portal.";
    setConnectivityState(kWifiStateConnected, "portal_server_invalid", true);
    Serial.printf("[provisioning] campo Servidor invalido '%s' e sem configuracao salva.\n", normalizedServerBaseUrl.c_str());
    return false;
  }

  gServerHost = parsedHost;
  gServerPort = parsedPort;
  gPrefs.putString("host", gServerHost);
  gPrefs.putString("port", String(gServerPort));
  gMqttHost = gServerHost;
  gMqttPort = kDefaultMqttPort;
  gMqttRootTopic = kDefaultMqttRootTopic;
  persistMqttConfig();
  setConnectivityState(kWifiStateConnected, "portal_server_configured", true);
  Serial.printf("[provisioning] servidor configurado manualmente: %s.\n", buildServerBaseUrl(gServerHost, gServerPort).c_str());
  return true;
}

bool connectWifiWithTimeout(const String& ssid, const String& password, unsigned long timeoutMs) {
  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid.c_str(), password.c_str());
  setConnectivityState(kWifiStateConnecting, "wifi_connecting", true);

  unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED) {
    if (millis() - start > timeoutMs) {
      return false;
    }

    delay(200);
  }

  gWifiDisconnectedSinceMs = 0;
  setConnectivityState(kWifiStateConnected, "wifi_connected", true);
  return true;
}

bool pairWithServer(const String& pairingCode, const String& deviceName, String& errorCode, String& errorMessage) {
  if (pairingCode.length() == 0) {
    setConnectivityState(kWifiStateConnected, "provisioned_without_pairing", true);
    return true;
  }

  HTTPClient http;
  String url = "http://" + gServerHost + ":" + String(gServerPort) + "/api/v1/pair";
  if (!http.begin(url)) {
    errorCode = "pair_http_begin_failed";
    errorMessage = "Nao foi possivel iniciar conexao HTTP para pareamento.";
    setConnectivityState(kWifiStateConnected, "pair_http_begin_failed", true);
    return false;
  }

  http.addHeader("Content-Type", "application/json");
  JsonDocument req;
  req["pairingCode"] = pairingCode;
  req["deviceName"] = deviceName;
  req["profile"] = kFirmwareProfile;
  req["firmwareVersion"] = kFirmwareVersion;
  req["boardModel"] = kBoardModel;
  req["panelType"] = kPanelType;

  String body;
  serializeJson(req, body);
  int code = http.POST(body);
  if (code >= 200 && code < 300) {
    JsonDocument resp;
    if (deserializeJson(resp, http.getString()) != DeserializationError::Ok) {
      errorCode = "pair_response_invalid";
      errorMessage = "Resposta de pareamento invalida.";
      http.end();
      return false;
    }

    gDeviceId = resp["deviceId"] | "";
    gToken = resp["token"] | "";
    if (gDeviceId.isEmpty() || gToken.isEmpty()) {
      errorCode = "pair_response_missing_auth";
      errorMessage = "Resposta de pareamento sem credenciais.";
      http.end();
      return false;
    }

    gPrefs.putString("deviceId", gDeviceId);
    gPrefs.putString("token", gToken);

    // Atualiza host/port a partir do httpBase retornado pelo servidor,
    // garantindo que o dispositivo use o IP real em vez de micaaudio.local.
    String httpBase = resp["httpBase"] | "";
    String parsedHost;
    uint16_t parsedPort = 0;
    if (httpBase.length() > 0 && tryParseServerBaseUrl(httpBase, parsedHost, parsedPort)) {
      gServerHost = parsedHost;
      gServerPort = parsedPort;
      gPrefs.putString("host", gServerHost);
      gPrefs.putString("port", String(gServerPort));
    }

    String mqttHost = resp["mqttHost"] | "";
    int mqttPort = resp["mqttPort"] | 0;
    String mqttRootTopic = resp["mqttRootTopic"] | "";
    gMqttHost = mqttHost.length() > 0 ? mqttHost : gServerHost;
    gMqttPort = mqttPort > 0 ? static_cast<uint16_t>(mqttPort) : kDefaultMqttPort;
    gMqttRootTopic = mqttRootTopic.length() > 0 ? mqttRootTopic : kDefaultMqttRootTopic;
    persistMqttConfig();

    setConnectivityState(kWifiStateConnected, "pair_success", true);
    http.end();
    return true;
  }

  errorCode = "pair_http_error";
  errorMessage = "Servidor rejeitou pareamento.";
  setConnectivityState(kWifiStateConnected, "pair_http_error", true);
  http.end();
  return false;
}

void handleSerialProvisioningLine(const String& line) {
  JsonDocument command;
  if (deserializeJson(command, line) != DeserializationError::Ok) {
    return;
  }

  String type = command["type"] | "";
  if (!type.equalsIgnoreCase("provision")) {
    return;
  }

  String requestId = command["requestId"] | "";
  String ssid = command["ssid"] | "";
  String password = command["password"] | "";
  String serverBaseUrl = command["serverBaseUrl"] | "";
  String pairCode = command["pairCode"] | "";

  if (requestId.length() == 0) {
    return;
  }

  if (ssid.length() == 0) {
    sendSerialResult(requestId, false, "SSID ausente no request de provisionamento.", "ssid_required");
    return;
  }

  String parsedHost;
  uint16_t parsedPort = 0;
  if (!tryParseServerBaseUrl(serverBaseUrl, parsedHost, parsedPort)) {
    sendSerialResult(requestId, false, "serverBaseUrl invalido.", "server_base_url_invalid");
    return;
  }

  sendSerialProgress(requestId, "wifi_connecting", "Conectando ao Wi-Fi.");
  if (!connectWifiWithTimeout(ssid, password, kWifiConnectAttemptTimeoutMs)) {
    sendSerialResult(requestId, false, "Falha ao conectar no Wi-Fi.", "wifi_connect_failed");
    return;
  }

  gServerHost = parsedHost;
  gServerPort = parsedPort;
  gPrefs.putString("host", gServerHost);
  gPrefs.putString("port", String(gServerPort));
  gMqttHost = gServerHost;
  gMqttPort = kDefaultMqttPort;
  gMqttRootTopic = kDefaultMqttRootTopic;
  persistMqttConfig();
  sendSerialProgress(requestId, "wifi_connected", "Wi-Fi conectado.");

  String deviceName = gPrefs.getString("name", kBoardDisplayName);
  String pairErrorCode;
  String pairErrorMessage;
  sendSerialProgress(requestId, "pairing", "Executando pareamento com o servidor.");
  if (!pairWithServer(pairCode, deviceName, pairErrorCode, pairErrorMessage)) {
    sendSerialResult(requestId, false, pairErrorMessage, pairErrorCode.c_str());
    return;
  }

  connectMqtt();
  connectWebSocket();
  sendTelemetry(true);
  sendSerialProgress(requestId, "done", "Provisionamento concluido.");
  sendSerialResult(requestId, true, "Provisionamento concluido com sucesso.");
}

void processSerialProvisioning() {
  const unsigned long now = millis();
  if (gLastSerialHelloMs == 0 || (now - gLastSerialHelloMs) >= kSerialHelloIntervalMs) {
    sendSerialHello();
    gLastSerialHelloMs = now;
  }

  while (Serial.available() > 0) {
    char character = static_cast<char>(Serial.read());
    if (character == '\r') {
      continue;
    }

    if (character == '\n') {
      String line = gSerialInputBuffer;
      gSerialInputBuffer = "";
      line.trim();
      if (line.length() > 0) {
        handleSerialProvisioningLine(line);
      }

      continue;
    }

    if (gSerialInputBuffer.length() < kSerialInputMaxLength) {
      gSerialInputBuffer += character;
    }
  }
}

// DOCS: docs/wiki/guides/setup-new-device.md#passos
bool startProvisioningPortal(const char* reason) {
  (void)publishDeviceLog(
      "warning",
      "portal",
      reason == nullptr ? "portal_open" : reason,
      String("Abrindo portal de provisioning. motivo=") + (reason == nullptr ? "-" : reason),
      false);
  disconnectMqtt(true);
  gWs.disconnect();
  gLastTelemetryMs = 0;
  setProvisioningPortalActive(true, reason);
  Serial.printf("[portal_open] motivo=%s\n", reason == nullptr ? "-" : reason);

  WiFiManager wm;
  wm.setConfigPortalBlocking(true);
  wm.setConfigPortalTimeout(0);

  String savedHost = gPrefs.getString("host", "");
  uint16_t savedPort = static_cast<uint16_t>(atoi(gPrefs.getString("port", "5272").c_str()));
  String savedServerBaseUrl = buildServerBaseUrl(savedHost, savedPort);

  WiFiManagerParameter pServer("server", "Servidor", savedServerBaseUrl.c_str(), 96);
  WiFiManagerParameter pPair("pair", "Codigo pareamento", "", 12);
  WiFiManagerParameter pName("name", "Nome dispositivo", gPrefs.getString("name", kBoardDisplayName).c_str(), 32);

  wm.addParameter(&pServer);
  wm.addParameter(&pPair);
  wm.addParameter(&pName);

  String apName = "MicaAudio-Setup-" + String((uint32_t)ESP.getEfuseMac(), HEX).substring(6);
  Serial.printf("[provisioning] AP=%s reason=%s\n", apName.c_str(), reason == nullptr ? "-" : reason);
  if (!wm.autoConnect(apName.c_str())) {
    setConnectivityState(kWifiStatePortal, "portal_error", true);
    Serial.println("[provisioning] autoConnect retornou false; portal permanece disponivel.");
    return false;
  }

  setProvisioningPortalActive(false, "wifi_connected");
  Serial.println("[portal_close] provisioning encerrado apos conexao Wi-Fi.");
  gWifiDisconnectedSinceMs = 0;
  setConnectivityState(kWifiStateConnected, "wifi_connected", true);

  gPrefs.putString("name", pName.getValue());

  String serverConfigErrorCode;
  String serverConfigErrorMessage;
  if (!tryApplyProvisioningPortalServer(pServer.getValue(), savedHost, savedPort, serverConfigErrorCode, serverConfigErrorMessage)) {
    Serial.printf("[provisioning] falha ao aplicar Servidor do portal: %s (%s)\n",
        serverConfigErrorMessage.c_str(),
        serverConfigErrorCode.c_str());
    return false;
  }

  String pairingCode = pPair.getValue();
  String pairErrorCode;
  String pairErrorMessage;
  if (!pairWithServer(pairingCode, pName.getValue(), pairErrorCode, pairErrorMessage)) {
    Serial.printf("[pair] falha no provisioning portal: %s (%s)\n", pairErrorMessage.c_str(), pairErrorCode.c_str());
  }

  return true;
}

void enterProvisioningMode(bool clearDeviceCredentials, const char* reason) {
  if (clearDeviceCredentials) {
    gPrefs.remove("deviceId");
    gPrefs.remove("token");
    gDeviceId = "";
    gToken = "";
  }

  (void)startProvisioningPortal(reason);
}

void handleControlCommandMessage(const JsonDocument& control) {
  const char *command = control["command"] | "";
  String commandId = control["commandId"] | "";
  JsonObjectConst parameters = control["parameters"].as<JsonObjectConst>();

  if (command != nullptr && command[0] != '\0') {
    (void)publishDeviceLog("info", "command", command, String("Comando recebido via MQTT: ") + command);
  }

  if (strcmp(command, "enter_provisioning") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    sendCommandProgress(commandId, 100, "enter-provisioning", "Entrando em provisioning.", 1);
    enterProvisioningMode(true, "command_enter_provisioning");
    return;
  }

  if (strcmp(command, "revoke_and_restart") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    sendCommandProgress(commandId, 100, "revoke-restart", "Reiniciando dispositivo.", 1);
    (void)publishPresence("offline");
    delay(50);
    gPrefs.remove("deviceId");
    gPrefs.remove("token");
    delay(200);
    ESP.restart();
    return;
  }

  if (strcmp(command, "test_led") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");

    JsonVariantConst enabledValue = parameters["enabled"];
    if (!enabledValue.isNull()) {
      bool enabled = false;
      if (!tryParseBooleanParameter(enabledValue, enabled)) {
        (void)publishDeviceLog("warning", "command", "test_led_invalid_enabled", "Parametro enabled invalido para test_led.");
        sendCommandProgress(commandId, 100, "invalid", "Parametro enabled invalido.", 0);
        return;
      }

      Serial.printf("[led] parametro legado enabled recebido: %s\n", enabled ? "true" : "false");
      if (gAuxLedAvailable) {
        gTestLedEnabled = enabled;
        gPrefs.putBool("testLedEnabled", gTestLedEnabled);
      } else {
        gTestLedEnabled = false;
        gPrefs.putBool("testLedEnabled", false);
      }

      updateTestLedDutyFromBrightness(resolveAppliedBrightness());
      clearTestLed();
      applyTestLedState();
      sendTelemetry(true);
      const char* message = gAuxLedAvailable
          ? (gTestLedEnabled ? "Compat legado: LED auxiliar habilitado." : "Compat legado: LED auxiliar desabilitado.")
          : "Compat legado: sem LED auxiliar, parametro enabled ignorado.";
      sendCommandProgress(commandId, 100, "set-test-led-compat", message, 1);
      return;
    }

    if (!isTestLedAvailable()) {
      (void)publishDeviceLog("warning", "command", "test_led_unavailable", "Nenhum LED de teste disponivel neste hardware.");
      sendCommandProgress(commandId, 100, "test-led-unavailable", "Nenhum LED de teste disponivel neste hardware.", 0);
      return;
    }

    triggerTestLed();
    sendCommandProgress(commandId, 100, "test-led", "Teste de LED acionado.", 1);
    return;
  }

  if (strcmp(command, "set_brightness") == 0) {
    String brightnessRaw = parameters["brightness"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (brightnessRaw.length() == 0) {
      (void)publishDeviceLog("warning", "command", "set_brightness_missing", "Parametro brightness ausente.");
      sendCommandProgress(commandId, 100, "invalid", "brightness ausente.", 0);
      return;
    }

    const int brightnessValue = brightnessRaw.toInt();
    if (brightnessValue == 0 && brightnessRaw != "0") {
      (void)publishDeviceLog("warning", "command", "set_brightness_invalid", "Parametro brightness invalido.");
      sendCommandProgress(commandId, 100, "invalid", "brightness invalido.", 0);
      return;
    }

    gBrightnessCap = clampBrightnessToSafeRange(brightnessValue);
    gPrefs.putUChar("brightnessCap", gBrightnessCap);
    setMatrixBrightness(resolveAppliedBrightness());
    updateTestLedDutyFromBrightness(gAppliedBrightness);
    applyTestLedState();
    sendTelemetry(true);

    sendCommandProgress(commandId, 100, "set-brightness", "Brilho atualizado.", 1);
    return;
  }

  if (strcmp(command, "install_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      (void)publishDeviceLog("warning", "command", "install_app_missing_appid", "appId ausente para install_app.");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    sendCommandProgress(commandId, 70, "install-app", "Salvando app...");
    gActiveAppId = appId;
    gActiveAppName = appName.length() > 0 ? appName : appId;
    gActiveAppConfig = configJson;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppName", gActiveAppName);
    gPrefs.putString("activeAppConfig", gActiveAppConfig);
    sendTelemetry(true);

    sendCommandProgress(commandId, 100, "install-app", "App instalado.", 1);
    return;
  }

  if (strcmp(command, "activate_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      (void)publishDeviceLog("warning", "command", "activate_app_missing_appid", "appId ausente para activate_app.");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    gActiveAppId = appId;
    gActiveAppName = appName.length() > 0 ? appName : appId;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppName", gActiveAppName);
    sendTelemetry(true);

    sendCommandProgress(commandId, 100, "activate-app", "App ativado.", 1);
    return;
  }

  if (strcmp(command, "set_app_config") == 0) {
    String appId = parameters["appId"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      (void)publishDeviceLog("warning", "command", "set_app_config_missing_appid", "appId ausente para set_app_config.");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    gActiveAppId = appId;
    gActiveAppConfig = configJson;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppConfig", gActiveAppConfig);
    sendTelemetry(true);

    sendCommandProgress(commandId, 100, "set-app-config", "Configuracao aplicada.", 1);
    return;
  }

  (void)publishDeviceLog("warning", "command", "unknown_command", String("Comando desconhecido: ") + command);
  sendCommandProgress(commandId, 100, "unknown", "Comando desconhecido.", 0);
}

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

      gStreamFramesReceived++;
      if (hasSequence) {
        if (gHasStreamLastSequence && frameSequence > gStreamLastSequence + 1u) {
          gStreamSequenceGapCount += frameSequence - (gStreamLastSequence + 1u);
        }

        gStreamLastSequence = frameSequence;
        gHasStreamLastSequence = true;
      }

      gLevel = payload[14];
      memcpy(gBins, payload + 15, kBinsCount);
      gStreamBrightness = payload[143];
      gFrameModeActive = false;
      gLastFrameMs = millis();
      gStreamFramesApplied++;
      return;
    }

    if (messageType == kStreamFrame128x64Rgb565MessageType) {
      if (len < kStreamFrame128x64Rgb565Size) {
        registerInvalidStreamFrame("frame_short");
        return;
      }

      gStreamFramesReceived++;
      if (hasSequence) {
        if (gHasStreamLastSequence && frameSequence > gStreamLastSequence + 1u) {
          gStreamSequenceGapCount += frameSequence - (gStreamLastSequence + 1u);
        }

        gStreamLastSequence = frameSequence;
        gHasStreamLastSequence = true;
      }

      gStreamBrightness = payload[14];
      size_t offset = 15;
      for (size_t i = 0; i < kMatrixPixelCount; i++) {
        gFrameRgb565[i] = static_cast<uint16_t>(payload[offset]) |
                          static_cast<uint16_t>(payload[offset + 1]) << 8;
        offset += 2;
      }

      gFrameModeActive = true;
      gLastFrameMs = millis();
      gStreamFramesApplied++;
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
}

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
void drawBars() {
  if (!gMatrixReady) {
    return;
  }

  setMatrixBrightness(resolveAppliedBrightness());
  updateTestLedDutyFromBrightness(gAppliedBrightness);
  clearMatrix();

  const uint16_t columnCount = (kBinsCount < kMatrixWidth) ? kBinsCount : kMatrixWidth;
  for (uint16_t x = 0; x < columnCount; x++) {
    const uint16_t binIndex = (x * kBinsCount) / columnCount;
    const uint8_t amplitude = gBins[binIndex];
    const uint8_t barHeight =
        static_cast<uint8_t>((static_cast<uint16_t>(amplitude) * kMatrixHalfHeight + 254u) / 255u);

    if (barHeight == 0) {
      continue;
    }

    const RgbColor color = rainbowColorForColumn(x, columnCount);
    for (uint8_t offset = 0; offset < barHeight; offset++) {
      const uint8_t topY = static_cast<uint8_t>((kMatrixHalfHeight - 1u) - offset);
      const uint8_t bottomY = static_cast<uint8_t>(kMatrixHalfHeight + offset);
      drawMatrixPixel(static_cast<uint8_t>(x), topY, color);

      if (bottomY < kMatrixHeight) {
        drawMatrixPixel(static_cast<uint8_t>(x), bottomY, color);
      }
    }
  }

  commitMatrixFrame();
}

void drawFrame128x64() {
  if (!gMatrixReady) {
    return;
  }

  setMatrixBrightness(resolveAppliedBrightness());
  updateTestLedDutyFromBrightness(gAppliedBrightness);
  clearMatrix();

  for (uint8_t y = 0; y < kMatrixHeight; y++) {
    for (uint8_t x = 0; x < kMatrixWidth; x++) {
      const size_t index = static_cast<size_t>(y) * static_cast<size_t>(kMatrixWidth) + static_cast<size_t>(x);
      const RgbColor color = rgb565ToRgb888(gFrameRgb565[index]);
      drawMatrixPixel(x, y, color);
    }
  }

  commitMatrixFrame();
}
}  // namespace

void setup() {
  Serial.begin(115200);
  Serial.println("[boot] inicializando firmware.");
  if (strcmp(kSecurityProfile, "dev") == 0) {
    Serial.printf("MicaAudio firmware board=%s profile=%s security=%s\\n", kBoardModel, kFirmwareProfile, kSecurityProfile);
  }

  gPrefs.begin("micaaudio", false);
  Serial.println("[wifi_connecting] preparando conectividade.");
  setConnectivityState(kWifiStateConnecting, "boot", true);

  gBrightnessCap = clampBrightnessToSafeRange(static_cast<int>(gPrefs.getUChar("brightnessCap", kBrightnessDefaultCap)));
  gTestLedEnabled = gPrefs.getBool("testLedEnabled", false);
  gStreamBrightness = gBrightnessCap;
  gAppliedBrightness = resolveAppliedBrightness();
  initializeOnboardTestLed();
  initializeAuxLed();

  if (!initMatrixDisplay()) {
    Serial.println("Painel HUB75 indisponivel: exibicao de barras desativada.");
  }

  updateTestLedDutyFromBrightness(resolveAppliedBrightness());
  applyTestLedState();

  gServerHost = gPrefs.getString("host", "");
  gServerPort = static_cast<uint16_t>(atoi(gPrefs.getString("port", "5272").c_str()));
  gMqttHost = gPrefs.getString("mqttHost", "");
  gMqttPort = static_cast<uint16_t>(atoi(gPrefs.getString("mqttPort", "5273").c_str()));
  gMqttRootTopic = gPrefs.getString("mqttRootTopic", kDefaultMqttRootTopic);
  normalizeMqttConfig();
  gDeviceId = gPrefs.getString("deviceId", "");
  gToken = gPrefs.getString("token", "");
  gActiveAppId = gPrefs.getString("activeAppId", "");
  gActiveAppName = gPrefs.getString("activeAppName", "");
  gActiveAppConfig = gPrefs.getString("activeAppConfig", "");

  const bool missingServerConfig = gServerHost.isEmpty() || gServerPort == 0;
  const bool missingDeviceCredentials = gDeviceId.isEmpty() || gToken.isEmpty();
  if (missingServerConfig || missingDeviceCredentials) {
    const char* bootReason = missingServerConfig
        ? "boot_missing_server_config"
        : "boot_missing_device_credentials";
    Serial.printf("[boot] configuracao incompleta; abrindo provisioning portal (%s).\n", bootReason);
    (void)startProvisioningPortal(bootReason);

    gServerHost = gPrefs.getString("host", "");
    gServerPort = static_cast<uint16_t>(atoi(gPrefs.getString("port", "5272").c_str()));
    gMqttHost = gPrefs.getString("mqttHost", "");
    gMqttPort = static_cast<uint16_t>(atoi(gPrefs.getString("mqttPort", "5273").c_str()));
    gMqttRootTopic = gPrefs.getString("mqttRootTopic", kDefaultMqttRootTopic);
    normalizeMqttConfig();
    gDeviceId = gPrefs.getString("deviceId", "");
    gToken = gPrefs.getString("token", "");
  }

  WiFi.mode(WIFI_STA);
  WiFi.begin();

  bool bootWifiConnected = false;
  if (!gServerHost.isEmpty() && gServerPort != 0) {
    unsigned long bootWifiWaitStart = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - bootWifiWaitStart) < 5000) {
      processSerialProvisioning();
      delay(120);
    }

    bootWifiConnected = WiFi.status() == WL_CONNECTED;
  }

  if (gServerHost.isEmpty() || gServerPort == 0 || gDeviceId.isEmpty() || gToken.isEmpty()) {
    Serial.println("[wifi_connecting] aguardando provisioning por AP para concluir configuracao.");
    setConnectivityState(kWifiStateDisconnected, "boot_missing_server_config", true);
    gWifiDisconnectedSinceMs = millis();
  } else if (bootWifiConnected) {
    Serial.println("[wifi_connected] Wi-Fi conectado no boot.");
    setConnectivityState(kWifiStateConnected, "wifi_connected", true);
    connectMqtt();
    connectWebSocket();
  } else {
    Serial.println("[wifi_connecting] sem Wi-Fi no boot, aguardando reconexao.");
    setConnectivityState(kWifiStateDisconnected, "boot_waiting_wifi", true);
    gWifiDisconnectedSinceMs = millis();
  }

  sendSerialHello();
}

void loop() {
  uint64_t loopWorkTimeUs = 0;
  uint32_t phaseStartUs = micros();
  processSerialProvisioning();
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  const bool wifiConnected = WiFi.status() == WL_CONNECTED;

  if (!wifiConnected) {
    if (gWifiDisconnectedSinceMs == 0) {
      gWifiDisconnectedSinceMs = millis();
      phaseStartUs = micros();
      setConnectivityState(kWifiStateDisconnected, "wifi_disconnected", true);
      accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
      Serial.println("[wifi] desconectado, aguardando reconexao.");
    }

    if (!gProvisioningPortalActive
        && (millis() - gWifiDisconnectedSinceMs) > kWifiDisconnectProvisioningFallbackMs) {
      Serial.println("[wifi] fallback para provisioning apos queda prolongada.");
      phaseStartUs = micros();
      (void)startProvisioningPortal("wifi_disconnected_fallback");
      gWifiDisconnectedSinceMs = 0;
      connectMqtt();
      connectWebSocket();
      accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
    } else {
      delay(120);
    }

    phaseStartUs = micros();
    processSerialProvisioning();
    accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
    phaseStartUs = micros();
    updateTestLed();
    accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
    updateLoopLoadPercent(loopWorkTimeUs);
    return;
  }

  if (gWifiDisconnectedSinceMs != 0) {
    gWifiDisconnectedSinceMs = 0;
  }

  if (gProvisioningPortalActive) {
    phaseStartUs = micros();
    setProvisioningPortalActive(false, "portal_closed");
    accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  }

  phaseStartUs = micros();
  setConnectivityState(kWifiStateConnected, "wifi_connected");
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  phaseStartUs = micros();
  gMqtt.loop();
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  phaseStartUs = micros();
  gWs.loop();
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  phaseStartUs = micros();
  flushWsFlapDiagnostics(false);
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);

  if (!gMqtt.connected()) {
    if (gMqttDisconnectedSinceMs == 0) {
      gMqttDisconnectedSinceMs = millis();
    }

    if (millis() - gMqttDisconnectedSinceMs >= kMqttReconnectRetryMs) {
      phaseStartUs = micros();
      connectMqtt();
      accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
      gMqttDisconnectedSinceMs = millis();
    }
  } else {
    gMqttDisconnectedSinceMs = 0;
    phaseStartUs = micros();
    sendTelemetry(false);
    accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  }

  if (!gWs.isConnected()) {
    if (gWsDisconnectedSinceMs == 0) {
      gWsDisconnectedSinceMs = millis();
      phaseStartUs = micros();
      setConnectivityState(kWifiStateConnected, "ws_disconnected", false, false);
      accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
    }

    if (millis() - gWsDisconnectedSinceMs > kWsReconnectRetryMs) {
      Serial.println("[ws_disconnected] sem sessao por tempo prolongado; tentando reconectar websocket.");
      phaseStartUs = micros();
      connectWebSocket();
      accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
      gWsDisconnectedSinceMs = millis();
    }
  } else {
    gWsDisconnectedSinceMs = 0;
  }

  if (millis() - gLastFrameMs > 15000) {
    phaseStartUs = micros();
    memset(gBins, 0, sizeof(gBins));
    gLevel = 0;
    memset(gFrameRgb565, 0, sizeof(gFrameRgb565));
    gFrameModeActive = false;
    accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  }

  phaseStartUs = micros();
  updateTestLed();
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  phaseStartUs = micros();
  processSerialProvisioning();
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);
  phaseStartUs = micros();
  if (gFrameModeActive) {
    drawFrame128x64();
  } else {
    drawBars();
  }
  accumulateLoopWorkTime(loopWorkTimeUs, phaseStartUs);

  updateLoopLoadPercent(loopWorkTimeUs);
}
