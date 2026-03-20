#include <Arduino.h>
#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <Preferences.h>
#include <PubSubClient.h>
#include <Update.h>
#include <WebSocketsClient.h>
#include <WiFi.h>
#include <WiFiManager.h>
#include <esp_err.h>
#include <esp_heap_caps.h>
#include <esp_ota_ops.h>
#include <mbedtls/sha256.h>
#include <math.h>
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
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-128x64-single-canvas-mapping
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---buffer-ws-para-frame-128x64
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-anti-flicker-com-double-buffer
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-upstream-baseline-fluidity-recovery
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-60-fps-com-pacing-fisico-correto
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-diagnostic-matrix-envs
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-fallback-local-de-conectividade
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md
constexpr uint8_t kBinsCount = MICA_STREAM_BINS;
constexpr size_t kStreamFrameSize = 145;
constexpr size_t kStreamFrame128x64Rgb565Size = 16400;
constexpr size_t kExpectedWebSocketMaxDataSize = 32768;
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
constexpr unsigned long kLoopHealthWindowMs = 5000;
constexpr unsigned long kSerialHelloIntervalMs = 3000;
constexpr unsigned long kMatrixSignalTimeoutMs = 15000;
constexpr unsigned long kConnectivityFallbackDebounceMs = 1000;
constexpr size_t kSerialInputMaxLength = 1024;
constexpr unsigned long kWifiConnectAttemptTimeoutMs = 20000;
constexpr uint32_t kHealthyLoopThresholdUs = 25000;
constexpr uint32_t kNetworkPollBudgetUs = 8000;
constexpr unsigned long kOtaSelfTestWindowMs = 10000;
constexpr uint16_t kDefaultMqttPort = 5273;
constexpr const char* kDefaultMqttRootTopic = "mica/v1/devices";
constexpr uint16_t kMqttPacketBufferBytes = 32768;
constexpr uint8_t kMatrixWidth = MICA_MATRIX_WIDTH;
constexpr uint8_t kMatrixHeight = MICA_MATRIX_HEIGHT;
constexpr uint8_t kMatrixHalfHeight = kMatrixHeight / 2;
constexpr size_t kMatrixPixelCount = static_cast<size_t>(kMatrixWidth) * static_cast<size_t>(kMatrixHeight);
constexpr uint8_t kHub75ColorDepthBits = 6;

#ifndef MICA_HUB75_LATCH_BLANKING
#define MICA_HUB75_LATCH_BLANKING 2
#endif

#ifndef MICA_HUB75_MIN_REFRESH_RATE
#define MICA_HUB75_MIN_REFRESH_RATE 60
#endif

#ifndef MICA_HUB75_CLKPHASE
#define MICA_HUB75_CLKPHASE 0
#endif

#ifndef MICA_HUB75_SHIFT_DRIVER
#define MICA_HUB75_SHIFT_DRIVER 0
#endif

constexpr uint8_t kHub75LatchBlankingPulses = static_cast<uint8_t>(MICA_HUB75_LATCH_BLANKING);
constexpr uint8_t kHub75MinRefreshRate = static_cast<uint8_t>(MICA_HUB75_MIN_REFRESH_RATE);
constexpr bool kHub75ClockPhaseEnabled = MICA_HUB75_CLKPHASE != 0;
constexpr uint8_t kHub75TargetPresentFps = 60;
constexpr uint8_t kMatrixShadowBufferCount = 2;
constexpr uint32_t kMicrosPerSecond = 1000000UL;
constexpr uint32_t kHub75TargetPresentIntervalUs =
    (kMicrosPerSecond + kHub75TargetPresentFps - 1u) / kHub75TargetPresentFps;
constexpr uint32_t kHub75FallbackPresentIntervalUs = 20000UL;
static_assert((kMatrixHeight % 2) == 0, "MICA_MATRIX_HEIGHT must be even.");
static_assert(
    MICA_HUB75_LATCH_BLANKING >= 1 && MICA_HUB75_LATCH_BLANKING <= 4,
    "MICA_HUB75_LATCH_BLANKING must stay between 1 and 4 clock pulses.");
static_assert(
    MICA_HUB75_MIN_REFRESH_RATE >= 1 && MICA_HUB75_MIN_REFRESH_RATE <= 240,
    "MICA_HUB75_MIN_REFRESH_RATE must stay between 1 and 240.");
static_assert(
    MICA_HUB75_CLKPHASE == 0 || MICA_HUB75_CLKPHASE == 1,
    "MICA_HUB75_CLKPHASE must be 0 or 1.");
#if defined(MICA_PROFILE_DMA_EXP) && defined(PIXEL_COLOR_DEPTH_BITS)
static_assert(
    PIXEL_COLOR_DEPTH_BITS == kHub75ColorDepthBits,
    "PIXEL_COLOR_DEPTH_BITS must stay aligned with the official HUB75 baseline profile.");
#endif
#if defined(MICA_PROFILE_DMA_EXP)
static_assert(
    MICA_HUB75_SHIFT_DRIVER == 0 || MICA_HUB75_SHIFT_DRIVER == 1,
    "MICA_HUB75_SHIFT_DRIVER must stay 0 (SHIFTREG) or 1 (FM6124) in this firmware path.");
#endif
#if !defined(WEBSOCKETS_MAX_DATA_SIZE)
#error WEBSOCKETS_MAX_DATA_SIZE must be defined by WebSockets.h.
#endif
static_assert(
    WEBSOCKETS_MAX_DATA_SIZE >= kExpectedWebSocketMaxDataSize,
    "WEBSOCKETS_MAX_DATA_SIZE must stay >= 32768 for HUB75 frame transport.");
static_assert(
    WEBSOCKETS_MAX_DATA_SIZE > kStreamFrame128x64Rgb565Size,
    "Frame128x64Rgb565 payload does not fit in the current WebSockets max frame size.");

enum class MatrixBufferMode : uint8_t {
  Unknown = 0,
  Clear = 1,
  Bars = 2,
  Frame = 3,
};

enum class Hub75FallbackState : uint8_t {
  None = 0,
  NoWifi = 1,
  NoServer = 2,
  Portal = 3,
};

enum class Hub75BinsVisualStyle : uint8_t {
  LegacyFallback = 0,
  WaveMirror = 1,
  MirrorLines = 2,
  MirrorBlocks = 3,
  ClassicBars = 4,
  FlowLine = 5,
  HistoryScan = 6,
  RadialOrbit = 7,
  Atmosphere = 8,
  LaunchpadGrid = 9,
};

enum class Hub75BinsPaletteFamily : uint8_t {
  Canonical = 0,
  Rainbow = 1,
  Sunset = 2,
  Arctic = 3,
  Neon = 4,
  Aurora = 5,
  Plasma = 6,
  Mono = 7,
};

constexpr const char* kBoardModel = "esp32s3_devkitc1";
constexpr const char* kBoardDisplayName = "ESP32-S3 DevKitC-1";
constexpr uint8_t kMatrixRgbPins[6] = {4, 5, 6, 7, 15, 16};
constexpr uint8_t kMatrixAddrPins[5] = {18, 8, 3, 42, 17};
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
constexpr const char* kPrefsOtaCommandId = "ota_cmd";
constexpr const char* kPrefsOtaSourceVersion = "ota_src";
constexpr const char* kPrefsOtaTargetVersion = "ota_tgt";

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
portMUX_TYPE gStreamBufferMux = portMUX_INITIALIZER_UNLOCKED;
uint8_t gBinsBuffers[2][kBinsCount] = {};
uint8_t gBinsActiveIndex = 0;
uint8_t gLevel = 0;
uint8_t gBinsFlags = 0;
uint8_t gStreamBrightness = 255;
uint8_t gBrightnessCap = kBrightnessDefaultCap;
uint16_t gFrameRgb565Buffers[2][kMatrixPixelCount] = {};
uint8_t gFrameRgb565ActiveIndex = 0;
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
bool gMatrixFrameDirty = false;
bool gPendingMatrixPresentCountsAsApplied = false;
bool gMatrixSignalTimedOut = false;
uint8_t gAppliedBrightness = 255;
bool gFrameModeActive = false;
uint32_t gLastMatrixPresentUs = 0;
uint32_t gHub75PresentFrames = 0;
unsigned long gLoopWindowStartMs = 0;
uint32_t gLoopWindowIterationCount = 0;
uint32_t gLoopWindowHealthyCount = 0;
uint8_t gLoopHealthyPercent = 0;
bool gLoopHealthyPercentReady = false;
uint32_t gTelemetrySequence = 0;
uint32_t gDeviceLogSequence = 0;
bool gHasStreamLastSequence = false;
uint32_t gStreamLastSequence = 0;
uint32_t gStreamFramesReceived = 0;
uint32_t gStreamFramesApplied = 0;
uint32_t gStreamSequenceGapCount = 0;
uint32_t gStreamInvalidFrameCount = 0;
uint32_t gNetworkPollDeferCount = 0;
bool gProvisioningPortalActive = false;
String gWifiState = kWifiStateConnecting;
String gLastWifiEvent = "boot";
Hub75FallbackState gHub75FallbackState = Hub75FallbackState::None;
Hub75FallbackState gHub75FallbackPendingState = Hub75FallbackState::None;
unsigned long gHub75FallbackPendingSinceMs = 0;
bool gHub75FallbackDirty = false;
bool gHub75FallbackClearPending = false;
String gAuxLedUnavailableReason;
String gSerialInputBuffer;
unsigned long gLastSerialHelloMs = 0;
unsigned long gWsFlapWindowStartMs = 0;
uint16_t gWsConnectCountInWindow = 0;
uint16_t gWsDisconnectCountInWindow = 0;
String gPendingOtaCommandId;
String gPendingOtaSourceVersion;
String gPendingOtaTargetVersion;
String gPendingOtaFailureCode;
String gPendingOtaFailureMessage;
unsigned long gPendingOtaValidationStartedMs = 0;
bool gPendingOtaPendingVerifyAnnounced = false;
uint8_t gRgb5To8Lut[32] = {0};
uint8_t gRgb6To8Lut[64] = {0};
uint8_t gBinsPeakHeights[kMatrixWidth] = {0};
uint8_t gBinsHistory[kMatrixHeight][kMatrixWidth] = {};
uint8_t gBinsHistoryHead = 0;
uint8_t gLaunchpadPadLevels[64] = {0};
uint8_t gLaunchpadTopLevels[8] = {0};
uint8_t gLaunchpadSideLevels[8] = {0};
uint8_t gLastBinsStyleId = 0xFFu;
uint16_t gMatrixShadowFrames[kMatrixShadowBufferCount][kMatrixPixelCount] = {};
uint8_t gMatrixShadowBarHeights[kMatrixShadowBufferCount][kMatrixWidth] = {};
MatrixBufferMode gMatrixBufferModes[kMatrixShadowBufferCount] = {
    MatrixBufferMode::Unknown,
    MatrixBufferMode::Unknown};
