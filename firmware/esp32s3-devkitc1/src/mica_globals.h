#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao

#include "mica_types.h"
#include <Preferences.h>
#include <PubSubClient.h>
#include <WebSocketsClient.h>
#include <WiFi.h>
#include <freertos/semphr.h>
#include <freertos/task.h>

#if defined(MICA_PROFILE_DMA_EXP)
#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>
#endif

// ---------------------------------------------------------------------------
// Library object globals
// ---------------------------------------------------------------------------
extern Preferences gPrefs;
extern WebSocketsClient gWs;
extern WiFiClient gMqttNetClient;
extern PubSubClient gMqtt;
extern String gServerHost;
extern uint16_t gServerPort;
extern String gMqttHost;
extern uint16_t gMqttPort;
extern String gMqttRootTopic;
extern String gDeviceId;
extern String gToken;

// ---------------------------------------------------------------------------
// Active app state
// ---------------------------------------------------------------------------
extern String gActiveAppId;
extern String gActiveAppName;
extern String gActiveAppConfig;

// ---------------------------------------------------------------------------
// Stream / bins buffers
// ---------------------------------------------------------------------------
extern portMUX_TYPE gStreamBufferMux;
extern uint8_t gBinsBuffers[2][kBinsCount];
extern uint8_t gBinsActiveIndex;
extern uint8_t gLevel;
extern uint8_t gBinsFlags;
extern uint8_t gStreamBrightness;
extern uint8_t gBrightnessCap;
extern uint16_t gFrameRgb565Buffers[2][kMatrixPixelCount];
extern uint8_t gFrameRgb565ActiveIndex;

// ---------------------------------------------------------------------------
// Timing
// ---------------------------------------------------------------------------
extern unsigned long gLastFrameMs;
extern unsigned long gWsDisconnectedSinceMs;
extern unsigned long gMqttDisconnectedSinceMs;
extern unsigned long gWifiDisconnectedSinceMs;
extern unsigned long gLastTelemetryMs;

// ---------------------------------------------------------------------------
// Test LED state
// ---------------------------------------------------------------------------
extern unsigned long gTestLedUntilMs;
extern unsigned long gTestLedNextToggleMs;
extern bool gTestLedState;
extern bool gTestLedEnabled;
extern bool gTestLedPwmReady;
extern bool gAuxLedAvailable;
extern bool gOnboardTestLedAvailable;
extern uint8_t gTestLedDuty;
extern uint8_t gTestLedPulseDuty;

// ---------------------------------------------------------------------------
// Matrix / display state
// ---------------------------------------------------------------------------
extern bool gMatrixReady;
extern bool gMatrixFrameDirty;
extern bool gPendingMatrixPresentCountsAsApplied;
extern bool gMatrixSignalTimedOut;
extern uint8_t gAppliedBrightness;
extern bool gFrameModeActive;
extern uint32_t gLastMatrixPresentUs;
extern uint32_t gHub75PresentFrames;

// ---------------------------------------------------------------------------
// Loop health and performance counters
// ---------------------------------------------------------------------------
extern unsigned long gLoopWindowStartMs;
extern uint32_t gLoopWindowIterationCount;
extern uint32_t gLoopWindowHealthyCount;
extern uint8_t gLoopHealthyPercent;
extern bool gLoopHealthyPercentReady;
extern uint32_t gPerfLoopMaxUs;
extern uint32_t gPerfNetworkMaxUs;
extern uint32_t gPerfRenderMaxUs;
extern uint32_t gPerfSerialMaxUs;
extern uint32_t gPerfLastReportLoopMaxUs;
extern uint32_t gPerfLastReportNetworkMaxUs;
extern uint32_t gPerfLastReportRenderMaxUs;
extern uint32_t gPerfLastReportSerialMaxUs;
extern uint32_t gPerfRenderSkipCount;
extern unsigned long gPerfLastReportMs;
extern uint32_t gPerfHub75PresentFramesAtLastReport;

// ---------------------------------------------------------------------------
// Telemetry and stream counters
// ---------------------------------------------------------------------------
extern uint32_t gTelemetrySequence;
extern uint32_t gDeviceLogSequence;
extern bool gHasStreamLastSequence;
extern uint32_t gStreamLastSequence;
extern uint32_t gStreamFramesReceived;
extern uint32_t gStreamFramesApplied;
extern uint32_t gStreamSequenceGapCount;
extern uint32_t gStreamInvalidFrameCount;
extern uint32_t gNetworkPollDeferCount;

