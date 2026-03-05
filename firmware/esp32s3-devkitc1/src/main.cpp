#include <Arduino.h>
#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <Preferences.h>
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
constexpr uint8_t kBinsCount = MICA_STREAM_BINS;
constexpr size_t kStreamFrameSize = 145;
constexpr size_t kStreamFrame128x64Rgb565Size = 16400;
constexpr uint8_t kStreamVersion = 2;
constexpr uint8_t kStreamBinsMessageType = 1;
constexpr uint8_t kStreamFrame128x64Rgb565MessageType = 2;
constexpr unsigned long kWifiDisconnectProvisioningFallbackMs = 20000;
constexpr unsigned long kWsReconnectRetryMs = 60000;
constexpr unsigned long kTelemetryIntervalMs = 2000;
constexpr unsigned long kSerialHelloIntervalMs = 3000;
constexpr size_t kSerialInputMaxLength = 1024;
constexpr unsigned long kWifiConnectAttemptTimeoutMs = 20000;
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
String gServerHost;
uint16_t gServerPort = 5272;
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
uint64_t gLoopBusyTimeUs = 0;
unsigned long gLoopWindowStartMs = 0;
uint8_t gLoopLoadPercent = 0;
uint32_t gTelemetrySequence = 0;
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

void connectWebSocket();

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

