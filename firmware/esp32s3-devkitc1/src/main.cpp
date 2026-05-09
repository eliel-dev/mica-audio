#include <Arduino.h>
#include <esp_heap_caps.h>
#include <esp32s3/rom/rtc.h>

#include "mica_config.h"
#include "mica_globals.h"
#include "mica_commands.h"
#include "mica_display.h"
#include "mica_visuals.h"
#include "mica_network.h"
#include "mica_ota.h"
#include "mica_panels.h"
#include "mica_prefs.h"
#include "mica_provisioning.h"

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-128x64-single-canvas-mapping
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---buffer-ws-para-frame-128x64
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-anti-flicker-com-double-buffer
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-upstream-baseline-fluidity-recovery
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-60-fps-com-pacing-fisico-correto
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-diagnostic-matrix-envs
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-fallback-local-de-conectividade
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---rollback-para-ap-first-estavel
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---ap-first-com-hub75-adiado-no-boot-limpo
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md
// DOCS: docs/handoffs/2026-04-14-serial-monitor-copy-e-a3-extracao-loop.md
// DOCS: docs/handoffs/2026-04-14-ota-firmware-update-flow-e-hub75-status.md
// DOCS: docs/handoffs/2026-04-14-freertos-ota-background-task.md
// DOCS: docs/handoffs/2026-04-16-ap-first-wifi-mem-and-copy-logs.md
// DOCS: docs/handoffs/2026-04-17-firmware-control-worker-hardening.md
// DOCS: docs/handoffs/2026-04-18-wifi-reconnect-persistence-after-reset.md
// DOCS: docs/handoffs/2026-04-18-provisioned-boot-wifi-before-hub75.md

// STA-hardcoded mode: server endpoint comes from mica_config.h on every boot.
// MQTT coordinates may still be cached in NVS (populated during the first
// auto-register call) so a reboot without server reachability can still try
// MQTT against the last known broker. Device identity (deviceId/token) is the
// only credential persisted in NVS; it survives across reflashes.
static void reloadProvisioningStateFromPrefs(PrefReadSummary* summary = nullptr) {
  gServerHost = String(MICA_SERVER_HOST);
  gServerPort = MICA_SERVER_PORT;
  gMqttHost = prefsGetStringOrDefault("mqttHost", String(MICA_SERVER_HOST), summary);
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
  initializeControlCommandRuntime();
  if (isTaskWatchdogReady()) {
    subscribeCurrentTaskToWatchdog();
    gLoopTaskWatchdogSubscribed = true;
  }

  Serial.printf(
      "[wifi_connecting] preparando STA hardcoded ssid=\"%s\" server=%s:%u\n",
      MICA_WIFI_SSID,
      MICA_SERVER_HOST,
      static_cast<unsigned>(MICA_SERVER_PORT));
  setConnectivityState(kWifiStateConnecting, "boot", true);

  PrefReadSummary provisioningPrefSummary;
  reloadProvisioningStateFromPrefs(&provisioningPrefSummary);

  loadLightRuntimeStateFromPrefs();

  // STA hardcoded: bring Wi-Fi up before HUB75 to keep the boot ordering that
  // the rest of the firmware (network poll, control worker) expects.
  logBootMemorySnapshot("before_sta_connect");
  // 30 s is generous enough for a slow access point handshake but still bails
  // out fast enough to keep the loop watchdog happy if Wi-Fi is offline.
  constexpr unsigned long kStaBootConnectTimeoutMs = 30000UL;
  const bool bootWifiConnected = connectStaHardcoded(kStaBootConnectTimeoutMs);
  logBootMemorySnapshot("after_sta_connect");

  initializeHub75RuntimeFromPrefs();

  if (bootWifiConnected) {
    if (autoRegisterIfNeeded()) {
      Serial.printf(
          "[boot_ready] STA conectado e device registrado deviceId=%s.\n",
          gDeviceId.c_str());
      gLastWifiReconnectAttemptMs = 0;
      connectMqtt();
      connectWebSocket();
    } else {
      Serial.println(
          "[boot_pending] STA conectado, mas auto-register falhou; tentaremos novamente nas iteracoes do loop.");
      setConnectivityState(kWifiStateConnected, "auto_register_pending", true);
      gWifiDisconnectedSinceMs = 0;
    }
  } else {
    Serial.println("[wifi_waiting_sta_hardcoded] sem Wi-Fi no boot; mantendo retry no loop.");
    setConnectivityState(kWifiStateDisconnected, "wifi_waiting_sta_hardcoded", true);
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
