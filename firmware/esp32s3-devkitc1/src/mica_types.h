#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-128x64-single-canvas-mapping
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---buffer-ws-para-frame-128x64
// DOCS: docs/handoffs/2026-04-17-control-worker-watchdog-and-wifi-heap-regression-fix.md
// DOCS: docs/handoffs/2026-04-18-wifi-reconnect-persistence-after-reset.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md

#include <Arduino.h>
#include "firmware_version.h"

#if __has_include("firmware_version.auto.h")
#include "firmware_version.auto.h"
#endif

#ifndef MICA_FIRMWARE_VERSION
#define MICA_FIRMWARE_VERSION MICA_FIRMWARE_VERSION_FALLBACK
#endif

// ---------------------------------------------------------------------------
// Stream and protocol constants
// ---------------------------------------------------------------------------
constexpr uint8_t kBinsCount = MICA_STREAM_BINS;
constexpr size_t kStreamFrameSize = 145;
constexpr size_t kStreamFrame128x64Rgb565Size = 16400;
constexpr size_t kExpectedWebSocketMaxDataSize = 32768;
constexpr uint8_t kStreamVersion = 2;
constexpr uint8_t kStreamBinsMessageType = 1;
constexpr uint8_t kStreamFrame128x64Rgb565MessageType = 2;
constexpr const char* kPanelsBatchExpectedContentType = "image/webp";

// ---------------------------------------------------------------------------
// Timing and network constants
// ---------------------------------------------------------------------------
constexpr unsigned long kWifiDisconnectProvisioningFallbackMs = 20000;
constexpr unsigned long kWifiBootConnectGraceMs = 5000;
constexpr unsigned long kWifiReconnectRetryMs = 5000;
constexpr unsigned long kMqttReconnectRetryMs = 5000;
constexpr unsigned long kWsReconnectRetryMs = 60000;
constexpr unsigned long kWsAutoReconnectIntervalMs = 2000;
constexpr unsigned long kWsFlapReportWindowMs = 60000;
constexpr uint8_t kWsFlapReportThreshold = 3;
constexpr unsigned long kTelemetryIntervalMs = 2000;
constexpr unsigned long kLoopHealthWindowMs = 5000;
constexpr unsigned long kSerialHelloIntervalMs = 3000;
constexpr unsigned long kSerialProvisioningBootGraceMs = 60000;
constexpr unsigned long kMatrixSignalTimeoutMs = 15000;
constexpr unsigned long kConnectivityFallbackDebounceMs = 1000;
constexpr size_t kSerialInputMaxLength = 1024;
constexpr unsigned long kWifiConnectAttemptTimeoutMs = 20000;
constexpr uint32_t kHealthyLoopThresholdUs = 25000;
constexpr uint32_t kNetworkPollBudgetUs = 8000;
constexpr unsigned long kOtaSelfTestWindowMs = 10000;
constexpr uint16_t kDefaultMqttPort = 5273;
constexpr uint16_t kVisualUdpPort = 5274;
constexpr size_t kVisualUdpFrameHeaderSize = 12;
constexpr size_t kVisualUdpFrameTagSize = 16;
constexpr size_t kVisualUdpFrameMaxDatagramSize = kVisualUdpFrameHeaderSize + kStreamFrameSize + kVisualUdpFrameTagSize;
constexpr const char* kDefaultMqttRootTopic = "mica/v1/devices";
constexpr uint16_t kMqttPacketBufferBytes = 32768;
constexpr uint8_t kControlCommandQueueDepth = 8;
constexpr uint8_t kAsyncControlEventQueueDepth = 12;
constexpr uint8_t kSlowCommandQueueDepth = 2;
constexpr uint8_t kMaxControlCommandsPerLoop = 4;
constexpr uint8_t kMaxAsyncEventsPerLoop = 8;
constexpr uint32_t kTaskWatchdogTimeoutMs = 30000;

// ---------------------------------------------------------------------------
// Matrix dimensions
// ---------------------------------------------------------------------------
constexpr uint8_t kMatrixWidth = MICA_MATRIX_WIDTH;
constexpr uint8_t kMatrixHeight = MICA_MATRIX_HEIGHT;
constexpr uint8_t kMatrixHalfHeight = kMatrixHeight / 2;
constexpr size_t kMatrixPixelCount = static_cast<size_t>(kMatrixWidth) * static_cast<size_t>(kMatrixHeight);
constexpr uint8_t kHub75ColorDepthBits = 6;

// ---------------------------------------------------------------------------
// Panels batch constants
// ---------------------------------------------------------------------------
constexpr uint8_t kPanelsBatchTargetFps = 30;
constexpr uint16_t kPanelsBatchDurationMs = 1000;
constexpr uint8_t kPanelsBatchExpectedFrameCount = 30;
constexpr unsigned long kPanelsBatchWaitChunkMs = 5;
constexpr bool kPanelsPerfLoggingEnabled = false;
constexpr uint16_t kPanelsBatchTaskStackSize = 16384;
constexpr UBaseType_t kPanelsBatchTaskPriority = 2;
constexpr uint16_t kControlWorkerTaskStackSize = 12288;
constexpr UBaseType_t kControlWorkerTaskPriority = 1;

// ---------------------------------------------------------------------------
// OTA task constants
// ---------------------------------------------------------------------------
constexpr uint16_t kOtaDownloadTaskStackSize = 8192;
constexpr UBaseType_t kOtaDownloadTaskPriority = 1;