uint8_t gMatrixShadowBackBufferIndex = 0;

enum class PendingOtaBootState : uint8_t {
  None = 0,
  PendingVerify = 1,
  ValidatedPendingReport = 2,
  RolledBackPendingReport = 3,
  FailedPendingReport = 4,
};

PendingOtaBootState gPendingOtaBootState = PendingOtaBootState::None;

struct FirmwareReleaseInfo;

void connectWebSocket();
void connectMqtt();
void handleControlCommandMessage(const JsonDocument& control);
void sendCommandProgress(
    const String& commandId,
    uint8_t progressPercent,
    const char* stage,
    const String& message,
    int successFlag = -1);
bool hasPendingOtaContext();
bool tryGetRunningOtaState(esp_ota_img_states_t& otaState);
bool isRunningAppPendingVerify();
void loadPendingOtaContext();
void clearPendingOtaContext();
bool persistPendingOtaContext(const String& commandId, const String& sourceVersion, const String& targetVersion);
void initializePendingOtaBootState();
void processPendingOtaSafeUpdate();
void publishPendingOtaReportIfNeeded();
void requestPendingOtaRollbackAndReboot(const char* errorCode, const String& errorMessage);
bool tryFetchLatestFirmwareRelease(FirmwareReleaseInfo& info, const String& requestedVersion, String& errorCode, String& errorMessage);
bool performFirmwareOta(const FirmwareReleaseInfo& info, const String& commandId, String& errorCode, String& errorMessage);

#if defined(MICA_PROFILE_DMA_EXP)
MatrixPanel_I2S_DMA* gMatrix = nullptr;
#if MICA_HUB75_SHIFT_DRIVER == 1
constexpr HUB75_I2S_CFG::shift_driver kHub75BaselineDriver = HUB75_I2S_CFG::FM6124;
#else
constexpr HUB75_I2S_CFG::shift_driver kHub75BaselineDriver = HUB75_I2S_CFG::SHIFTREG;
#endif
#endif

struct RgbColor {
  uint8_t r;
  uint8_t g;
  uint8_t b;
};

struct FirmwareReleaseInfo {
  String firmwareVersion;
  String boardModel;
  String panelType;
  String profile;
  String controlPlane;
  String sha256;
  uint32_t fileSizeBytes = 0;
  String downloadPath;
};

constexpr uint32_t ceilDivideU32(uint32_t numerator, uint32_t denominator) {
  return denominator == 0 ? 0u : (numerator + denominator - 1u) / denominator;
}

const char* hub75FallbackStateName(Hub75FallbackState state) {
  switch (state) {
    case Hub75FallbackState::NoWifi:
      return "no_wifi";
    case Hub75FallbackState::NoServer:
      return "no_server";
    case Hub75FallbackState::Portal:
      return "portal";
    case Hub75FallbackState::None:
    default:
      return "none";
  }
}

size_t matrixPixelIndex(uint8_t x, uint8_t y) {
  return static_cast<size_t>(y) * static_cast<size_t>(kMatrixWidth) + static_cast<size_t>(x);
}

uint16_t rgb888ToRgb565(uint8_t r, uint8_t g, uint8_t b) {
  return static_cast<uint16_t>(((r & 0xF8u) << 8) | ((g & 0xFCu) << 3) | (b >> 3));
}

bool commitMatrixFrame();

void initializeColorConversionLookups() {
  for (uint8_t value = 0; value < 32; value++) {
    gRgb5To8Lut[value] = static_cast<uint8_t>((static_cast<uint16_t>(value) * 255u + 15u) / 31u);
  }

  for (uint8_t value = 0; value < 64; value++) {
    gRgb6To8Lut[value] = static_cast<uint8_t>((static_cast<uint16_t>(value) * 255u + 31u) / 63u);
  }
}

void clearMatrixShadowBuffer(uint8_t bufferIndex) {
  if (bufferIndex >= kMatrixShadowBufferCount) {
    return;
  }

  memset(gMatrixShadowFrames[bufferIndex], 0, sizeof(gMatrixShadowFrames[bufferIndex]));
  memset(gMatrixShadowBarHeights[bufferIndex], 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
  gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Clear;
}

void resetMatrixShadowState() {
  for (uint8_t bufferIndex = 0; bufferIndex < kMatrixShadowBufferCount; bufferIndex++) {
    clearMatrixShadowBuffer(bufferIndex);
  }

  gMatrixShadowBackBufferIndex = 0;
}

void setMatrixShadowPixel(uint8_t x, uint8_t y, uint16_t rgb565) {
  if (x >= kMatrixWidth || y >= kMatrixHeight) {
    return;
  }

  gMatrixShadowFrames[gMatrixShadowBackBufferIndex][matrixPixelIndex(x, y)] = rgb565;
}

void fillMatrixShadowRect(int16_t x, int16_t y, int16_t w, int16_t h, uint16_t rgb565) {
  if (w <= 0 || h <= 0) {
    return;
  }

  const int16_t xStart = x < 0 ? 0 : x;
  const int16_t yStart = y < 0 ? 0 : y;
  const int16_t xEnd = (x + w) > static_cast<int16_t>(kMatrixWidth) ? static_cast<int16_t>(kMatrixWidth) : static_cast<int16_t>(x + w);
  const int16_t yEnd = (y + h) > static_cast<int16_t>(kMatrixHeight) ? static_cast<int16_t>(kMatrixHeight) : static_cast<int16_t>(y + h);
  if (xStart >= xEnd || yStart >= yEnd) {
    return;
  }

  for (int16_t row = yStart; row < yEnd; row++) {
    uint16_t* shadowRow =
        gMatrixShadowFrames[gMatrixShadowBackBufferIndex] + (static_cast<size_t>(row) * static_cast<size_t>(kMatrixWidth));
    for (int16_t column = xStart; column < xEnd; column++) {
      shadowRow[column] = rgb565;
    }
  }
}

bool hasPendingOtaContext() {
  return gPendingOtaCommandId.length() > 0
      && gPendingOtaSourceVersion.length() > 0
      && gPendingOtaTargetVersion.length() > 0;
}

bool tryGetRunningOtaState(esp_ota_img_states_t& otaState) {
  const esp_partition_t* runningPartition = esp_ota_get_running_partition();
  if (runningPartition == nullptr) {
    return false;
  }

  return esp_ota_get_state_partition(runningPartition, &otaState) == ESP_OK;
}

bool isRunningAppPendingVerify() {
  esp_ota_img_states_t otaState = ESP_OTA_IMG_UNDEFINED;
  return tryGetRunningOtaState(otaState) && otaState == ESP_OTA_IMG_PENDING_VERIFY;
}

void loadPendingOtaContext() {
  gPendingOtaCommandId = gPrefs.getString(kPrefsOtaCommandId, "");
  gPendingOtaSourceVersion = gPrefs.getString(kPrefsOtaSourceVersion, "");
  gPendingOtaTargetVersion = gPrefs.getString(kPrefsOtaTargetVersion, "");
  gPendingOtaFailureCode = "";
  gPendingOtaFailureMessage = "";
  gPendingOtaValidationStartedMs = 0;
  gPendingOtaPendingVerifyAnnounced = false;
  gPendingOtaBootState = PendingOtaBootState::None;
}

void clearPendingOtaContext() {
  gPrefs.remove(kPrefsOtaCommandId);
  gPrefs.remove(kPrefsOtaSourceVersion);
  gPrefs.remove(kPrefsOtaTargetVersion);

  gPendingOtaCommandId = "";
  gPendingOtaSourceVersion = "";
  gPendingOtaTargetVersion = "";
  gPendingOtaFailureCode = "";
  gPendingOtaFailureMessage = "";
  gPendingOtaValidationStartedMs = 0;
  gPendingOtaPendingVerifyAnnounced = false;
  gPendingOtaBootState = PendingOtaBootState::None;
}

bool persistPendingOtaContext(const String& commandId, const String& sourceVersion, const String& targetVersion) {
  if (commandId.isEmpty() || sourceVersion.isEmpty() || targetVersion.isEmpty()) {
    return false;
  }

  const size_t commandWritten = gPrefs.putString(kPrefsOtaCommandId, commandId);
  const size_t sourceWritten = gPrefs.putString(kPrefsOtaSourceVersion, sourceVersion);
  const size_t targetWritten = gPrefs.putString(kPrefsOtaTargetVersion, targetVersion);
  if (commandWritten == 0u || sourceWritten == 0u || targetWritten == 0u) {
    return false;
  }

  gPendingOtaCommandId = commandId;
  gPendingOtaSourceVersion = sourceVersion;
  gPendingOtaTargetVersion = targetVersion;
  gPendingOtaFailureCode = "";
  gPendingOtaFailureMessage = "";
  gPendingOtaValidationStartedMs = 0;
  gPendingOtaPendingVerifyAnnounced = false;
  gPendingOtaBootState = PendingOtaBootState::None;
  return true;
}

void initializePendingOtaBootState() {
  if (!hasPendingOtaContext()) {
    return;
  }

  if (String(kFirmwareVersion).equalsIgnoreCase(gPendingOtaTargetVersion)) {
    esp_ota_img_states_t otaState = ESP_OTA_IMG_UNDEFINED;
    if (tryGetRunningOtaState(otaState) && otaState == ESP_OTA_IMG_PENDING_VERIFY) {
      gPendingOtaBootState = PendingOtaBootState::PendingVerify;
      gPendingOtaValidationStartedMs = millis();
      Serial.printf("[ota] safe update pendente de validacao. target=%s\n", gPendingOtaTargetVersion.c_str());
      return;
    }

    gPendingOtaBootState = PendingOtaBootState::ValidatedPendingReport;
    Serial.printf("[ota] firmware OTA ativo e aguardando confirmacao tracked. target=%s\n", gPendingOtaTargetVersion.c_str());
    return;
  }

  if (String(kFirmwareVersion).equalsIgnoreCase(gPendingOtaSourceVersion)) {
    gPendingOtaBootState = PendingOtaBootState::RolledBackPendingReport;
    gPendingOtaFailureCode = "ota_rolled_back";
    gPendingOtaFailureMessage = String("Rollback automatico concluido; firmware atual voltou para ") + kFirmwareVersion + ".";
    Serial.printf("[ota] rollback automatico detectado. source=%s target=%s\n",
        gPendingOtaSourceVersion.c_str(),
        gPendingOtaTargetVersion.c_str());
    return;
  }

  gPendingOtaBootState = PendingOtaBootState::FailedPendingReport;
  gPendingOtaFailureCode = "ota_context_mismatch";
  gPendingOtaFailureMessage = String("Contexto OTA inconsistente para firmware atual ") + kFirmwareVersion + ".";
  Serial.printf("[ota] contexto OTA inconsistente. current=%s source=%s target=%s\n",
      kFirmwareVersion,
      gPendingOtaSourceVersion.c_str(),
      gPendingOtaTargetVersion.c_str());
}

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
  return {
      gRgb5To8Lut[(rgb565 >> 11) & 0x1Fu],
      gRgb6To8Lut[(rgb565 >> 5) & 0x3Fu],
      gRgb5To8Lut[rgb565 & 0x1Fu]};
}

