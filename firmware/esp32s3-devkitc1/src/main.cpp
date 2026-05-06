#include <Arduino.h>
#include <esp_heap_caps.h>
#include <esp32s3/rom/rtc.h>

#include "mica_globals.h"
#include "mica_commands.h"
#include "mica_display.h"
#include "mica_visuals.h"
#include "mica_network.h"
#include "mica_ota.h"
#include "mica_panels.h"
#include "mica_prefs.h"
#include "mica_provisioning.h"
#include "mica_session.h"
#include "mica_config.h"

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-128x64-single-canvas-mapping
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---buffer-ws-para-frame-128x64
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-anti-flicker-com-double-buffer
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-upstream-baseline-fluidity-recovery
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-60-fps-com-pacing-fisico-correto
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-diagnostic-matrix-envs
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-fallback-local-de-conectividade
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---rollback-para-ap-first-estavel
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---ap-first-com-hub75-adiado-no-boot-limpo
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md
// DOCS: docs/handoffs/2026-04-14-serial-monitor-copy-e-a3-extracao-loop.md
// DOCS: docs/handoffs/2026-04-14-ota-firmware-update-flow-e-hub75-status.md
// DOCS: docs/handoffs/2026-04-14-freertos-ota-background-task.md
// DOCS: docs/handoffs/2026-04-16-ap-first-wifi-mem-and-copy-logs.md
// DOCS: docs/handoffs/2026-04-17-firmware-control-worker-hardening.md
// DOCS: docs/handoffs/2026-04-18-wifi-reconnect-persistence-after-reset.md
// DOCS: docs/handoffs/2026-04-18-provisioned-boot-wifi-before-hub75.md
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
// DOCS: docs/handoffs/2026-05-04-esp32s3-ap-portal-rollback.md
// DOCS: docs/handoffs/2026-05-05-station-mode-hardcoded-wifi.md

static void reloadProvisioningStateFromPrefs(PrefReadSummary* summary = nullptr) {
  gServerHost = prefsGetStringOrDefault("host", MICA_SERVER_HOST, summary);
  gServerPort = prefsGetPortOrDefault("port", MICA_SERVER_PORT, summary);
  gMqttHost = prefsGetStringOrDefault("mqttHost", "", summary);
  gMqttPort = prefsGetPortOrDefault("mqttPort", 5273, summary);
  gMqttRootTopic = prefsGetStringOrDefault("mqttRootTopic", String(kDefaultMqttRootTopic), summary);
  normalizeMqttConfig();
  gDeviceId = prefsGetStringOrDefault("deviceId", "", summary);
  gToken = prefsGetStringOrDefault("token", "", summary);
}

static uint32_t sanitizeLargestFreeBlock(size_t largestRawBytes, uint32_t freeBytes) {
  if (freeBytes == 0 || largestRawBytes == 0) {
    return 0;
  }

  const size_t clampedBytes = largestRawBytes > freeBytes ? freeBytes : largestRawBytes;
  return clampedBytes > UINT32_MAX ? UINT32_MAX : static_cast<uint32_t>(clampedBytes);
}

static void logBootMemorySnapshot(const char* stage) {
  const uint32_t freeHeapBytes = ESP.getFreeHeap();
  const uint32_t largestHeapBlockBytes =
      sanitizeLargestFreeBlock(heap_caps_get_largest_free_block(MALLOC_CAP_8BIT), freeHeapBytes);
  const uint32_t largestDmaBlockBytes =
      sanitizeLargestFreeBlock(heap_caps_get_largest_free_block(MALLOC_CAP_DMA | MALLOC_CAP_INTERNAL), freeHeapBytes);

  Serial.printf(
      "[boot_mem] stage=%s freeHeapBytes=%lu largestHeapBlockBytes=%lu largestDmaBlockBytes=%lu\n",
      stage != nullptr ? stage : "unknown",
      static_cast<unsigned long>(freeHeapBytes),
      static_cast<unsigned long>(largestHeapBlockBytes),
      static_cast<unsigned long>(largestDmaBlockBytes));
}