// ---------------------------------------------------------------------------
// HUB75 build-time overrides
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// Board pin definitions
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// Test LED constants
// ---------------------------------------------------------------------------
constexpr unsigned long kTestLedDurationMs = 1500;
constexpr unsigned long kTestLedTogglePeriodMs = 120;
constexpr uint16_t kTestLedPwmFrequencyHz = 5000;
constexpr uint8_t kTestLedPwmResolutionBits = 8;

// ---------------------------------------------------------------------------
// Firmware identity
// ---------------------------------------------------------------------------
constexpr const char* kFirmwareProfile = "dma_exp";
constexpr const char* kFirmwareVersion = MICA_FIRMWARE_VERSION;
constexpr uint8_t kBrightnessSafeMin = 30;
constexpr uint8_t kBrightnessSafeMax = 160;
constexpr uint8_t kBrightnessDefaultCap = 160;

// ---------------------------------------------------------------------------
// WiFi / provisioning state strings
// ---------------------------------------------------------------------------
constexpr const char* kWifiStateConnecting = "connecting";
constexpr const char* kWifiStateConnected = "connected";
constexpr const char* kWifiStatePortal = "portal";
constexpr const char* kWifiStateDisconnected = "disconnected";
constexpr const char* kSerialProvisioningProtocol = "mica.serial.v1";

// ---------------------------------------------------------------------------
// OTA preference keys
// ---------------------------------------------------------------------------
constexpr const char* kPrefsOtaCommandId = "ota_cmd";
constexpr const char* kPrefsOtaSourceVersion = "ota_src";
constexpr const char* kPrefsOtaTargetVersion = "ota_tgt";

// ---------------------------------------------------------------------------
// Security profile
// ---------------------------------------------------------------------------
#if defined(MICA_SECURITY_PROFILE_RELEASE)
constexpr const char* kSecurityProfile = "release";
#else
constexpr const char* kSecurityProfile = "dev";
#endif

// ---------------------------------------------------------------------------
// Static assertions
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------
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
  Updating = 4,
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

enum class OtaTaskResult : uint8_t {
  Idle = 0,
  Running = 1,
  Success = 2,
  Failed = 3,
};

enum class ControlCommandSource : uint8_t {
  Mqtt = 0,
  WebSocket = 1,
};

enum class SlowCommandKind : uint8_t {
  None = 0,
  EnterProvisioning = 1,
  UpdateFirmware = 2,
  QueuePanelsBatch = 3,
};

enum class ControlWorkerState : uint8_t {
  Idle = 0,
  PanelsDownloading = 1,
  PanelsValidating = 2,
  FetchingFirmware = 3,
  AwaitingOtaResult = 4,
  ProvisioningPending = 5,
  Failed = 6,
};

enum class PanelsWorkerState : uint8_t {
  Idle = 0,
  PendingBatch = 1,
  Decoding = 2,
  Presenting = 3,
  Cancelled = 4,
  Failed = 5,
};

enum class AsyncControlEventKind : uint8_t {
  CommandProgress = 0,
  DeviceLog = 1,
  StartOta = 2,
};

enum class PendingOtaBootState : uint8_t {
  None = 0,
  PendingVerify = 1,
  ValidatedPendingReport = 2,
  RolledBackPendingReport = 3,
  FailedPendingReport = 4,
};

// ---------------------------------------------------------------------------
// Structs
// ---------------------------------------------------------------------------
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

struct OtaTaskParams {
  String downloadPath;
  String sha256;
  uint32_t fileSizeBytes;
  String commandId;
  String sourceVersion;
  String firmwareVersion;
};

struct PanelsBatchBuffer {
  String panelsSessionId;
  uint32_t batchSequence = 0;
  uint8_t* data = nullptr;
  size_t length = 0;
  uint16_t frameCount = 0;
  uint16_t durationMs = 0;
};

struct PanelsBatchDownloadRequest {
  String panelsSessionId;
  String downloadUrl;
  String expectedSha256;
  String expectedContentType;
  uint32_t batchSequence = 0;
  uint32_t fileSizeBytes = 0;
  uint32_t frameCount = 0;
  uint32_t durationMs = 0;
};

struct ControlCommandEnvelope {
  ControlCommandSource source = ControlCommandSource::Mqtt;
  String command;
  String commandId;
  String payloadJson;
};

struct SlowCommandRequest {
  SlowCommandKind kind = SlowCommandKind::None;
  String commandId;
  String requestedVersion;
  PanelsBatchDownloadRequest panels;
};

struct AsyncControlEvent {
  AsyncControlEventKind kind = AsyncControlEventKind::CommandProgress;
  String commandId;
  uint8_t progressPercent = 0;
  String stage;
  String message;
  int successFlag = -1;
  String logLevel;
  String logCategory;
  String logEventCode;
  bool includeTelemetrySequence = true;
  OtaTaskParams* otaParams = nullptr;
  String otaTargetVersion;
};

// ---------------------------------------------------------------------------
// Inline utilities
// ---------------------------------------------------------------------------
constexpr uint32_t ceilDivideU32(uint32_t numerator, uint32_t denominator) {
  return denominator == 0 ? 0u : (numerator + denominator - 1u) / denominator;
}