float clamp01f(float value) {
  if (value <= 0.0f) {
    return 0.0f;
  }

  return value >= 1.0f ? 1.0f : value;
}

uint8_t clampToByte(int value) {
  if (value <= 0) {
    return 0;
  }

  return value >= 255 ? 255 : static_cast<uint8_t>(value);
}

RgbColor scaleColor(const RgbColor& color, float amount) {
  const float scale = clamp01f(amount);
  return {
      clampToByte(static_cast<int>(roundf(color.r * scale))),
      clampToByte(static_cast<int>(roundf(color.g * scale))),
      clampToByte(static_cast<int>(roundf(color.b * scale)))};
}

RgbColor mixColor(const RgbColor& left, const RgbColor& right, float amount) {
  const float t = clamp01f(amount);
  return {
      clampToByte(static_cast<int>(roundf(left.r + ((right.r - left.r) * t)))),
      clampToByte(static_cast<int>(roundf(left.g + ((right.g - left.g) * t)))),
      clampToByte(static_cast<int>(roundf(left.b + ((right.b - left.b) * t))))};
}

RgbColor sampleGradientStops(const RgbColor* stops, size_t stopCount, float t) {
  if (stops == nullptr || stopCount == 0) {
    return {255, 255, 255};
  }

  if (stopCount == 1) {
    return stops[0];
  }

  const float clamped = clamp01f(t);
  const float scaled = clamped * static_cast<float>(stopCount - 1u);
  const size_t leftIndex = static_cast<size_t>(scaled);
  const size_t rightIndex = leftIndex + 1u >= stopCount ? stopCount - 1u : leftIndex + 1u;
  return mixColor(stops[leftIndex], stops[rightIndex], scaled - static_cast<float>(leftIndex));
}

Hub75BinsVisualStyle decodeBinsVisualStyle(uint8_t flags) {
  switch (flags >> 3u) {
    case 1u:
      return Hub75BinsVisualStyle::WaveMirror;
    case 2u:
      return Hub75BinsVisualStyle::MirrorLines;
    case 3u:
      return Hub75BinsVisualStyle::MirrorBlocks;
    case 4u:
      return Hub75BinsVisualStyle::ClassicBars;
    case 5u:
      return Hub75BinsVisualStyle::FlowLine;
    case 6u:
      return Hub75BinsVisualStyle::HistoryScan;
    case 7u:
      return Hub75BinsVisualStyle::RadialOrbit;
    case 8u:
      return Hub75BinsVisualStyle::Atmosphere;
    case 9u:
      return Hub75BinsVisualStyle::LaunchpadGrid;
    default:
      return Hub75BinsVisualStyle::LegacyFallback;
  }
}

Hub75BinsPaletteFamily decodeBinsPaletteFamily(uint8_t flags) {
  switch (flags & 0x07u) {
    case 1u:
      return Hub75BinsPaletteFamily::Rainbow;
    case 2u:
      return Hub75BinsPaletteFamily::Sunset;
    case 3u:
      return Hub75BinsPaletteFamily::Arctic;
    case 4u:
      return Hub75BinsPaletteFamily::Neon;
    case 5u:
      return Hub75BinsPaletteFamily::Aurora;
    case 6u:
      return Hub75BinsPaletteFamily::Plasma;
    case 7u:
      return Hub75BinsPaletteFamily::Mono;
    default:
      return Hub75BinsPaletteFamily::Canonical;
  }
}

Hub75BinsPaletteFamily resolveBinsEffectivePalette(Hub75BinsVisualStyle style, Hub75BinsPaletteFamily requestedPalette) {
  if (requestedPalette != Hub75BinsPaletteFamily::Canonical) {
    return requestedPalette;
  }

  switch (style) {
    case Hub75BinsVisualStyle::WaveMirror:
      return Hub75BinsPaletteFamily::Rainbow;
    case Hub75BinsVisualStyle::RadialOrbit:
      return Hub75BinsPaletteFamily::Mono;
    case Hub75BinsVisualStyle::Atmosphere:
      return Hub75BinsPaletteFamily::Aurora;
    case Hub75BinsVisualStyle::LaunchpadGrid:
      return Hub75BinsPaletteFamily::Canonical;
    default:
      return Hub75BinsPaletteFamily::Rainbow;
  }
}

RgbColor samplePaletteColor(Hub75BinsPaletteFamily palette, float t) {
  static constexpr RgbColor kSunsetStops[] = {
      {255, 72, 40},
      {255, 132, 32},
      {255, 188, 48},
      {220, 82, 196},
      {98, 54, 255}};
  static constexpr RgbColor kArcticStops[] = {
      {24, 214, 255},
      {0, 168, 255},
      {74, 224, 255},
      {138, 255, 214},
      {114, 92, 255}};
  static constexpr RgbColor kNeonStops[] = {
      {57, 255, 20},
      {0, 255, 180},
      {0, 220, 255},
      {120, 86, 255},
      {255, 60, 180}};
  static constexpr RgbColor kAuroraStops[] = {
      {48, 255, 170},
      {0, 222, 255},
      {88, 124, 255},
      {198, 84, 255},
      {255, 176, 226}};
  static constexpr RgbColor kPlasmaStops[] = {
      {255, 88, 46},
      {255, 168, 26},
      {255, 58, 168},
      {64, 108, 255},
      {72, 244, 255}};
  static constexpr RgbColor kMonoStops[] = {
      {88, 98, 118},
      {180, 192, 214},
      {255, 255, 255}};

  switch (palette) {
    case Hub75BinsPaletteFamily::Sunset:
      return sampleGradientStops(kSunsetStops, sizeof(kSunsetStops) / sizeof(kSunsetStops[0]), t);
    case Hub75BinsPaletteFamily::Arctic:
      return sampleGradientStops(kArcticStops, sizeof(kArcticStops) / sizeof(kArcticStops[0]), t);
    case Hub75BinsPaletteFamily::Neon:
      return sampleGradientStops(kNeonStops, sizeof(kNeonStops) / sizeof(kNeonStops[0]), t);
    case Hub75BinsPaletteFamily::Aurora:
      return sampleGradientStops(kAuroraStops, sizeof(kAuroraStops) / sizeof(kAuroraStops[0]), t);
    case Hub75BinsPaletteFamily::Plasma:
      return sampleGradientStops(kPlasmaStops, sizeof(kPlasmaStops) / sizeof(kPlasmaStops[0]), t);
    case Hub75BinsPaletteFamily::Mono:
      return sampleGradientStops(kMonoStops, sizeof(kMonoStops) / sizeof(kMonoStops[0]), t);
    case Hub75BinsPaletteFamily::Canonical:
      return {255, 255, 255};
    case Hub75BinsPaletteFamily::Rainbow:
    default:
      return rainbowColorForColumn(
          static_cast<uint16_t>(clampToByte(static_cast<int>(roundf(clamp01f(t) * 255.0f)))),
          256);
  }
}

uint8_t smoothBinsSample(const uint8_t* bins, int index) {
  const int clampedIndex = index < 0 ? 0 : (index >= kBinsCount ? kBinsCount - 1 : index);
  const int leftIndex = clampedIndex > 0 ? clampedIndex - 1 : clampedIndex;
  const int rightIndex = clampedIndex + 1 < kBinsCount ? clampedIndex + 1 : clampedIndex;
  const int smoothed =
      static_cast<int>(bins[leftIndex]) + (static_cast<int>(bins[clampedIndex]) * 2) + static_cast<int>(bins[rightIndex]);
  return static_cast<uint8_t>(smoothed / 4);
}

uint8_t amplitudeToHeight(uint8_t amplitude, uint8_t maxHeight) {
  return static_cast<uint8_t>((static_cast<uint16_t>(amplitude) * maxHeight + 254u) / 255u);
}

uint8_t sampleBinsAverage(const uint8_t* bins, uint16_t startInclusive, uint16_t endExclusive) {
  if (startInclusive >= endExclusive || startInclusive >= kBinsCount) {
    return 0;
  }

  const uint16_t safeEnd = endExclusive > kBinsCount ? kBinsCount : endExclusive;
  uint32_t sum = 0;
  for (uint16_t index = startInclusive; index < safeEnd; index++) {
    sum += bins[index];
  }

  return static_cast<uint8_t>(sum / static_cast<uint32_t>(safeEnd - startInclusive));
}

void resetBinsVisualState() {
  memset(gBinsPeakHeights, 0, sizeof(gBinsPeakHeights));
  memset(gBinsHistory, 0, sizeof(gBinsHistory));
  gBinsHistoryHead = 0;
  memset(gLaunchpadPadLevels, 0, sizeof(gLaunchpadPadLevels));
  memset(gLaunchpadTopLevels, 0, sizeof(gLaunchpadTopLevels));
  memset(gLaunchpadSideLevels, 0, sizeof(gLaunchpadSideLevels));
  for (uint8_t bufferIndex = 0; bufferIndex < kMatrixShadowBufferCount; bufferIndex++) {
    memset(gMatrixShadowBarHeights[bufferIndex], 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
    if (gMatrixBufferModes[bufferIndex] == MatrixBufferMode::Bars) {
      gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Unknown;
    }
  }
}

bool finishBinsVisualFrame() {
  gMatrixBufferModes[gMatrixShadowBackBufferIndex] = MatrixBufferMode::Bars;
  return commitMatrixFrame();
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
    clearMatrixShadowBuffer(gMatrixShadowBackBufferIndex);
  }
#endif
}

void drawMatrixPixel(uint8_t x, uint8_t y, const RgbColor& color) {

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->drawPixelRGB888(x, y, color.r, color.g, color.b);
    setMatrixShadowPixel(x, y, rgb888ToRgb565(color.r, color.g, color.b));
  }
#endif
}