static void loadLightRuntimeStateFromPrefs() {
  gBrightnessCap = clampBrightnessToSafeRange(static_cast<int>(prefsGetUCharOrDefault("brightnessCap", kBrightnessDefaultCap)));
  gTestLedEnabled = prefsGetBoolOrDefault("testLedEnabled", false);
  gStreamBrightness = gBrightnessCap;
  gAppliedBrightness = resolveAppliedBrightness();
  initializeColorConversionLookups();
  initializeOnboardTestLed();
  initializeAuxLed();
  updateTestLedDutyFromBrightness(resolveAppliedBrightness());
  applyTestLedState();

  gActiveAppId = prefsGetStringOrDefault("activeAppId", "");
  gActiveAppName = prefsGetStringOrDefault("activeAppName", "");
  gActiveAppConfig = prefsGetStringOrDefault("activeAppConfig", "");
  loadPendingOtaContext();
  initializePendingOtaBootState();
}

static void initializeHub75RuntimeFromPrefs() {
  logBootMemorySnapshot("before_hub75_init");
  resetMatrixShadowState();
  if (!initMatrixDisplay()) {
    Serial.println("Painel HUB75 indisponivel: exibicao de barras desativada.");
  }
  initializePanelsBatchRuntime();
  logBootMemorySnapshot("after_hub75_init");
}

void processSignalTimeout() {
  const unsigned long nowMs = millis();
  updateHub75FallbackState(nowMs);
  if ((nowMs - gLastFrameMs) > kMatrixSignalTimeoutMs && !gMatrixSignalTimedOut) {
    portENTER_CRITICAL(&gStreamBufferMux);
    memset(gBinsBuffers, 0, sizeof(gBinsBuffers));
    gBinsActiveIndex = 0;
    gLevel = 0;
    gBinsFlags = 0;
    memset(gFrameRgb565Buffers, 0, 2 * kFrameRgb565BufferRowBytes);
    gFrameRgb565ActiveIndex = 0;
    portEXIT_CRITICAL(&gStreamBufferMux);
    gFrameModeActive = false;
    gMatrixSignalTimedOut = true;
    gLastBinsStyleId = 0xFFu;
    resetBinsVisualState();
    gPendingMatrixPresentCountsAsApplied = false;
    markMatrixFrameDirty(false);
  }
}