void setConnectivityState(const char* wifiState, const char* lastEvent, bool forceLog = false) {
  bool changed = false;
  if (wifiState != nullptr && wifiState[0] != '\0' && !gWifiState.equals(wifiState)) {
    gWifiState = wifiState;
    changed = true;
  }

  if (lastEvent != nullptr && lastEvent[0] != '\0' && !gLastWifiEvent.equals(lastEvent)) {
    gLastWifiEvent = lastEvent;
    changed = true;
  }

  if (changed || forceLog) {
    Serial.printf("[conn] wifiState=%s portal=%s event=%s\n",
        gWifiState.c_str(),
        gProvisioningPortalActive ? "on" : "off",
        gLastWifiEvent.c_str());
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

void sendCommandProgress(
    const String& commandId,
    uint8_t progressPercent,
    const char* stage,
    const String& message,
    int successFlag = -1) {
  if (commandId.isEmpty() || !gWs.isConnected()) {
    return;
  }

  JsonDocument progress;
  progress["type"] = "command_progress";
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

  String payload;
  serializeJson(progress, payload);
  gWs.sendTXT(payload);
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

void updateLoopLoadPercent(uint32_t loopStartUs) {
  const uint32_t nowUs = micros();
  const uint32_t elapsedUs = nowUs - loopStartUs;
  gLoopBusyTimeUs += static_cast<uint64_t>(elapsedUs);

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
    loadPercent = (gLoopBusyTimeUs * 100ULL) / windowElapsedUs;
  }

  if (loadPercent > 100ULL) {
    loadPercent = 100ULL;
  }

  gLoopLoadPercent = static_cast<uint8_t>(loadPercent);
  gLoopBusyTimeUs = 0;
  gLoopWindowStartMs = nowMs;
}

void sendTelemetry(bool force) {
  if (!gWs.isConnected() || gDeviceId.isEmpty()) {
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

  telemetry["type"] = "telemetry";
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

  String payload;
  serializeJson(telemetry, payload);
  gWs.sendTXT(payload);
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
  sendSerialProgress(requestId, "wifi_connected", "Wi-Fi conectado.");

  String deviceName = gPrefs.getString("name", kBoardDisplayName);
  String pairErrorCode;
  String pairErrorMessage;
  sendSerialProgress(requestId, "pairing", "Executando pareamento com o servidor.");
  if (!pairWithServer(pairCode, deviceName, pairErrorCode, pairErrorMessage)) {
    sendSerialResult(requestId, false, pairErrorMessage, pairErrorCode.c_str());
    return;
  }

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

bool startProvisioningPortal(const char* reason) {
  gWs.disconnect();
  gLastTelemetryMs = 0;
  setProvisioningPortalActive(true, reason);
  Serial.printf("[portal_open] motivo=%s\n", reason == nullptr ? "-" : reason);

  WiFiManager wm;
  wm.setConfigPortalBlocking(true);
  wm.setConfigPortalTimeout(0);

  WiFiManagerParameter pHost("host", "Servidor host", gPrefs.getString("host", "micaaudio.local").c_str(), 63);
  WiFiManagerParameter pPort("port", "Servidor porta", gPrefs.getString("port", "5272").c_str(), 8);
  WiFiManagerParameter pPair("pair", "Codigo pareamento", "", 12);
  WiFiManagerParameter pName("name", "Nome dispositivo", gPrefs.getString("name", kBoardDisplayName).c_str(), 32);

  wm.addParameter(&pHost);
  wm.addParameter(&pPort);
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

  gPrefs.putString("host", pHost.getValue());
  gPrefs.putString("port", pPort.getValue());
  gPrefs.putString("name", pName.getValue());
  gServerHost = pHost.getValue();
  gServerPort = static_cast<uint16_t>(atoi(pPort.getValue()));

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

// DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
void onWsEvent(WStype_t type, uint8_t *payload, size_t len) {
  if (type == WStype_CONNECTED) {
    gWsDisconnectedSinceMs = 0;
    setConnectivityState(kWifiStateConnected, "ws_connected", true);
    Serial.println("[ws_connected] sessao websocket estabelecida.");
    sendTelemetry(true);
    return;
  }

  if (type == WStype_DISCONNECTED) {
    setConnectivityState(WiFi.status() == WL_CONNECTED ? kWifiStateConnected : kWifiStateDisconnected, "ws_disconnected", true);
    Serial.println("[ws_disconnected] sessao websocket encerrada.");
    gLastTelemetryMs = 0;
    return;
  }

  if (type == WStype_BIN) {
    if (payload == nullptr || len < 2) {
      gStreamInvalidFrameCount++;
      return;
    }

    if (payload[0] != kStreamVersion) {
      gStreamInvalidFrameCount++;
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
        gStreamInvalidFrameCount++;
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
        gStreamInvalidFrameCount++;
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

    gStreamInvalidFrameCount++;

    return;
  }

  if (type != WStype_TEXT || payload == nullptr || len == 0) {
    return;
  }

  JsonDocument control;
  if (deserializeJson(control, payload, len) != DeserializationError::Ok) {
    return;
  }

  const char *command = control["command"] | "";
  String commandId = control["commandId"] | "";
  JsonObjectConst parameters = control["parameters"].as<JsonObjectConst>();

  if (strcmp(command, "enter_provisioning") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    postCommandAck(commandId, true, "Entrando em provisioning.", 100, "enter-provisioning");
    sendCommandProgress(commandId, 100, "enter-provisioning", "Entrando em provisioning.", 1);
    enterProvisioningMode(true, "command_enter_provisioning");
    return;
  }

  if (strcmp(command, "revoke_and_restart") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    postCommandAck(commandId, true, "Revogando e reiniciando.", 100, "revoke-restart");
    sendCommandProgress(commandId, 100, "revoke-restart", "Reiniciando dispositivo.", 1);
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
        postCommandAck(commandId, false, "Parametro enabled invalido.", 100, "invalid", "param_invalid");
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
      postCommandAck(commandId, true, message, 100, "set-test-led-compat");
      sendCommandProgress(commandId, 100, "set-test-led-compat", message, 1);
      return;
    }

    if (!isTestLedAvailable()) {
      postCommandAck(commandId, false, "Nenhum LED de teste disponivel neste hardware.", 100, "test-led-unavailable", "test_led_unavailable");
      sendCommandProgress(commandId, 100, "test-led-unavailable", "Nenhum LED de teste disponivel neste hardware.", 0);
      return;
    }

    triggerTestLed();
    postCommandAck(commandId, true, "Teste de LED acionado.", 100, "test-led");
    sendCommandProgress(commandId, 100, "test-led", "Teste de LED acionado.", 1);
    return;
  }

  if (strcmp(command, "set_brightness") == 0) {
    String brightnessRaw = parameters["brightness"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (brightnessRaw.length() == 0) {
      postCommandAck(commandId, false, "brightness ausente.", 100, "invalid", "brightness_invalid");
      sendCommandProgress(commandId, 100, "invalid", "brightness ausente.", 0);
      return;
    }

    const int brightnessValue = brightnessRaw.toInt();
    if (brightnessValue == 0 && brightnessRaw != "0") {
      postCommandAck(commandId, false, "brightness invalido.", 100, "invalid", "brightness_invalid");
      sendCommandProgress(commandId, 100, "invalid", "brightness invalido.", 0);
      return;
    }

    gBrightnessCap = clampBrightnessToSafeRange(brightnessValue);
    gPrefs.putUChar("brightnessCap", gBrightnessCap);
    setMatrixBrightness(resolveAppliedBrightness());
    updateTestLedDutyFromBrightness(gAppliedBrightness);
    applyTestLedState();
    sendTelemetry(true);

    postCommandAck(commandId, true, "Brilho atualizado.", 100, "set-brightness");
    sendCommandProgress(commandId, 100, "set-brightness", "Brilho atualizado.", 1);
    return;
  }
  if (strcmp(command, "install_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      postCommandAck(commandId, false, "appId ausente.", 100, "invalid", "app_invalid");
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

    postCommandAck(commandId, true, "App instalado.", 100, "install-app");
    sendCommandProgress(commandId, 100, "install-app", "App instalado.", 1);
    return;
  }

  if (strcmp(command, "activate_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      postCommandAck(commandId, false, "appId ausente.", 100, "invalid", "app_invalid");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    gActiveAppId = appId;
    gActiveAppName = appName.length() > 0 ? appName : appId;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppName", gActiveAppName);

    postCommandAck(commandId, true, "App ativado.", 100, "activate-app");
    sendCommandProgress(commandId, 100, "activate-app", "App ativado.", 1);
    return;
  }

  if (strcmp(command, "set_app_config") == 0) {
    String appId = parameters["appId"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      postCommandAck(commandId, false, "appId ausente.", 100, "invalid", "app_invalid");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    gActiveAppId = appId;
    gActiveAppConfig = configJson;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppConfig", gActiveAppConfig);

    postCommandAck(commandId, true, "Configuracao de app aplicada.", 100, "set-app-config");
    sendCommandProgress(commandId, 100, "set-app-config", "Configuracao aplicada.", 1);
    return;
  }

  postCommandAck(commandId, false, "Comando desconhecido.", 100, "unknown", "unknown_command");
  sendCommandProgress(commandId, 100, "unknown", "Comando desconhecido.", 0);
}

void connectWebSocket() {
  if (gDeviceId.isEmpty() || gToken.isEmpty() || gServerHost.isEmpty()) {
    setConnectivityState(kWifiStateConnecting, "ws_missing_auth", true);
    return;
  }

  if (WiFi.status() != WL_CONNECTED) {
    setConnectivityState(kWifiStateConnecting, "ws_waiting_wifi", true);
    return;
  }

  String extraHeaders = "X-Device-Id: " + gDeviceId + "\r\nX-Device-Token: " + gToken;
  gWs.setExtraHeaders(extraHeaders.c_str());
  String path = "/ws/v1/stream";
  Serial.printf("[ws] conectando em ws://%s:%u%s\n", gServerHost.c_str(), gServerPort, path.c_str());
  setConnectivityState(kWifiStateConnected, "ws_connecting", true);
  gWs.begin(gServerHost.c_str(), gServerPort, path.c_str());
  gWs.onEvent(onWsEvent);
  gWs.setReconnectInterval(2000);
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
  gDeviceId = gPrefs.getString("deviceId", "");
  gToken = gPrefs.getString("token", "");
  gActiveAppId = gPrefs.getString("activeAppId", "");
  gActiveAppName = gPrefs.getString("activeAppName", "");
  gActiveAppConfig = gPrefs.getString("activeAppConfig", "");
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

  if (gServerHost.isEmpty() || gServerPort == 0) {
    Serial.println("[wifi_connecting] aguardando provisioning serial ou fallback para portal.");
    setConnectivityState(kWifiStateDisconnected, "boot_missing_server_config", true);
    gWifiDisconnectedSinceMs = millis();
  } else if (bootWifiConnected) {
    Serial.println("[wifi_connected] Wi-Fi conectado no boot.");
    setConnectivityState(kWifiStateConnected, "wifi_connected", true);
    connectWebSocket();
  } else {
    Serial.println("[wifi_connecting] sem Wi-Fi no boot, aguardando reconexao.");
    setConnectivityState(kWifiStateDisconnected, "boot_waiting_wifi", true);
    gWifiDisconnectedSinceMs = millis();
  }

  sendSerialHello();
}

void loop() {
  const uint32_t loopStartUs = micros();
  processSerialProvisioning();
  const bool wifiConnected = WiFi.status() == WL_CONNECTED;

  if (!wifiConnected) {
    if (gWifiDisconnectedSinceMs == 0) {
      gWifiDisconnectedSinceMs = millis();
      setConnectivityState(kWifiStateDisconnected, "wifi_disconnected", true);
      Serial.println("[wifi] desconectado, aguardando reconexao.");
    }

    if (!gProvisioningPortalActive
        && (millis() - gWifiDisconnectedSinceMs) > kWifiDisconnectProvisioningFallbackMs) {
      Serial.println("[wifi] fallback para provisioning apos queda prolongada.");
      (void)startProvisioningPortal("wifi_disconnected_fallback");
      gWifiDisconnectedSinceMs = 0;
      connectWebSocket();
    } else {
      delay(120);
    }

    processSerialProvisioning();
    updateTestLed();
    updateLoopLoadPercent(loopStartUs);
    return;
  }

  if (gWifiDisconnectedSinceMs != 0) {
    gWifiDisconnectedSinceMs = 0;
  }

  if (gProvisioningPortalActive) {
    setProvisioningPortalActive(false, "portal_closed");
  }

  setConnectivityState(kWifiStateConnected, "wifi_connected");
  gWs.loop();

  if (!gWs.isConnected()) {
    if (gWsDisconnectedSinceMs == 0) {
      gWsDisconnectedSinceMs = millis();
      setConnectivityState(kWifiStateConnected, "ws_disconnected");
    }

    if (millis() - gWsDisconnectedSinceMs > kWsReconnectRetryMs) {
      Serial.println("[ws_disconnected] sem sessao por tempo prolongado; tentando reconectar websocket.");
      connectWebSocket();
      gWsDisconnectedSinceMs = millis();
    }
  } else {
    gWsDisconnectedSinceMs = 0;
    sendTelemetry(false);
  }

  if (millis() - gLastFrameMs > 15000) {
    memset(gBins, 0, sizeof(gBins));
    gLevel = 0;
    memset(gFrameRgb565, 0, sizeof(gFrameRgb565));
    gFrameModeActive = false;
  }

  updateTestLed();
  processSerialProvisioning();
  if (gFrameModeActive) {
    drawFrame128x64();
  } else {
    drawBars();
  }

  updateLoopLoadPercent(loopStartUs);
}