void fillMatrixRect(int16_t x, int16_t y, int16_t w, int16_t h, const RgbColor& color) {
  if (!gMatrixReady || w <= 0 || h <= 0) {
    return;
  }

  const int16_t xStart = x < 0 ? 0 : x;
  const int16_t yStart = y < 0 ? 0 : y;
  const int16_t xEnd = (x + w) > static_cast<int16_t>(kMatrixWidth) ? static_cast<int16_t>(kMatrixWidth) : static_cast<int16_t>(x + w);
  const int16_t yEnd = (y + h) > static_cast<int16_t>(kMatrixHeight) ? static_cast<int16_t>(kMatrixHeight) : static_cast<int16_t>(y + h);
  if (xStart >= xEnd || yStart >= yEnd) {
    return;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->fillRect(
        xStart,
        yStart,
        xEnd - xStart,
        yEnd - yStart,
        rgb888ToRgb565(color.r, color.g, color.b));
    fillMatrixShadowRect(xStart, yStart, xEnd - xStart, yEnd - yStart, rgb888ToRgb565(color.r, color.g, color.b));
  }
#endif
}

#if defined(MICA_PROFILE_DMA_EXP)
void drawMatrixTextCentered(const char* text, int16_t baselineY, uint16_t color, uint8_t textSize) {
  if (gMatrix == nullptr || text == nullptr || text[0] == '\0') {
    return;
  }

  int16_t x1 = 0;
  int16_t y1 = 0;
  uint16_t textWidth = 0;
  uint16_t textHeight = 0;
  gMatrix->setTextWrap(false);
  gMatrix->setTextSize(textSize);
  gMatrix->getTextBounds(text, 0, baselineY, &x1, &y1, &textWidth, &textHeight);

  int16_t cursorX = ((static_cast<int16_t>(kMatrixWidth) - static_cast<int16_t>(textWidth)) / 2) - x1;
  if (cursorX < 0) {
    cursorX = 0;
  }

  gMatrix->setCursor(cursorX, baselineY);
  gMatrix->setTextColor(color);
  gMatrix->print(text);
}

void drawConnectivityFallbackIcon(Hub75FallbackState state, uint16_t accentColor, uint16_t neutralColor) {
  if (gMatrix == nullptr) {
    return;
  }

  constexpr int16_t kIconCenterX = kMatrixWidth / 2;
  constexpr int16_t kIconTopY = 8;
  switch (state) {
    case Hub75FallbackState::NoWifi:
      gMatrix->fillRect(kIconCenterX - 11, kIconTopY + 8, 3, 4, neutralColor);
      gMatrix->fillRect(kIconCenterX - 4, kIconTopY + 5, 3, 7, neutralColor);
      gMatrix->fillRect(kIconCenterX + 3, kIconTopY + 2, 3, 10, neutralColor);
      gMatrix->drawLine(kIconCenterX - 14, kIconTopY + 12, kIconCenterX + 10, kIconTopY, accentColor);
      gMatrix->drawLine(kIconCenterX - 13, kIconTopY + 12, kIconCenterX + 11, kIconTopY, accentColor);
      break;
    case Hub75FallbackState::NoServer:
      gMatrix->drawRect(kIconCenterX - 12, kIconTopY + 1, 24, 12, neutralColor);
      gMatrix->fillRect(kIconCenterX - 8, kIconTopY + 4, 3, 3, accentColor);
      gMatrix->fillRect(kIconCenterX - 1, kIconTopY + 4, 3, 3, accentColor);
      gMatrix->fillRect(kIconCenterX + 6, kIconTopY + 4, 3, 3, accentColor);
      gMatrix->drawLine(kIconCenterX - 5, kIconTopY + 16, kIconCenterX + 5, kIconTopY + 16, neutralColor);
      gMatrix->drawLine(kIconCenterX, kIconTopY + 13, kIconCenterX, kIconTopY + 18, neutralColor);
      break;
    case Hub75FallbackState::Portal:
      gMatrix->fillRect(kIconCenterX - 1, kIconTopY + 8, 3, 6, neutralColor);
      gMatrix->drawLine(kIconCenterX - 7, kIconTopY + 12, kIconCenterX - 1, kIconTopY + 9, accentColor);
      gMatrix->drawLine(kIconCenterX + 1, kIconTopY + 9, kIconCenterX + 7, kIconTopY + 12, accentColor);
      gMatrix->drawLine(kIconCenterX - 11, kIconTopY + 14, kIconCenterX - 3, kIconTopY + 10, accentColor);
      gMatrix->drawLine(kIconCenterX + 3, kIconTopY + 10, kIconCenterX + 11, kIconTopY + 14, accentColor);
      break;
    case Hub75FallbackState::None:
    default:
      break;
  }
}
#endif

Hub75FallbackState resolveHub75FallbackCandidate() {
  if (gProvisioningPortalActive) {
    return Hub75FallbackState::Portal;
  }

  if (WiFi.status() != WL_CONNECTED) {
    return Hub75FallbackState::NoWifi;
  }

  if (!gWs.isConnected()) {
    return Hub75FallbackState::NoServer;
  }

  return Hub75FallbackState::None;
}

Hub75FallbackState resolveHub75FallbackState(unsigned long nowMs) {
  const Hub75FallbackState candidate = resolveHub75FallbackCandidate();
  if (candidate == Hub75FallbackState::None || candidate == Hub75FallbackState::Portal) {
    gHub75FallbackPendingState = candidate;
    gHub75FallbackPendingSinceMs = 0;
    return candidate;
  }

  if (candidate == gHub75FallbackState) {
    gHub75FallbackPendingState = candidate;
    gHub75FallbackPendingSinceMs = 0;
    return candidate;
  }

  if (candidate != gHub75FallbackPendingState) {
    gHub75FallbackPendingState = candidate;
    gHub75FallbackPendingSinceMs = nowMs;
    return Hub75FallbackState::None;
  }

  if (gHub75FallbackPendingSinceMs != 0
      && (nowMs - gHub75FallbackPendingSinceMs) >= kConnectivityFallbackDebounceMs) {
    return candidate;
  }

  return Hub75FallbackState::None;
}

void updateHub75FallbackState(unsigned long nowMs) {
  const Hub75FallbackState nextState = resolveHub75FallbackState(nowMs);
  if (nextState == gHub75FallbackState) {
    return;
  }

  const Hub75FallbackState previousState = gHub75FallbackState;
  gHub75FallbackState = nextState;
  gHub75FallbackDirty = nextState != Hub75FallbackState::None;
  gHub75FallbackClearPending = previousState != Hub75FallbackState::None && nextState == Hub75FallbackState::None;
  if (nextState != Hub75FallbackState::None) {
    gHub75FallbackClearPending = false;
  }

  Serial.printf(
      "[hub75_fallback] previous=%s next=%s\n",
      hub75FallbackStateName(previousState),
      hub75FallbackStateName(nextState));
}

bool clearConnectivityFallbackFrame() {
  if (!gMatrixReady) {
    return false;
  }

  clearMatrix();
  return commitMatrixFrame();
}

bool drawConnectivityFallback(Hub75FallbackState state) {
  if (!gMatrixReady || state == Hub75FallbackState::None) {
    return false;
  }

  const char* title = "";
  const char* subtitle = "";
  RgbColor accent = {0, 0, 0};
  switch (state) {
    case Hub75FallbackState::NoWifi:
      title = "SEM WIFI";
      subtitle = "Conecte a rede";
      accent = {255, 170, 48};
      break;
    case Hub75FallbackState::NoServer:
      title = "SEM SERV";
      subtitle = "Abra o MicaAudio";
      accent = {255, 96, 96};
      break;
    case Hub75FallbackState::Portal:
      title = "SETUP WIFI";
      subtitle = "Conecte no portal";
      accent = {96, 220, 255};
      break;
    case Hub75FallbackState::None:
    default:
      return false;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  clearMatrix();
  const uint16_t accentColor = rgb888ToRgb565(accent.r, accent.g, accent.b);
  const uint16_t titleColor = rgb888ToRgb565(244, 244, 244);
  const uint16_t subtitleColor = rgb888ToRgb565(158, 170, 180);
  drawConnectivityFallbackIcon(state, accentColor, titleColor);
  gMatrix->drawFastHLine(24, 22, kMatrixWidth - 48, rgb888ToRgb565(36, 48, 60));
  drawMatrixTextCentered(title, 36, titleColor, 2);
  drawMatrixTextCentered(subtitle, 50, subtitleColor, 1);
  gMatrixBufferModes[gMatrixShadowBackBufferIndex] = MatrixBufferMode::Clear;
  return commitMatrixFrame();
#else
  return false;
#endif
}

bool commitMatrixFrame() {
#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->flipDMABuffer();
    gLastMatrixPresentUs = micros();
    gHub75PresentFrames++;
    gMatrixShadowBackBufferIndex ^= 1u;
    return true;
  }
#endif

  return false;
}

uint32_t getPhysicalPresentIntervalUs() {
#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr && gMatrix->calculated_refresh_rate > 0) {
    const uint32_t refreshRate = static_cast<uint32_t>(gMatrix->calculated_refresh_rate);
    const uint32_t intervalUs = ceilDivideU32(kMicrosPerSecond, refreshRate);
    return intervalUs == 0 ? 1u : intervalUs;
  }
#endif

  return kHub75FallbackPresentIntervalUs;
}

uint32_t getEffectiveMatrixPresentIntervalUs() {
  const uint32_t physicalPresentIntervalUs = getPhysicalPresentIntervalUs();
  return physicalPresentIntervalUs > kHub75TargetPresentIntervalUs
      ? physicalPresentIntervalUs
      : kHub75TargetPresentIntervalUs;
}

bool shouldPresentMatrixFrame(uint32_t nowUs) {
  return gLastMatrixPresentUs == 0
      || static_cast<uint32_t>(nowUs - gLastMatrixPresentUs) >= getEffectiveMatrixPresentIntervalUs();
}

void markMatrixFrameDirty(bool countAsAppliedFrame) {
  gMatrixFrameDirty = true;
  if (countAsAppliedFrame) {
    gPendingMatrixPresentCountsAsApplied = true;
  }
}

#if defined(MICA_PROFILE_DMA_EXP)
const char* hub75DriverName(HUB75_I2S_CFG::shift_driver driver) {
  switch (driver) {
    case HUB75_I2S_CFG::SHIFTREG:
      return "SHIFTREG";
    case HUB75_I2S_CFG::FM6124:
      return "FM6124";
    case HUB75_I2S_CFG::FM6126A:
      return "FM6126A";
    case HUB75_I2S_CFG::ICN2038S:
      return "ICN2038S";
    case HUB75_I2S_CFG::MBI5124:
      return "MBI5124";
    default:
      return "UNKNOWN";
  }
}
#endif