bool processRenderFrame() {
  const uint32_t nowUs = micros();
  if (!gMatrixReady || !shouldPresentMatrixFrame(nowUs)) {
    if (gMatrixReady) {
      gPerfRenderSkipCount++;
    }
    return false;
  }

  bool presented = false;
  bool presentedStreamPayload = false;
  if (gHub75FallbackState != Hub75FallbackState::None) {
    const bool isUpdating = gHub75FallbackState == Hub75FallbackState::Updating;
    if (gHub75FallbackDirty || isUpdating) {
      presented = drawConnectivityFallback(gHub75FallbackState);
      if (presented && !isUpdating) {
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

  return presented;
}

void setup() {
  Serial.begin(115200);
  gResetReasonCode = static_cast<uint8_t>(rtc_get_reset_reason(0));
  Serial.println("[boot] inicializando firmware.");
  Serial.printf("[boot] reset_reason_cpu0=%u\n", static_cast<unsigned>(gResetReasonCode));
  Serial.printf(
      "[ws] max_data_size=%u frame128x64_payload=%u\n",
      static_cast<unsigned>(WEBSOCKETS_MAX_DATA_SIZE),
      static_cast<unsigned>(kStreamFrame128x64Rgb565Size));
  if (strcmp(kSecurityProfile, "dev") == 0) {
    Serial.printf("MicaAudio firmware board=%s profile=%s security=%s\\n", kBoardModel, kFirmwareProfile, kSecurityProfile);
  }

  gPrefs.begin("micaaudio", false);
  if (!initializePsramBuffers()) {
    Serial.println("[boot] FALHA CRITICA: PSRAM buffers nao alocados. Sistema instavel.");
  }
  initializeControlCommandRuntime();
  if (isTaskWatchdogReady()) {
    subscribeCurrentTaskToWatchdog();
    gLoopTaskWatchdogSubscribed = true;
  }

  Serial.println("[wifi_connecting] preparando conectividade.");
  setConnectivityState(kWifiStateConnecting, "boot", true);

  PrefReadSummary provisioningPrefSummary;
  reloadProvisioningStateFromPrefs(&provisioningPrefSummary);

  loadLightRuntimeStateFromPrefs();

  bool bootWifiConnected = false;
  logBootMemorySnapshot("before_wifi_connect");
  WiFi.setAutoReconnect(true);
  WiFi.mode(WIFI_STA);
  Serial.printf("[wifi_boot] conectando em SSID='%s'\n", MICA_WIFI_SSID);
  WiFi.begin(MICA_WIFI_SSID, MICA_WIFI_PASSWORD);
  gLastWifiReconnectAttemptMs = millis();

  unsigned long bootWifiWaitStart = millis();
  while (WiFi.status() != WL_CONNECTED && (millis() - bootWifiWaitStart) < kWifiBootConnectGraceMs) {
    processSerialProvisioning();
    resetTaskWatchdog();
    delay(120);
  }

  bootWifiConnected = WiFi.status() == WL_CONNECTED;
  if (!bootWifiConnected) {
    const int status = WiFi.status();
    Serial.printf(
        "[wifi_boot] falha ao conectar no grace period. status=%d (%s)\n",
        status,
        resolveWifiStatusText(status));
  } else {
    Serial.printf(
        "[wifi_boot] conectado. ip=%s mac=%s rssi=%d\n",
        WiFi.localIP().toString().c_str(),
        WiFi.macAddress().c_str(),
        WiFi.RSSI());
  }
  logBootMemorySnapshot("after_wifi_connect");

  initializeHub75RuntimeFromPrefs();

  if (bootWifiConnected) {
    Serial.println("[wifi_connected] Wi-Fi conectado no boot.");
    setConnectivityState(kWifiStateConnected, "wifi_connected", true);
    gLastWifiReconnectAttemptMs = 0;
    connectMqtt();
    connectWebSocket();
  } else {
    Serial.println("[wifi_waiting_retry] sem Wi-Fi no boot; aguardando reconexao.");
    setConnectivityState(kWifiStateDisconnected, "wifi_waiting_retry", true);
    gWifiDisconnectedSinceMs = millis();
    gLastWifiReconnectAttemptMs = millis();
  }

  sendSerialHello();
}

void loop() {
  const uint32_t loopStartedUs = micros();
  processSerialProvisioning();
  processQueuedControlCommands();
  processAsyncControlEvents();
  processClientSessionRuntime(millis());
  const uint32_t serialDoneUs = micros();
  processPendingOtaSafeUpdate();
  processOtaProgressBridge();
  processProvisioningLaunchRequest();

  const uint32_t networkStartUs = micros();
  processNetworkPoll();

  processSignalTimeout();
  setMatrixBrightness(resolveAppliedBrightness());
  updateTestLedDutyFromBrightness(gAppliedBrightness);
  updateTestLed();

  const uint32_t renderStartUs = micros();
  processRenderFrame();

  const uint32_t loopEndUs = micros();
  const uint32_t serialUs = serialDoneUs - loopStartedUs;
  const uint32_t networkUs = renderStartUs - networkStartUs;
  const uint32_t renderUs = loopEndUs - renderStartUs;
  const uint32_t loopTotalUs = loopEndUs - loopStartedUs;
  if (loopTotalUs > gPerfLoopMaxUs) { gPerfLoopMaxUs = loopTotalUs; }
  if (networkUs > gPerfNetworkMaxUs) { gPerfNetworkMaxUs = networkUs; }
  if (renderUs > gPerfRenderMaxUs) { gPerfRenderMaxUs = renderUs; }
  if (serialUs > gPerfSerialMaxUs) { gPerfSerialMaxUs = serialUs; }

  resetTaskWatchdog();
  updateLoopHealthyPercent(loopTotalUs);
  reportPerfMetrics();
}