// ---------------------------------------------------------------------------
// Connectivity / fallback state
// ---------------------------------------------------------------------------
extern bool gProvisioningPortalActive;
extern String gWifiState;
extern String gLastWifiEvent;
extern Hub75FallbackState gHub75FallbackState;
extern Hub75FallbackState gHub75FallbackPendingState;
extern unsigned long gHub75FallbackPendingSinceMs;
extern bool gHub75FallbackDirty;
extern bool gHub75FallbackClearPending;

// ---------------------------------------------------------------------------
// OTA state
// ---------------------------------------------------------------------------
extern bool gOtaInProgress;
extern uint8_t gOtaProgressPercent;
extern const char* gOtaProgressStage;
extern volatile OtaTaskResult gOtaTaskResult;
extern TaskHandle_t gOtaDownloadTaskHandle;
extern String gOtaBridgeCommandId;
extern String gOtaBridgeTargetVersion;
extern uint8_t gOtaBridgeLastPercent;

// ---------------------------------------------------------------------------
// Misc
// ---------------------------------------------------------------------------
extern String gAuxLedUnavailableReason;
extern String gSerialInputBuffer;
extern unsigned long gLastSerialHelloMs;
extern unsigned long gSerialProvisioningWindowStartedMs;
extern bool gSerialProvisioningWindowActive;
extern unsigned long gWsFlapWindowStartMs;
extern uint16_t gWsConnectCountInWindow;
extern uint16_t gWsDisconnectCountInWindow;

// ---------------------------------------------------------------------------
// Pending OTA boot context
// ---------------------------------------------------------------------------
extern String gPendingOtaCommandId;
extern String gPendingOtaSourceVersion;
extern String gPendingOtaTargetVersion;
extern String gPendingOtaFailureCode;
extern String gPendingOtaFailureMessage;
extern unsigned long gPendingOtaValidationStartedMs;
extern bool gPendingOtaPendingVerifyAnnounced;
extern PendingOtaBootState gPendingOtaBootState;

// ---------------------------------------------------------------------------
// Color conversion LUTs
// ---------------------------------------------------------------------------
extern uint8_t gRgb5To8Lut[32];
extern uint8_t gRgb6To8Lut[64];

// ---------------------------------------------------------------------------
// Bins visual state (peak heights, history, launchpad)
// ---------------------------------------------------------------------------
extern uint8_t gBinsPeakHeights[kMatrixWidth];
extern uint8_t gBinsHistory[kMatrixHeight][kMatrixWidth];
extern uint8_t gBinsHistoryHead;
extern uint8_t gLaunchpadPadLevels[64];
extern uint8_t gLaunchpadTopLevels[8];
extern uint8_t gLaunchpadSideLevels[8];
extern uint8_t gLastBinsStyleId;

// ---------------------------------------------------------------------------
// Shadow buffer
// ---------------------------------------------------------------------------
extern uint16_t gMatrixShadowFrames[kMatrixShadowBufferCount][kMatrixPixelCount];
extern uint8_t gMatrixShadowBarHeights[kMatrixShadowBufferCount][kMatrixWidth];
extern MatrixBufferMode gMatrixBufferModes[kMatrixShadowBufferCount];
extern uint8_t gMatrixShadowBackBufferIndex;

// ---------------------------------------------------------------------------
// Panels batch state
// ---------------------------------------------------------------------------
extern SemaphoreHandle_t gPanelsBatchMutex;
extern TaskHandle_t gPanelsBatchTaskHandle;
extern PanelsBatchBuffer gPanelsBatchPending;
extern String gPanelsBatchCurrentSessionId;
extern uint32_t gPanelsBatchCurrentSequence;
extern bool gPanelsBatchCancelRequested;
extern bool gPanelsBatchUnderrun;
extern bool gAnimatedWebpBatchSupported;

// ---------------------------------------------------------------------------
// HUB75 DMA matrix (conditional)
// ---------------------------------------------------------------------------
#if defined(MICA_PROFILE_DMA_EXP)
extern MatrixPanel_I2S_DMA* gMatrix;
#endif