bool validateMatrixPinConfiguration() {
  if (kMatrixHeight < 64) {
    return true;
  }

  const int ePin = static_cast<int>(kMatrixAddrPins[4]);
  if (ePin < 0) {
    Serial.println("Pinout HUB75 invalido: painel 128x64 exige linha E.");
    return false;
  }

  if (ePin == static_cast<int>(kMatrixClockPin)
      || ePin == static_cast<int>(kMatrixLatchPin)
      || ePin == static_cast<int>(kMatrixOePin)) {
    Serial.printf(
        "Pinout HUB75 invalido: linha E=%d conflita com CLK/LAT/OE.\n",
        ePin);
    return false;
  }

  return true;
}

void logMatrixPinout() {
  Serial.printf(
      "HUB75 pinout RGB={%u,%u,%u,%u,%u,%u} ADDR={%u,%u,%u,%u,%u} LAT=%u OE=%u CLK=%u\n",
      static_cast<unsigned>(kMatrixRgbPins[0]),
      static_cast<unsigned>(kMatrixRgbPins[1]),
      static_cast<unsigned>(kMatrixRgbPins[2]),
      static_cast<unsigned>(kMatrixRgbPins[3]),
      static_cast<unsigned>(kMatrixRgbPins[4]),
      static_cast<unsigned>(kMatrixRgbPins[5]),
      static_cast<unsigned>(kMatrixAddrPins[0]),
      static_cast<unsigned>(kMatrixAddrPins[1]),
      static_cast<unsigned>(kMatrixAddrPins[2]),
      static_cast<unsigned>(kMatrixAddrPins[3]),
      static_cast<unsigned>(kMatrixAddrPins[4]),
      static_cast<unsigned>(kMatrixLatchPin),
      static_cast<unsigned>(kMatrixOePin),
      static_cast<unsigned>(kMatrixClockPin));
}

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#pontos-de-alteracao-frequente
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-128x64-single-canvas-mapping
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-anti-flicker-com-double-buffer
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-upstream-baseline-fluidity-recovery
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-60-fps-com-pacing-fisico-correto
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-diagnostic-matrix-envs
bool initMatrixDisplay() {

#if defined(MICA_PROFILE_DMA_EXP)
  if (!validateMatrixPinConfiguration()) {
    return false;
  }

  logMatrixPinout();

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
      static_cast<int8_t>(kMatrixAddrPins[4]),
      static_cast<int8_t>(kMatrixLatchPin),
      static_cast<int8_t>(kMatrixOePin),
      static_cast<int8_t>(kMatrixClockPin)};

  HUB75_I2S_CFG config(kMatrixWidth, kMatrixHeight, 1, pinMap);
  config.double_buff = true;
  config.i2sspeed = HUB75_I2S_CFG::HZ_10M;
  config.clkphase = kHub75ClockPhaseEnabled;
  config.driver = kHub75BaselineDriver;
  config.latch_blanking = kHub75LatchBlankingPulses;
  config.min_refresh_rate = kHub75MinRefreshRate;
  config.setPixelColorDepthBits(kHub75ColorDepthBits);
  Serial.printf(
      "[hub75] config matrix=%ux%u driver=%s color_depth=%u i2s=10MHz clkphase=%u double_buffer=%u latch_blanking=%u min_refresh_rate=%u\n",
      static_cast<unsigned>(kMatrixWidth),
      static_cast<unsigned>(kMatrixHeight),
      hub75DriverName(config.driver),
      static_cast<unsigned>(config.getPixelColorDepthBits()),
      config.clkphase ? 1u : 0u,
      config.double_buff ? 1u : 0u,
      static_cast<unsigned>(config.latch_blanking),
      static_cast<unsigned>(config.min_refresh_rate));

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

  const uint8_t effectiveLatchBlanking = gMatrix->setLatBlanking(kHub75LatchBlankingPulses);
  Serial.printf(
      "[hub75] active driver=%s calculated_refresh_rate=%dHz latch_blanking=%u physical_present_interval_us=%lu target_present_interval_us=%lu effective_present_interval_us=%lu clkphase=%u double_buffer=%u min_refresh_rate=%u\n",
      hub75DriverName(config.driver),
      gMatrix->calculated_refresh_rate,
      static_cast<unsigned>(effectiveLatchBlanking),
      static_cast<unsigned long>(getPhysicalPresentIntervalUs()),
      static_cast<unsigned long>(kHub75TargetPresentIntervalUs),
      static_cast<unsigned long>(getEffectiveMatrixPresentIntervalUs()),
      config.clkphase ? 1u : 0u,
      config.double_buff ? 1u : 0u,
      static_cast<unsigned>(config.min_refresh_rate));
#if defined(MICA_HUB75_DIAGNOSTIC_MODE)
  Serial.println("[hub75] diagnostic_mode=1 oracle_compare=shiftreg_vs_fm6124");
#endif
#endif

  gMatrixReady = true;
  gAppliedBrightness = 0;
  resetMatrixShadowState();
  setMatrixBrightness(resolveAppliedBrightness());
  updateTestLedDutyFromBrightness(gAppliedBrightness);
  clearMatrix();
  (void)commitMatrixFrame();
  clearMatrix();
  (void)commitMatrixFrame();
  gLastMatrixPresentUs = 0;
  gHub75PresentFrames = 0;
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

bool tryParseFirmwareReleaseInfo(const JsonDocument& document, FirmwareReleaseInfo& info) {
  const char* firmwareVersion = document["firmwareVersion"] | "";
  const char* boardModel = document["boardModel"] | "";
  const char* panelType = document["panelType"] | "";
  const char* profile = document["profile"] | "";
  const char* controlPlane = document["controlPlane"] | "";
  const char* sha256 = document["sha256"] | "";
  const char* downloadPath = document["downloadPath"] | "";
  const uint32_t fileSizeBytes = document["fileSizeBytes"] | 0u;

  if (firmwareVersion[0] == '\0'
      || boardModel[0] == '\0'
      || panelType[0] == '\0'
      || profile[0] == '\0'
      || controlPlane[0] == '\0'
      || sha256[0] == '\0'
      || downloadPath[0] == '\0'
      || fileSizeBytes == 0u) {
    return false;
  }

  info.firmwareVersion = firmwareVersion;
  info.boardModel = boardModel;
  info.panelType = panelType;
  info.profile = profile;
  info.controlPlane = controlPlane;
  info.sha256 = normalizeLowerHex(sha256);
  info.fileSizeBytes = fileSizeBytes;
  info.downloadPath = downloadPath;
  return true;
}

bool validateFirmwareReleaseInfo(const FirmwareReleaseInfo& info, const String& requestedVersion, String& errorCode, String& errorMessage) {
  if (!requestedVersion.isEmpty() && !info.firmwareVersion.equalsIgnoreCase(requestedVersion)) {
    errorCode = "firmware_version_mismatch";
    errorMessage = "Servidor retornou versao diferente da solicitada para OTA.";
    return false;
  }

  if (!info.boardModel.equalsIgnoreCase(kBoardModel)
      || !info.panelType.equalsIgnoreCase(kPanelType)
      || !info.profile.equalsIgnoreCase(kFirmwareProfile)) {
    errorCode = "firmware_incompatible";
    errorMessage = "Firmware oficial nao corresponde ao hardware/perfil deste dispositivo.";
    return false;
  }

  if (!info.controlPlane.equalsIgnoreCase("mqtt")) {
    errorCode = "firmware_control_plane_invalid";
    errorMessage = "Firmware oficial incompativel com o control plane atual.";
    return false;
  }

  if (info.sha256.length() != 64) {
    errorCode = "firmware_sha_invalid";
    errorMessage = "Manifesto de firmware sem hash SHA-256 valido.";
    return false;
  }

  if (info.fileSizeBytes == 0u) {
    errorCode = "firmware_size_invalid";
    errorMessage = "Manifesto de firmware sem tamanho valido.";
    return false;
  }

  return true;
}

bool tryFetchLatestFirmwareRelease(FirmwareReleaseInfo& info, const String& requestedVersion, String& errorCode, String& errorMessage) {
  errorCode = "";
  errorMessage = "";

  HTTPClient http;
  if (!beginHttpWithDeviceAuth(http, "/api/v1/device/firmware/latest")) {
    errorCode = "firmware_http_begin_failed";
    errorMessage = "Nao foi possivel consultar o catalogo oficial de firmware.";
    return false;
  }

  int code = http.GET();
  if (code == HTTP_CODE_NOT_FOUND) {
    errorCode = "firmware_not_found";
    errorMessage = "Nao existe pacote oficial de firmware para este dispositivo.";
    http.end();
    return false;
  }

  if (code < 200 || code >= 300) {
    errorCode = "firmware_http_error";
    errorMessage = String("Falha ao consultar firmware oficial. HTTP ") + code + ".";
    http.end();
    return false;
  }

  JsonDocument response;
  if (deserializeJson(response, http.getString()) != DeserializationError::Ok) {
    errorCode = "firmware_manifest_invalid";
    errorMessage = "Resposta de firmware oficial invalida.";
    http.end();
    return false;
  }
  http.end();

  if (!tryParseFirmwareReleaseInfo(response, info)) {
    errorCode = "firmware_manifest_invalid";
    errorMessage = "Resposta de firmware oficial incompleta.";
    return false;
  }

  return validateFirmwareReleaseInfo(info, requestedVersion, errorCode, errorMessage);
}

