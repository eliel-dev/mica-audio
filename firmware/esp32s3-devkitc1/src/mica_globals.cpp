#include "mica_globals.h"

// ---------------------------------------------------------------------------
// Library object globals
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// Active app state
// ---------------------------------------------------------------------------
String gActiveAppId;
String gActiveAppName;
String gActiveAppConfig;

// ---------------------------------------------------------------------------
// Stream / bins buffers
// ---------------------------------------------------------------------------
portMUX_TYPE gStreamBufferMux = portMUX_INITIALIZER_UNLOCKED;
uint8_t gBinsBuffers[2][kBinsCount] = {};
uint8_t gBinsActiveIndex = 0;
uint8_t gLevel = 0;
uint8_t gBinsFlags = 0;
uint8_t gStreamBrightness = 255;
uint8_t gBrightnessCap = kBrightnessDefaultCap;
uint16_t gFrameRgb565Buffers[2][kMatrixPixelCount] = {};
uint8_t gFrameRgb565ActiveIndex = 0;

// ---------------------------------------------------------------------------
// Timing
// ---------------------------------------------------------------------------
unsigned long gLastFrameMs = 0;
unsigned long gWsDisconnectedSinceMs = 0;
unsigned long gMqttDisconnectedSinceMs = 0;
unsigned long gWifiDisconnectedSinceMs = 0;
unsigned long gLastTelemetryMs = 0;

// ---------------------------------------------------------------------------
// Test LED state
// ---------------------------------------------------------------------------
unsigned long gTestLedUntilMs = 0;
unsigned long gTestLedNextToggleMs = 0;
bool gTestLedState = false;
bool gTestLedEnabled = false;
bool gTestLedPwmReady = false;
bool gAuxLedAvailable = false;
bool gOnboardTestLedAvailable = false;
uint8_t gTestLedDuty = 0;
uint8_t gTestLedPulseDuty = 0;

// ---------------------------------------------------------------------------
// Matrix / display state
// ---------------------------------------------------------------------------
bool gMatrixReady = false;
bool gMatrixFrameDirty = false;
bool gPendingMatrixPresentCountsAsApplied = false;
bool gMatrixSignalTimedOut = false;
uint8_t gAppliedBrightness = 255;
bool gFrameModeActive = false;
uint32_t gLastMatrixPresentUs = 0;
uint32_t gHub75PresentFrames = 0;

// ---------------------------------------------------------------------------
// Loop health and performance counters
// ---------------------------------------------------------------------------
unsigned long gLoopWindowStartMs = 0;
uint32_t gLoopWindowIterationCount = 0;
uint32_t gLoopWindowHealthyCount = 0;
uint8_t gLoopHealthyPercent = 0;
bool gLoopHealthyPercentReady = false;
uint32_t gPerfLoopMaxUs = 0;
uint32_t gPerfNetworkMaxUs = 0;
uint32_t gPerfRenderMaxUs = 0;
uint32_t gPerfSerialMaxUs = 0;
uint32_t gPerfLastReportLoopMaxUs = 0;
uint32_t gPerfLastReportNetworkMaxUs = 0;
uint32_t gPerfLastReportRenderMaxUs = 0;
uint32_t gPerfLastReportSerialMaxUs = 0;
uint32_t gPerfRenderSkipCount = 0;
unsigned long gPerfLastReportMs = 0;
uint32_t gPerfHub75PresentFramesAtLastReport = 0;

// ---------------------------------------------------------------------------
// Telemetry and stream counters
// ---------------------------------------------------------------------------
uint32_t gTelemetrySequence = 0;
uint32_t gDeviceLogSequence = 0;
bool gHasStreamLastSequence = false;
uint32_t gStreamLastSequence = 0;
uint32_t gStreamFramesReceived = 0;
uint32_t gStreamFramesApplied = 0;
uint32_t gStreamSequenceGapCount = 0;
uint32_t gStreamInvalidFrameCount = 0;
uint32_t gNetworkPollDeferCount = 0;
uint8_t gResetReasonCode = 0;

// ---------------------------------------------------------------------------
// Connectivity / fallback state
// ---------------------------------------------------------------------------
bool gProvisioningPortalActive = false;
String gWifiState = kWifiStateConnecting;
String gLastWifiEvent = "boot";
Hub75FallbackState gHub75FallbackState = Hub75FallbackState::None;
Hub75FallbackState gHub75FallbackPendingState = Hub75FallbackState::None;
unsigned long gHub75FallbackPendingSinceMs = 0;
bool gHub75FallbackDirty = false;
bool gHub75FallbackClearPending = false;
bool gProvisioningLaunchPending = false;
bool gProvisioningLaunchClearCredentials = false;
String gProvisioningLaunchCommandId;
String gProvisioningLaunchReason;

