#include <Arduino.h>
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

static void reloadProvisioningStateFromPrefs(PrefReadSummary* summary = nullptr) {
  gServerHost = prefsGetStringOrDefault("host", "", summary);
  gServerPort = prefsGetPortOrDefault("port", 5272, summary);
  gMqttHost = prefsGetStringOrDefault("mqttHost", "", summary);
  gMqttPort = prefsGetPortOrDefault("mqttPort", 5273, summary);
  gMqttRootTopic = prefsGetStringOrDefault("mqttRootTopic", String(kDefaultMqttRootTopic), summary);
  normalizeMqttConfig();
  gDeviceId = prefsGetStringOrDefault("deviceId", "", summary);
  gToken = prefsGetStringOrDefault("token", "", summary);
}

static bool isProvisioningIncomplete() {
  const bool missingServerConfig = gServerHost.isEmpty() || gServerPort == 0;
  const bool missingDeviceCredentials = gDeviceId.isEmpty() || gToken.isEmpty();
  return missingServerConfig || missingDeviceCredentials;
}

static const char* resolveProvisioningBootReason() {
  return (gServerHost.isEmpty() || gServerPort == 0)
      ? "boot_missing_server_config"
      : "boot_missing_device_credentials";
}

static void loadRuntimeStateFromPrefs() {
  gBrightnessCap = clampBrightnessToSafeRange(static_cast<int>(prefsGetUCharOrDefault("brightnessCap", kBrightnessDefaultCap)));
  gTestLedEnabled = prefsGetBoolOrDefault("testLedEnabled", false);
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

  gActiveAppId = prefsGetStringOrDefault("activeAppId", "");
  gActiveAppName = prefsGetStringOrDefault("activeAppName", "");
  gActiveAppConfig = prefsGetStringOrDefault("activeAppConfig", "");
  loadPendingOtaContext();
  initializePendingOtaBootState();
  initializePanelsBatchRuntime();
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

  Serial.println("[wifi_connecting] preparando conectividade.");
  setConnectivityState(kWifiStateConnecting, "boot", true);

  PrefReadSummary provisioningPrefSummary;
  reloadProvisioningStateFromPrefs(&provisioningPrefSummary);

  bool bootWifiConnected = false;
  bool provisioningIncomplete = isProvisioningIncomplete();
  if (provisioningIncomplete) {
    logPrefsMissingSummary("boot_incomplete", provisioningPrefSummary);
    const char* bootReason = resolveProvisioningBootReason();
    Serial.printf(
        "[boot] configuracao incompleta; abrindo provisioning AP imediatamente (%s).\n",
        bootReason);
    if (gLoopTaskWatchdogSubscribed) {
      unsubscribeCurrentTaskFromWatchdog();
      gLoopTaskWatchdogSubscribed = false;
    }

    (void)startProvisioningPortal(bootReason);

    if (isTaskWatchdogReady()) {
      subscribeCurrentTaskToWatchdog();
      gLoopTaskWatchdogSubscribed = true;
    }

    reloadProvisioningStateFromPrefs();
    provisioningIncomplete = isProvisioningIncomplete();
    bootWifiConnected = WiFi.status() == WL_CONNECTED;
  }

  loadRuntimeStateFromPrefs();

  if (!provisioningIncomplete && !bootWifiConnected) {
    WiFi.mode(WIFI_STA);
    WiFi.begin();

    unsigned long bootWifiWaitStart = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - bootWifiWaitStart) < 5000) {
      processSerialProvisioning();
      resetTaskWatchdog();
      delay(120);
    }

    bootWifiConnected = WiFi.status() == WL_CONNECTED;
  }

  if (provisioningIncomplete) {
    if (bootWifiConnected) {
      Serial.println("[wifi_connected] Wi-Fi conectado, mas provisioning ainda incompleto apos o portal.");
      setConnectivityState(kWifiStateConnected, "boot_provisioning_incomplete", true);
      gWifiDisconnectedSinceMs = 0;
    } else {
      Serial.println("[wifi_connecting] provisioning ainda incompleto apos o portal AP.");
      setConnectivityState(kWifiStateDisconnected, "boot_provisioning_incomplete", true);
      gWifiDisconnectedSinceMs = millis();
    }
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