bool performFirmwareOta(const FirmwareReleaseInfo& info, const String& commandId, String& errorCode, String& errorMessage) {
  errorCode = "";
  errorMessage = "";

  sendCommandProgress(commandId, 35, "metadata", "Firmware oficial validado.");
  if (gWs.isConnected()) {
    gWs.disconnect();
  }

  if (gMatrixReady) {
    clearMatrix();
    commitMatrixFrame();
  }

  HTTPClient http;
  if (!beginHttpWithDeviceAuth(http, info.downloadPath)) {
    errorCode = "firmware_download_begin_failed";
    errorMessage = "Nao foi possivel iniciar o download OTA.";
    return false;
  }

  int code = http.GET();
  if (code < 200 || code >= 300) {
    errorCode = "firmware_download_http_error";
    errorMessage = String("Falha ao baixar firmware oficial. HTTP ") + code + ".";
    http.end();
    return false;
  }

  const int contentLength = http.getSize();
  if (contentLength <= 0 || static_cast<uint32_t>(contentLength) != info.fileSizeBytes) {
    errorCode = "firmware_download_size_mismatch";
    errorMessage = "Download OTA retornou tamanho diferente do manifesto oficial.";
    http.end();
    return false;
  }

  if (!Update.begin(info.fileSizeBytes)) {
    errorCode = "ota_begin_failed";
    errorMessage = String("Nao foi possivel reservar espaco OTA. code=") + Update.getError();
    http.end();
    return false;
  }

  WiFiClient* stream = http.getStreamPtr();
  if (stream == nullptr) {
    Update.abort();
    errorCode = "firmware_stream_unavailable";
    errorMessage = "Fluxo HTTP indisponivel para OTA.";
    http.end();
    return false;
  }

  mbedtls_sha256_context shaContext;
  mbedtls_sha256_init(&shaContext);
  if (mbedtls_sha256_starts_ret(&shaContext, 0) != 0) {
    Update.abort();
    mbedtls_sha256_free(&shaContext);
    errorCode = "firmware_sha_init_failed";
    errorMessage = "Falha ao inicializar validacao SHA-256 da OTA.";
    http.end();
    return false;
  }

  uint8_t buffer[4096];
  uint32_t totalRead = 0u;
  unsigned long lastDataMs = millis();
  uint8_t lastProgress = 35u;

  while (totalRead < info.fileSizeBytes) {
    const size_t availableBytes = stream->available();
    if (availableBytes == 0u) {
      if (!http.connected()) {
        break;
      }

      if (millis() - lastDataMs > 15000u) {
        Update.abort();
        mbedtls_sha256_free(&shaContext);
        errorCode = "firmware_download_timeout";
        errorMessage = "Download OTA interrompido por timeout.";
        http.end();
        return false;
      }

      delay(1);
      continue;
    }

    const size_t chunkSize = availableBytes < sizeof(buffer) ? availableBytes : sizeof(buffer);
    const int readCount = stream->readBytes(buffer, chunkSize);
    if (readCount <= 0) {
      delay(1);
      continue;
    }

    lastDataMs = millis();
    if (mbedtls_sha256_update_ret(&shaContext, buffer, static_cast<size_t>(readCount)) != 0) {
      Update.abort();
      mbedtls_sha256_free(&shaContext);
      errorCode = "firmware_sha_update_failed";
      errorMessage = "Falha ao atualizar hash SHA-256 durante OTA.";
      http.end();
      return false;
    }

    if (Update.write(buffer, static_cast<size_t>(readCount)) != static_cast<size_t>(readCount)) {
      Update.abort();
      mbedtls_sha256_free(&shaContext);
      errorCode = "ota_write_failed";
      errorMessage = String("Falha ao gravar bloco OTA. code=") + Update.getError();
      http.end();
      return false;
    }

    totalRead += static_cast<uint32_t>(readCount);
    const uint8_t progress = static_cast<uint8_t>(35u + ((static_cast<uint64_t>(totalRead) * 55u) / info.fileSizeBytes));
    if (progress >= static_cast<uint8_t>(lastProgress + 5u) || totalRead >= info.fileSizeBytes) {
      lastProgress = progress;
      sendCommandProgress(commandId, progress > 90u ? 90u : progress, "downloading", "Baixando e gravando firmware...");
    }
  }

  http.end();

  if (totalRead != info.fileSizeBytes) {
    Update.abort();
    mbedtls_sha256_free(&shaContext);
    errorCode = "firmware_download_incomplete";
    errorMessage = "Download OTA terminou antes de receber todos os bytes esperados.";
    return false;
  }

  uint8_t shaBytes[32];
  if (mbedtls_sha256_finish_ret(&shaContext, shaBytes) != 0) {
    Update.abort();
    mbedtls_sha256_free(&shaContext);
    errorCode = "firmware_sha_finish_failed";
    errorMessage = "Falha ao finalizar hash SHA-256 da OTA.";
    return false;
  }

  mbedtls_sha256_free(&shaContext);
  const String computedSha256 = bytesToLowerHex(shaBytes, sizeof(shaBytes));
  if (!computedSha256.equalsIgnoreCase(info.sha256)) {
    Update.abort();
    errorCode = "firmware_sha_mismatch";
    errorMessage = "Hash SHA-256 divergente no firmware baixado.";
    return false;
  }

  sendCommandProgress(commandId, 92, "flashing", "Validando e finalizando imagem OTA...");
  if (!Update.end()) {
    errorCode = "ota_end_failed";
    errorMessage = String("Falha ao finalizar OTA. code=") + Update.getError();
    return false;
  }

  if (!Update.isFinished()) {
    errorCode = "ota_incomplete";
    errorMessage = "OTA finalizada sem imagem completa.";
    return false;
  }

  return true;
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

void publishPendingOtaReportIfNeeded() {
  if (!gMqtt.connected() || !hasPendingOtaContext()) {
    return;
  }

  switch (gPendingOtaBootState) {
    case PendingOtaBootState::PendingVerify:
      if (!gPendingOtaPendingVerifyAnnounced) {
        const String message = String("Primeiro boot apos OTA em validacao segura por ")
            + (kOtaSelfTestWindowMs / 1000UL)
            + " s.";
        sendCommandProgress(gPendingOtaCommandId, 97, "pending-verify", message);
        (void)publishDeviceLog("info", "command", "pending-verify", message, false);
        gPendingOtaPendingVerifyAnnounced = true;
      }
      return;

    case PendingOtaBootState::ValidatedPendingReport:
      sendCommandProgress(
          gPendingOtaCommandId,
          100,
          "validated",
          String("Firmware validado com safe update mode: ") + kFirmwareVersion + ".",
          1);
      clearPendingOtaContext();
      return;

    case PendingOtaBootState::RolledBackPendingReport:
      sendCommandProgress(
          gPendingOtaCommandId,
          100,
          "rolled-back",
          gPendingOtaFailureMessage.length() > 0
              ? gPendingOtaFailureMessage
              : String("Rollback automatico apos OTA. Firmware atual: ") + kFirmwareVersion + ".",
          0);
      clearPendingOtaContext();
      return;

    case PendingOtaBootState::FailedPendingReport:
      sendCommandProgress(
          gPendingOtaCommandId,
          100,
          "failed",
          gPendingOtaFailureMessage.length() > 0
              ? gPendingOtaFailureMessage
              : "Falha ao concluir safe update mode.",
          0);
      clearPendingOtaContext();
      return;

    case PendingOtaBootState::None:
    default:
      return;
  }
}

void requestPendingOtaRollbackAndReboot(const char* errorCode, const String& errorMessage) {
  gPendingOtaFailureCode = (errorCode != nullptr && errorCode[0] != '\0') ? errorCode : "ota_self_test_failed";
  gPendingOtaFailureMessage = errorMessage;
  gPendingOtaBootState = PendingOtaBootState::FailedPendingReport;

  Serial.printf("[ota] solicitando rollback: %s (%s)\n",
      gPendingOtaFailureMessage.c_str(),
      gPendingOtaFailureCode.c_str());
  (void)publishDeviceLog("error", "command", gPendingOtaFailureCode.c_str(), gPendingOtaFailureMessage, false);

  const esp_err_t rollbackError = esp_ota_mark_app_invalid_rollback_and_reboot();
  Serial.printf("[ota] esp_ota_mark_app_invalid_rollback_and_reboot retornou %s\n", esp_err_to_name(rollbackError));
  delay(200);
  ESP.restart();
}

void processPendingOtaSafeUpdate() {
  if (gPendingOtaBootState == PendingOtaBootState::PendingVerify) {
    if (gPendingOtaValidationStartedMs == 0u) {
      gPendingOtaValidationStartedMs = millis();
    }

    if (millis() - gPendingOtaValidationStartedMs >= kOtaSelfTestWindowMs) {
      if (!String(kFirmwareVersion).equalsIgnoreCase(gPendingOtaTargetVersion)) {
        requestPendingOtaRollbackAndReboot(
            "ota_target_version_mismatch",
            "Safe update mode detectou firmware diferente da versao alvo apos o reboot.");
        return;
      }

      const esp_err_t validationError = esp_ota_mark_app_valid_cancel_rollback();
      if (validationError != ESP_OK) {
        requestPendingOtaRollbackAndReboot(
            "ota_mark_valid_failed",
            String("Falha ao confirmar a imagem OTA: ") + esp_err_to_name(validationError) + ".");
        return;
      }

      gPendingOtaBootState = PendingOtaBootState::ValidatedPendingReport;
      gPendingOtaPendingVerifyAnnounced = false;
      gPendingOtaValidationStartedMs = 0u;
      Serial.printf("[ota] imagem OTA validada com sucesso: %s\n", kFirmwareVersion);
    }
  }

  publishPendingOtaReportIfNeeded();
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

  if (strcmp(command, "update_firmware") == 0) {
    String requestedVersion = parameters["version"] | "";
    FirmwareReleaseInfo releaseInfo;
    String errorCode;
    String errorMessage;

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (isRunningAppPendingVerify()) {
      errorCode = "ota_invalid_state_pending_verify";
      errorMessage = "A imagem atual ainda nao foi validada pelo safe update mode; conclua ou aguarde o primeiro boot antes de nova OTA.";
      (void)publishDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
      sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
      return;
    }

    if (!tryFetchLatestFirmwareRelease(releaseInfo, requestedVersion, errorCode, errorMessage)) {
      (void)publishDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
      sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
      return;
    }

    if (!performFirmwareOta(releaseInfo, commandId, errorCode, errorMessage)) {
      (void)publishDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
      sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
      return;
    }

    if (!persistPendingOtaContext(commandId, kFirmwareVersion, releaseInfo.firmwareVersion)) {
      errorCode = "ota_context_persist_failed";
      errorMessage = "Firmware baixado, mas nao foi possivel registrar o contexto do safe update mode antes do reboot.";
      (void)publishDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
      sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
      return;
    }

    sendCommandProgress(commandId, 94, "rebooting", "Firmware aplicado. Reiniciando para validacao segura.");
    (void)publishPresence("offline");
    delay(250);
    ESP.restart();
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

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
bool drawBars() {
  if (!gMatrixReady) {
    return false;
  }

  uint8_t binsSnapshot[kBinsCount];
  uint8_t levelSnapshot = 0;
  uint8_t streamBrightnessSnapshot = 0;
  {
    portENTER_CRITICAL(&gStreamBufferMux);
    const uint8_t index = gBinsActiveIndex;
    memcpy(binsSnapshot, gBinsBuffers[index], sizeof(binsSnapshot));
    levelSnapshot = gLevel;
    streamBrightnessSnapshot = gStreamBrightness;
    portEXIT_CRITICAL(&gStreamBufferMux);
  }

  const uint8_t bufferIndex = gMatrixShadowBackBufferIndex;
  uint8_t* renderedHeights = gMatrixShadowBarHeights[bufferIndex];
  if (gMatrixBufferModes[bufferIndex] != MatrixBufferMode::Bars) {
    clearMatrix();
    memset(renderedHeights, 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
  }

  const uint16_t columnCount = (kBinsCount < kMatrixWidth) ? kBinsCount : kMatrixWidth;
  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    uint8_t targetHeight = 0;
    RgbColor targetColor = {0, 0, 0};
    if (x < columnCount) {
      const uint16_t binIndex = (x * kBinsCount) / columnCount;
      const uint8_t amplitude = binsSnapshot[binIndex];
      targetHeight =
          static_cast<uint8_t>((static_cast<uint16_t>(amplitude) * kMatrixHalfHeight + 254u) / 255u);
      targetColor = rainbowColorForColumn(x, columnCount);
    }

    const uint8_t previousHeight = renderedHeights[x];
    if (targetHeight > previousHeight) {
      const uint8_t deltaHeight = static_cast<uint8_t>(targetHeight - previousHeight);
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight - targetHeight),
          1,
          deltaHeight,
          targetColor);
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight + previousHeight),
          1,
          deltaHeight,
          targetColor);
    } else if (targetHeight < previousHeight) {
      const uint8_t deltaHeight = static_cast<uint8_t>(previousHeight - targetHeight);
      const RgbColor black = {0, 0, 0};
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight - previousHeight),
          1,
          deltaHeight,
          black);
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight + targetHeight),
          1,
          deltaHeight,
          black);
    }

    renderedHeights[x] = targetHeight;
  }

  gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Bars;
  return commitMatrixFrame();
}