// ---------------------------------------------------------------------------
// OTA state
// ---------------------------------------------------------------------------
bool gOtaInProgress = false;
uint8_t gOtaProgressPercent = 0;
const char* gOtaProgressStage = "";
volatile OtaTaskResult gOtaTaskResult = OtaTaskResult::Idle;
TaskHandle_t gOtaDownloadTaskHandle = nullptr;
String gOtaBridgeCommandId;
String gOtaBridgeTargetVersion;
uint8_t gOtaBridgeLastPercent = 0;

// ---------------------------------------------------------------------------
// Misc
// ---------------------------------------------------------------------------
String gAuxLedUnavailableReason;
String gSerialInputBuffer;
unsigned long gLastSerialHelloMs = 0;
unsigned long gSerialProvisioningWindowStartedMs = 0;
bool gSerialProvisioningWindowActive = false;
unsigned long gWsFlapWindowStartMs = 0;
uint16_t gWsConnectCountInWindow = 0;
uint16_t gWsDisconnectCountInWindow = 0;

// ---------------------------------------------------------------------------
// Pending OTA boot context
// ---------------------------------------------------------------------------
String gPendingOtaCommandId;
String gPendingOtaSourceVersion;
String gPendingOtaTargetVersion;
String gPendingOtaFailureCode;
String gPendingOtaFailureMessage;
unsigned long gPendingOtaValidationStartedMs = 0;
bool gPendingOtaPendingVerifyAnnounced = false;
PendingOtaBootState gPendingOtaBootState = PendingOtaBootState::None;
bool gTaskWatchdogReady = false;
bool gLoopTaskWatchdogSubscribed = false;

// ---------------------------------------------------------------------------
// Color conversion LUTs
// ---------------------------------------------------------------------------
uint8_t gRgb5To8Lut[32] = {0};
uint8_t gRgb6To8Lut[64] = {0};

// ---------------------------------------------------------------------------
// Bins visual state (peak heights, history, launchpad)
// ---------------------------------------------------------------------------
uint8_t gBinsPeakHeights[kMatrixWidth] = {0};
uint8_t gBinsHistory[kMatrixHeight][kMatrixWidth] = {};
uint8_t gBinsHistoryHead = 0;
uint8_t gLaunchpadPadLevels[64] = {0};
uint8_t gLaunchpadTopLevels[8] = {0};
uint8_t gLaunchpadSideLevels[8] = {0};
uint8_t gLastBinsStyleId = 0xFFu;

// ---------------------------------------------------------------------------
// Shadow buffer
// ---------------------------------------------------------------------------
uint16_t gMatrixShadowFrames[kMatrixShadowBufferCount][kMatrixPixelCount] = {};
uint8_t gMatrixShadowBarHeights[kMatrixShadowBufferCount][kMatrixWidth] = {};
MatrixBufferMode gMatrixBufferModes[kMatrixShadowBufferCount] = {
    MatrixBufferMode::Unknown,
    MatrixBufferMode::Unknown};
uint8_t gMatrixShadowBackBufferIndex = 0;

// ---------------------------------------------------------------------------
// Panels batch state
// ---------------------------------------------------------------------------
SemaphoreHandle_t gPanelsBatchMutex = nullptr;
TaskHandle_t gPanelsBatchTaskHandle = nullptr;
PanelsBatchBuffer gPanelsBatchPending = {};
String gPanelsBatchCurrentSessionId;
uint32_t gPanelsBatchCurrentSequence = 0;
bool gPanelsBatchCancelRequested = false;
bool gPanelsBatchUnderrun = false;
bool gAnimatedWebpBatchSupported = true;
PanelsWorkerState gPanelsWorkerState = PanelsWorkerState::Idle;

// ---------------------------------------------------------------------------
// Control runtime state
// ---------------------------------------------------------------------------
QueueHandle_t gControlCommandQueue = nullptr;
QueueHandle_t gAsyncControlEventQueue = nullptr;
QueueHandle_t gSlowCommandQueue = nullptr;
TaskHandle_t gControlWorkerTaskHandle = nullptr;
ControlCommandEnvelope* gDeferredControlCommand = nullptr;
ControlWorkerState gControlWorkerState = ControlWorkerState::Idle;
SlowCommandKind gActiveSlowCommand = SlowCommandKind::None;
SlowCommandKind gLastSlowCommand = SlowCommandKind::None;
uint32_t gLastSlowCommandDurationMs = 0;
unsigned long gActiveSlowCommandStartedMs = 0;

// ---------------------------------------------------------------------------
// HUB75 DMA matrix (conditional)
// ---------------------------------------------------------------------------
#if defined(MICA_PROFILE_DMA_EXP)
MatrixPanel_I2S_DMA* gMatrix = nullptr;
#endif