bool drawWaveMirrorVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::WaveMirror, palette);
  const int16_t midY = kMatrixHalfHeight;
  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    const RgbColor centerColor = samplePaletteColor(effectivePalette, x / static_cast<float>(kMatrixWidth - 1u));
    drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(midY), scaleColor(centerColor, 0.70f));
  }

  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    const uint8_t amplitude = smoothBinsSample(bins, static_cast<int>(x));
    const uint8_t halfHeight = amplitudeToHeight(amplitude, static_cast<uint8_t>(kMatrixHalfHeight - 1));
    const RgbColor color = samplePaletteColor(effectivePalette, x / static_cast<float>(kMatrixWidth - 1u));
    const RgbColor glow = scaleColor(color, 0.42f);
    for (uint8_t offset = 0; offset <= halfHeight; offset++) {
      const int16_t yTop = midY - offset;
      const int16_t yBottom = midY + offset;
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(yTop), color);
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(yBottom), color);
      if (offset > 0 && x > 0) {
        drawMatrixPixel(static_cast<uint8_t>(x - 1), static_cast<uint8_t>(yTop), glow);
        drawMatrixPixel(static_cast<uint8_t>(x - 1), static_cast<uint8_t>(yBottom), glow);
      }
      if (offset > 0 && x + 1u < kMatrixWidth) {
        drawMatrixPixel(static_cast<uint8_t>(x + 1), static_cast<uint8_t>(yTop), glow);
        drawMatrixPixel(static_cast<uint8_t>(x + 1), static_cast<uint8_t>(yBottom), glow);
      }
    }
  }

  return finishBinsVisualFrame();
}

bool drawMirrorLinesVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::MirrorLines, palette);
  const int16_t midY = kMatrixHalfHeight;
  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    const uint8_t amplitude = smoothBinsSample(bins, static_cast<int>(x));
    const uint8_t halfHeight = amplitudeToHeight(amplitude, static_cast<uint8_t>(kMatrixHalfHeight - 1));
    const RgbColor color = samplePaletteColor(effectivePalette, x / static_cast<float>(kMatrixWidth - 1u));
    for (uint8_t offset = 0; offset < halfHeight; offset++) {
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(midY - offset), color);
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(midY + offset), color);
    }
  }

  return finishBinsVisualFrame();
}

bool drawMirrorBlocksVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::MirrorBlocks, palette);
  const int16_t horizon = static_cast<int16_t>(kMatrixHeight * 0.62f);
  constexpr uint16_t kBlockCount = 64;
  for (uint16_t block = 0; block < kBlockCount; block++) {
    const uint16_t x = block * 2u;
    const uint8_t amplitude = sampleBinsAverage(bins, block * 2u, (block * 2u) + 2u);
    const uint8_t topHeight = amplitudeToHeight(amplitude, static_cast<uint8_t>(horizon));
    const uint8_t reflectionHeight = static_cast<uint8_t>(topHeight * 0.45f);
    const RgbColor color = samplePaletteColor(effectivePalette, block / static_cast<float>(kBlockCount - 1u));
    fillMatrixRect(static_cast<int16_t>(x), horizon - topHeight, 2, topHeight, color);
    fillMatrixRect(static_cast<int16_t>(x), horizon, 2, reflectionHeight, scaleColor(color, 0.38f));
  }

  return finishBinsVisualFrame();
}

bool drawClassicBarsVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::ClassicBars, palette);
  constexpr uint16_t kBarCount = 64;
  for (uint16_t bar = 0; bar < kBarCount; bar++) {
    const uint16_t x = bar * 2u;
    const uint8_t amplitude = sampleBinsAverage(bins, bar * 2u, (bar * 2u) + 2u);
    const uint8_t height = amplitudeToHeight(amplitude, kMatrixHeight - 1u);
    const uint8_t previousPeak = gBinsPeakHeights[bar];
    const uint8_t peak = height > previousPeak ? height : (previousPeak > 0 ? previousPeak - 1u : 0u);
    gBinsPeakHeights[bar] = peak;
    const RgbColor color = samplePaletteColor(effectivePalette, bar / static_cast<float>(kBarCount - 1u));
    fillMatrixRect(static_cast<int16_t>(x), static_cast<int16_t>(kMatrixHeight - height), 2, height, color);
    if (peak > 0) {
      fillMatrixRect(static_cast<int16_t>(x), static_cast<int16_t>(kMatrixHeight - peak), 2, 1, scaleColor(color, 0.95f));
    }
  }

  return finishBinsVisualFrame();
}

bool drawFlowLineVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::FlowLine, palette);
  constexpr uint16_t kSampleCount = 64;
  int16_t previousX = 0;
  int16_t previousY = static_cast<int16_t>(kMatrixHeight - amplitudeToHeight(sampleBinsAverage(bins, 0, 2), kMatrixHeight - 1u));
  for (uint16_t sample = 0; sample < kSampleCount; sample++) {
    const uint16_t x = sample * 2u;
    const uint8_t amplitude = sampleBinsAverage(bins, sample * 2u, (sample * 2u) + 2u);
    const int16_t y = static_cast<int16_t>(kMatrixHeight - amplitudeToHeight(amplitude, kMatrixHeight - 2u));
    const RgbColor color = samplePaletteColor(effectivePalette, sample / static_cast<float>(kSampleCount - 1u));
    gMatrix->drawLine(previousX, previousY, x, y, rgb888ToRgb565(color.r, color.g, color.b));
    if ((sample & 0x01u) == 0u) {
      fillMatrixRect(static_cast<int16_t>(x), y, 2, static_cast<int16_t>(kMatrixHeight - y), scaleColor(color, 0.22f));
    }

    previousX = static_cast<int16_t>(x);
    previousY = y;
  }

  return finishBinsVisualFrame();
}

bool drawHistoryScanVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::HistoryScan, palette);
  constexpr uint16_t kColumnCount = 64;
  uint8_t* currentRow = gBinsHistory[gBinsHistoryHead];
  for (uint16_t column = 0; column < kColumnCount; column++) {
    currentRow[column] = sampleBinsAverage(bins, column * 2u, (column * 2u) + 2u);
  }

  gBinsHistoryHead = static_cast<uint8_t>((gBinsHistoryHead + 1u) % kMatrixHeight);

  for (uint16_t row = 0; row < kMatrixHeight; row++) {
    const int historyIndex = (static_cast<int>(gBinsHistoryHead) - 1 - static_cast<int>(row) + kMatrixHeight) % kMatrixHeight;
    const uint8_t* historyRow = gBinsHistory[historyIndex];
    for (uint16_t column = 0; column < kColumnCount; column++) {
      const uint8_t amplitude = historyRow[column];
      if (amplitude < 6u) {
        continue;
      }

      const float paletteT = clamp01f((amplitude / 255.0f) * 0.85f + (column / static_cast<float>(kColumnCount - 1u)) * 0.15f);
      const RgbColor color = samplePaletteColor(effectivePalette, paletteT);
      fillMatrixRect(static_cast<int16_t>(column * 2u), static_cast<int16_t>(kMatrixHeight - 1u - row), 2, 1, color);
    }
  }

  return finishBinsVisualFrame();
}

bool drawRadialOrbitVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::RadialOrbit, palette);
  const int16_t centerX = kMatrixWidth / 2;
  const int16_t centerY = kMatrixHeight / 2;
  constexpr uint16_t kRayCount = 48;
  const float baseRadius = 7.0f;
  const float maxRadius = 28.0f;
  for (uint16_t ray = 0; ray < kRayCount; ray++) {
    const uint16_t binStart = static_cast<uint16_t>((ray * kBinsCount) / kRayCount);
    const uint16_t binEnd = static_cast<uint16_t>(((ray + 1u) * kBinsCount) / kRayCount);
    const uint8_t amplitude = sampleBinsAverage(bins, binStart, binEnd);
    const float angle = (static_cast<float>(ray) / static_cast<float>(kRayCount)) * 6.2831853f - 1.5707963f;
    const float radius = baseRadius + ((amplitude / 255.0f) * maxRadius);
    const int16_t endX = static_cast<int16_t>(roundf(centerX + cosf(angle) * radius));
    const int16_t endY = static_cast<int16_t>(roundf(centerY + sinf(angle) * radius));
    const RgbColor color = samplePaletteColor(effectivePalette, ray / static_cast<float>(kRayCount - 1u));
    gMatrix->drawLine(centerX, centerY, endX, endY, rgb888ToRgb565(color.r, color.g, color.b));
    fillMatrixRect(endX - 1, endY - 1, 2, 2, scaleColor(color, 0.68f));
  }

  fillMatrixRect(centerX - 2, centerY - 2, 4, 4, scaleColor(samplePaletteColor(effectivePalette, 0.5f), 0.72f));
  return finishBinsVisualFrame();
}

bool drawAtmosphereVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette, uint8_t level) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::Atmosphere, palette);
  constexpr uint16_t kSampleCount = 64;
  const float time = millis() / 1000.0f;
  const float globalLevel = level / 255.0f;
  for (uint8_t ribbon = 0; ribbon < 3u; ribbon++) {
    const float baseline = kMatrixHeight * (0.24f + (ribbon * 0.22f));
    const float phase = time * (0.9f + (ribbon * 0.22f));
    int16_t previousX = 0;
    int16_t previousY = static_cast<int16_t>(baseline);
    for (uint16_t sample = 0; sample < kSampleCount; sample++) {
      const uint16_t x = sample * 2u;
      const uint8_t amplitude = sampleBinsAverage(bins, sample * 2u, (sample * 2u) + 2u);
      const float energy = amplitude / 255.0f;
      const float wave = sinf((sample * 0.22f) + phase + ribbon) * (5.0f + (globalLevel * 4.0f));
      const int16_t y = static_cast<int16_t>(roundf(baseline + wave - (energy * 16.0f)));
      const float colorT = clamp01f((sample / static_cast<float>(kSampleCount - 1u)) * 0.65f + (ribbon * 0.18f));
      const RgbColor color = samplePaletteColor(effectivePalette, colorT);
      gMatrix->drawLine(previousX, previousY, x, y, rgb888ToRgb565(color.r, color.g, color.b));
      if ((sample % 6u) == 0u) {
        fillMatrixRect(x - 1, y - 1, 3, 3, scaleColor(color, 0.52f + (energy * 0.30f)));
      }

      previousX = static_cast<int16_t>(x);
      previousY = y;
    }
  }

  const RgbColor bloom = samplePaletteColor(effectivePalette, 0.35f + (globalLevel * 0.25f));
  fillMatrixRect((kMatrixWidth / 2) - 6, (kMatrixHeight / 2) - 4, 12, 8, scaleColor(bloom, 0.30f + (globalLevel * 0.30f)));
  return finishBinsVisualFrame();
}

bool drawLaunchpadGridVisual(const uint8_t* bins) {
  clearMatrix();
  const RgbColor deviceBody = {10, 12, 16};
  const RgbColor padOff = {34, 38, 46};
  const RgbColor topOff = {58, 64, 72};
  fillMatrixRect(18, 4, 92, 56, deviceBody);
  constexpr uint8_t kGridSize = 8;
  constexpr uint8_t kPadWidth = 9;
  constexpr uint8_t kPadHeight = 5;
  constexpr uint8_t kPadGapX = 2;
  constexpr uint8_t kPadGapY = 1;
  constexpr uint8_t kGridLeft = 22;
  constexpr uint8_t kGridTop = 12;
  for (uint8_t row = 0; row < kGridSize; row++) {
    for (uint8_t col = 0; col < kGridSize; col++) {
      const uint8_t padIndex = static_cast<uint8_t>(row * kGridSize + col);
      const uint8_t target = sampleBinsAverage(bins, padIndex * 2u, (padIndex * 2u) + 2u);
      const uint8_t previous = gLaunchpadPadLevels[padIndex];
      const uint8_t levelValue = target > previous ? target : (previous > 14u ? previous - 14u : 0u);
      gLaunchpadPadLevels[padIndex] = levelValue;
      const uint8_t x = kGridLeft + col * (kPadWidth + kPadGapX);
      const uint8_t y = kGridTop + row * (kPadHeight + kPadGapY);
      fillMatrixRect(x, y, kPadWidth, kPadHeight, padOff);
      if (levelValue > 6u) {
        const float colorT = clamp01f((col / 7.0f) * 0.55f + (row / 7.0f) * 0.25f + (levelValue / 255.0f) * 0.20f);
        const RgbColor activeColor = samplePaletteColor(Hub75BinsPaletteFamily::Neon, colorT);
        fillMatrixRect(x + 1, y + 1, kPadWidth - 2, kPadHeight - 2, scaleColor(activeColor, 0.35f + (levelValue / 255.0f) * 0.65f));
      }
    }
  }

  for (uint8_t col = 0; col < kGridSize; col++) {
    const uint8_t target = sampleBinsAverage(bins, col * 4u, (col * 4u) + 4u);
    const uint8_t previous = gLaunchpadTopLevels[col];
    gLaunchpadTopLevels[col] = target > previous ? target : (previous > 18u ? previous - 18u : 0u);
    const uint8_t x = kGridLeft + col * (kPadWidth + kPadGapX) + 2u;
    fillMatrixRect(x, 7, kPadWidth - 4u, 3, topOff);
    if (gLaunchpadTopLevels[col] > 12u) {
      const RgbColor color = samplePaletteColor(Hub75BinsPaletteFamily::Plasma, col / 7.0f);
      fillMatrixRect(x, 7, kPadWidth - 4u, 3, scaleColor(color, 0.28f + (gLaunchpadTopLevels[col] / 255.0f) * 0.72f));
    }
  }

  for (uint8_t row = 0; row < kGridSize; row++) {
    const uint8_t target = sampleBinsAverage(bins, row * 4u, (row * 4u) + 4u);
    const uint8_t previous = gLaunchpadSideLevels[row];
    gLaunchpadSideLevels[row] = target > previous ? target : (previous > 18u ? previous - 18u : 0u);
    const uint8_t y = kGridTop + row * (kPadHeight + kPadGapY) + 1u;
    fillMatrixRect(112, y, 3, kPadHeight - 2u, topOff);
    if (gLaunchpadSideLevels[row] > 12u) {
      const RgbColor color = samplePaletteColor(Hub75BinsPaletteFamily::Aurora, row / 7.0f);
      fillMatrixRect(112, y, 3, kPadHeight - 2u, scaleColor(color, 0.28f + (gLaunchpadSideLevels[row] / 255.0f) * 0.72f));
    }
  }

  return finishBinsVisualFrame();
}

bool drawBinsVisual() {
  if (!gMatrixReady) {
    return false;
  }

  uint8_t binsSnapshot[kBinsCount];
  uint8_t levelSnapshot = 0;
  uint8_t flagsSnapshot = 0;
  {
    portENTER_CRITICAL(&gStreamBufferMux);
    const uint8_t index = gBinsActiveIndex;
    memcpy(binsSnapshot, gBinsBuffers[index], sizeof(binsSnapshot));
    levelSnapshot = gLevel;
    flagsSnapshot = gBinsFlags;
    portEXIT_CRITICAL(&gStreamBufferMux);
  }

  const Hub75BinsVisualStyle style = decodeBinsVisualStyle(flagsSnapshot);
  const Hub75BinsPaletteFamily palette = decodeBinsPaletteFamily(flagsSnapshot);
  const uint8_t styleId = static_cast<uint8_t>(style);
  if (styleId != gLastBinsStyleId) {
    resetBinsVisualState();
    gLastBinsStyleId = styleId;
  }

  switch (style) {
    case Hub75BinsVisualStyle::WaveMirror:
      return drawWaveMirrorVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::MirrorLines:
      return drawMirrorLinesVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::MirrorBlocks:
      return drawMirrorBlocksVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::ClassicBars:
      return drawClassicBarsVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::FlowLine:
      return drawFlowLineVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::HistoryScan:
      return drawHistoryScanVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::RadialOrbit:
      return drawRadialOrbitVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::Atmosphere:
      return drawAtmosphereVisual(binsSnapshot, palette, levelSnapshot);
    case Hub75BinsVisualStyle::LaunchpadGrid:
      return drawLaunchpadGridVisual(binsSnapshot);
    case Hub75BinsVisualStyle::LegacyFallback:
    default:
      return drawBars();
  }
}

bool drawFrame128x64() {
  if (!gMatrixReady) {
    return false;
  }

  const uint8_t bufferIndex = gMatrixShadowBackBufferIndex;
  const uint8_t frameIndex = gFrameRgb565ActiveIndex;
  const uint16_t* frame = gFrameRgb565Buffers[frameIndex];
  // Bulk RGB565 path writes the full frame directly into the HUB75 BCM back buffer.
  gMatrix->writeFrameRGB565(frame);
  memcpy(gMatrixShadowFrames[bufferIndex], frame, sizeof(gFrameRgb565Buffers[0]));
  memset(gMatrixShadowBarHeights[bufferIndex], 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
  gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Frame;
  return commitMatrixFrame();
}
}  // namespace

void setup() {
  Serial.begin(115200);
  Serial.println("[boot] inicializando firmware.");
  Serial.printf(
      "[ws] max_data_size=%u frame128x64_payload=%u\n",
      static_cast<unsigned>(WEBSOCKETS_MAX_DATA_SIZE),
      static_cast<unsigned>(kStreamFrame128x64Rgb565Size));
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
  initializeColorConversionLookups();
  resetMatrixShadowState();
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
  loadPendingOtaContext();
  initializePendingOtaBootState();

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
  const uint32_t loopStartedUs = micros();
  processSerialProvisioning();
  processPendingOtaSafeUpdate();
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

    const bool shouldStartProvisioningFallback =
        !gProvisioningPortalActive
        && gWifiDisconnectedSinceMs != 0
        && (millis() - gWifiDisconnectedSinceMs) > kWifiDisconnectProvisioningFallbackMs;
    bool provisioningStarted = false;
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

  const unsigned long nowMs = millis();
  updateHub75FallbackState(nowMs);
  if ((nowMs - gLastFrameMs) > kMatrixSignalTimeoutMs && !gMatrixSignalTimedOut) {
    portENTER_CRITICAL(&gStreamBufferMux);
    memset(gBinsBuffers, 0, sizeof(gBinsBuffers));
    gBinsActiveIndex = 0;
    gLevel = 0;
    gBinsFlags = 0;
    memset(gFrameRgb565Buffers, 0, sizeof(gFrameRgb565Buffers));
    gFrameRgb565ActiveIndex = 0;
    portEXIT_CRITICAL(&gStreamBufferMux);
    gFrameModeActive = false;
    gMatrixSignalTimedOut = true;
    gLastBinsStyleId = 0xFFu;
    resetBinsVisualState();
    gPendingMatrixPresentCountsAsApplied = false;
    markMatrixFrameDirty(false);
  }

  setMatrixBrightness(resolveAppliedBrightness());
  updateTestLedDutyFromBrightness(gAppliedBrightness);
  updateTestLed();

  const uint32_t nowUs = micros();
  if (gMatrixReady && shouldPresentMatrixFrame(nowUs)) {
    bool presented = false;
    bool presentedStreamPayload = false;
    if (gHub75FallbackState != Hub75FallbackState::None) {
      if (gHub75FallbackDirty) {
        presented = drawConnectivityFallback(gHub75FallbackState);
        if (presented) {
          gHub75FallbackDirty = false;
        }
      }
    } else {
      if (gFrameModeActive) {
        if (gMatrixFrameDirty) {
          presented = drawFrame128x64();
          presentedStreamPayload = presented;
        }
      } else if (!gMatrixSignalTimedOut || gMatrixFrameDirty) {
        presented = drawBinsVisual();
        presentedStreamPayload = presented;
      }

      if (!presented && gHub75FallbackClearPending) {
        presented = clearConnectivityFallbackFrame();
      }
    }

    if (presented) {
      if (presentedStreamPayload) {
        gMatrixFrameDirty = false;
        if (gPendingMatrixPresentCountsAsApplied) {
          gStreamFramesApplied++;
        }
      }

      gPendingMatrixPresentCountsAsApplied = false;
      gHub75FallbackClearPending = false;
    }
  }

  updateLoopHealthyPercent(elapsedMicrosSince(loopStartedUs));
}
