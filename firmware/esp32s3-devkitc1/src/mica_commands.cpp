// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#campos-do-payload-de-telemetria-ws
// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md
// DOCS: docs/handoffs/2026-04-17-firmware-control-worker-hardening.md
// DOCS: docs/handoffs/2026-04-17-control-worker-watchdog-and-wifi-heap-regression-fix.md
// DOCS: docs/handoffs/2026-04-18-provisioned-boot-wifi-before-hub75.md

#include "mica_commands.h"

#include <esp_err.h>
#include <esp_task_wdt.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>

#include "mica_display.h"
#include "mica_globals.h"
#include "mica_network.h"
#include "mica_ota.h"
#include "mica_panels.h"
#include "mica_provisioning.h"
#include "mica_session.h"

namespace {

enum class ControlCommandHandleResult : uint8_t {
  Handled = 0,
  Deferred = 1,
};

static bool tryParseBooleanParameter(JsonVariantConst value, bool& output) {
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

static bool tryParseUnsignedLongParameter(JsonVariantConst value, uint32_t& output) {
  if (value.isNull()) {
    return false;
  }

  if (value.is<uint32_t>()) {
    output = value.as<uint32_t>();
    return true;
  }

  if (value.is<unsigned long>()) {
    output = static_cast<uint32_t>(value.as<unsigned long>());
    return true;
  }

  String raw = value.as<String>();
  raw.trim();
  if (raw.length() == 0) {
    return false;
  }

  char* endPtr = nullptr;
  const unsigned long parsed = strtoul(raw.c_str(), &endPtr, 10);
  if (endPtr == raw.c_str() || (endPtr != nullptr && *endPtr != '\0') || parsed > UINT32_MAX) {
    return false;
  }

  output = static_cast<uint32_t>(parsed);
  return true;
}

static void deleteControlCommandEnvelope(ControlCommandEnvelope*& envelope) {
  delete envelope;
  envelope = nullptr;
}

static void deleteSlowCommandRequest(SlowCommandRequest*& request) {
  delete request;
  request = nullptr;
}

static void deleteAsyncControlEvent(AsyncControlEvent*& event) {
  if (event != nullptr && event->otaParams != nullptr) {
    delete event->otaParams;
    event->otaParams = nullptr;
  }

  delete event;
  event = nullptr;
}

static const char* slowCommandKindToName(SlowCommandKind kind) {
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

static bool isSlowCommandDomainBusy() {
  return gActiveSlowCommand != SlowCommandKind::None || gProvisioningLaunchPending;
}

static const char* currentSlowCommandBusyName() {
  if (gActiveSlowCommand != SlowCommandKind::None) {
    return slowCommandKindToName(gActiveSlowCommand);
  }

  return gProvisioningLaunchPending ? "enter_provisioning" : "";
}

static bool tryBeginSlowCommand(SlowCommandKind kind) {
  if (kind == SlowCommandKind::None || gActiveSlowCommand != SlowCommandKind::None) {
    return false;
  }

  gActiveSlowCommand = kind;
  gActiveSlowCommandStartedMs = millis();
  gLastSlowCommand = kind;
  gLastSlowCommandDurationMs = 0;
  return true;
}

static void clearProvisioningLaunchRequest() {
  gProvisioningLaunchPending = false;
  gProvisioningLaunchClearCredentials = false;
  gProvisioningLaunchCommandId = "";
  gProvisioningLaunchReason = "";
}

static bool enqueueAsyncControlEvent(AsyncControlEvent* event, TickType_t waitTicks) {
  if (event == nullptr || gAsyncControlEventQueue == nullptr) {
    return false;
  }

  AsyncControlEvent* queuedEvent = event;
  return xQueueSend(gAsyncControlEventQueue, &queuedEvent, waitTicks) == pdTRUE;
}

static bool enqueueSlowCommandRequest(SlowCommandRequest* request) {
  if (request == nullptr || gSlowCommandQueue == nullptr) {
    return false;
  }

  SlowCommandRequest* queuedRequest = request;
  return xQueueSend(gSlowCommandQueue, &queuedRequest, pdMS_TO_TICKS(20)) == pdTRUE;
}

static String copyPayloadToString(const uint8_t* payload, size_t length) {
  String result;
  result.reserve(length);
  for (size_t index = 0; index < length; index++) {
    result += static_cast<char>(payload[index]);
  }

  return result;
}

static void initializeTaskWatchdogRuntime() {
  if (gTaskWatchdogReady) {
    return;
  }

  esp_task_wdt_config_t config = {};
  config.timeout_ms = kTaskWatchdogTimeoutMs;
  config.idle_core_mask = 0;
  config.trigger_panic = true;

  esp_err_t result = esp_task_wdt_reconfigure(&config);
  if (result == ESP_ERR_INVALID_STATE) {
    result = esp_task_wdt_init(&config);
  }

  if (result == ESP_OK) {
    gTaskWatchdogReady = true;
    Serial.printf("[wdt] task watchdog configurado timeout_ms=%lu\n", static_cast<unsigned long>(kTaskWatchdogTimeoutMs));
    return;
  }

  gTaskWatchdogReady = false;
  Serial.printf("[wdt] falha ao configurar task watchdog: %s\n", esp_err_to_name(result));
}

static bool ensureControlWorkerStarted() {
  if (gSlowCommandQueue == nullptr) {
    gSlowCommandQueue = xQueueCreate(kSlowCommandQueueDepth, sizeof(SlowCommandRequest*));
  }

  if (gSlowCommandQueue == nullptr) {
    Serial.println("[control] falha ao criar fila de jobs lentos.");
    return false;
  }

  if (gControlWorkerTaskHandle != nullptr) {
    return true;
  }

  BaseType_t result = xTaskCreatePinnedToCore(
      controlWorkerTask,
      "control_worker",
      kControlWorkerTaskStackSize,
      nullptr,
      kControlWorkerTaskPriority,
      &gControlWorkerTaskHandle,
      0);

  if (result != pdPASS) {
    gControlWorkerTaskHandle = nullptr;
    Serial.println("[control] falha ao criar control worker.");
    return false;
  }

  Serial.printf(
      "[control] worker iniciado no core 0 stack=%u bytes\n",
      static_cast<unsigned>(kControlWorkerTaskStackSize));
  return true;
}

static bool deferPanelsBatchCommandIfNeeded(const char* command) {
  return command != nullptr
      && strcmp(command, "queue_panels_batch") == 0
      && gActiveSlowCommand == SlowCommandKind::QueuePanelsBatch
      && !gProvisioningLaunchPending;
}

static bool schedulePanelsBatchDownload(
    const String& commandId,
    const PanelsBatchDownloadRequest& panelsRequest,
    const char* command) {
  if (isSlowCommandDomainBusy()) {
    if (deferPanelsBatchCommandIfNeeded(command)) {
      return false;
    }

    const String busyMessage = String("Outro job lento em andamento: ")
        + currentSlowCommandBusyName()
        + ".";
    (void)publishDeviceLog("warning", "command", "slow_job_busy", busyMessage);
    sendCommandProgress(commandId, 100, "failed", busyMessage, 0);
    return true;
  }

  if (!ensureControlWorkerStarted()) {
    (void)publishDeviceLog("warning", "command", "control_worker_unavailable", "Falha ao iniciar o worker de controle.");
    sendCommandProgress(commandId, 100, "failed", "Falha ao iniciar o worker de controle.", 0);
    return true;
  }

  if (!tryBeginSlowCommand(SlowCommandKind::QueuePanelsBatch)) {
    sendCommandProgress(commandId, 100, "failed", "Nao foi possivel reservar o worker de controle.", 0);
    return true;
  }

  SlowCommandRequest* request = new SlowCommandRequest();
  request->kind = SlowCommandKind::QueuePanelsBatch;
  request->commandId = commandId;
  request->panels = panelsRequest;
  if (!enqueueSlowCommandRequest(request)) {
    deleteSlowCommandRequest(request);
    completeSlowCommand(SlowCommandKind::QueuePanelsBatch);
    sendCommandProgress(commandId, 100, "failed", "Fila de jobs lentos indisponivel.", 0);
    return true;
  }

  sendCommandProgress(commandId, 25, "queued", "Lote WebP recebido; aguardando worker de controle.");
  return true;
}

static bool scheduleFirmwareUpdate(const String& commandId, const String& requestedVersion) {
  if (isRunningAppPendingVerify()) {
    const String errorMessage =
        "A imagem atual ainda nao foi validada pelo safe update mode; conclua ou aguarde o primeiro boot antes de nova OTA.";
    (void)publishDeviceLog("warning", "command", "ota_invalid_state_pending_verify", errorMessage);
    sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
    return true;
  }

  if (isSlowCommandDomainBusy()) {
    const String busyMessage = String("Outro job lento em andamento: ")
        + currentSlowCommandBusyName()
        + ".";
    (void)publishDeviceLog("warning", "command", "slow_job_busy", busyMessage);
    sendCommandProgress(commandId, 100, "failed", busyMessage, 0);
    return true;
  }

  if (!ensureControlWorkerStarted()) {
    (void)publishDeviceLog("warning", "command", "control_worker_unavailable", "Falha ao iniciar o worker de controle.");
    sendCommandProgress(commandId, 100, "failed", "Falha ao iniciar o worker de controle.", 0);
    return true;
  }

  if (!tryBeginSlowCommand(SlowCommandKind::UpdateFirmware)) {
    sendCommandProgress(commandId, 100, "failed", "Nao foi possivel reservar o worker de controle.", 0);
    return true;
  }

  SlowCommandRequest* request = new SlowCommandRequest();
  request->kind = SlowCommandKind::UpdateFirmware;
  request->commandId = commandId;
  request->requestedVersion = requestedVersion;
  if (!enqueueSlowCommandRequest(request)) {
    deleteSlowCommandRequest(request);
    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    sendCommandProgress(commandId, 100, "failed", "Fila de jobs lentos indisponivel.", 0);
    return true;
  }

  sendCommandProgress(commandId, 25, "queued", "Atualizacao OTA encaminhada ao worker de controle.");
  return true;
}

static bool isRecognizedControlCommand(const char* command) {
  return command != nullptr
      && (strcmp(command, "enter_provisioning") == 0
          || strcmp(command, "revoke_and_restart") == 0
          || strcmp(command, "test_led") == 0
          || strcmp(command, "set_brightness") == 0
          || strcmp(command, "update_firmware") == 0
          || strcmp(command, "install_app") == 0
          || strcmp(command, "activate_app") == 0
          || strcmp(command, "set_app_config") == 0
          || strcmp(command, "queue_panels_batch") == 0);
}

static void rejectSessionCommand(const String& commandId, const char* errorCode, const String& message) {
  (void)publishDeviceLog("warning", "session", errorCode, message, false);
  if (commandId.length() > 0) {
    sendCommandProgress(commandId, 100, errorCode, message, 0);
  }
}

static ControlCommandHandleResult handleControlCommandMessageCore(const JsonDocument& control) {
  const char* command = control["command"] | "";
  String commandId = control["commandId"] | "";
  JsonObjectConst parameters = control["parameters"].as<JsonObjectConst>();

  if (command != nullptr && command[0] != '\0') {
    (void)publishDeviceLog("info", "command", command, String("Comando recebido via controle: ") + command);
  }

  if (strcmp(command, "enter_provisioning") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (!requestProvisioningPortalLaunch(commandId, true, "command_enter_provisioning", false)) {
      sendCommandProgress(commandId, 100, "failed", "Outro job lento impede a entrada imediata em provisioning.", 0);
      return ControlCommandHandleResult::Handled;
    }

    sendCommandProgress(commandId, 35, "scheduled", "Provisioning reservado; preparando troca de modo.");
    return ControlCommandHandleResult::Handled;
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
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "test_led") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");

    JsonVariantConst enabledValue = parameters["enabled"];
    if (!enabledValue.isNull()) {
      bool enabled = false;
      if (!tryParseBooleanParameter(enabledValue, enabled)) {
        (void)publishDeviceLog("warning", "command", "test_led_invalid_enabled", "Parametro enabled invalido para test_led.");
        sendCommandProgress(commandId, 100, "invalid", "Parametro enabled invalido.", 0);
        return ControlCommandHandleResult::Handled;
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
      return ControlCommandHandleResult::Handled;
    }

    if (!isTestLedAvailable()) {
      (void)publishDeviceLog("warning", "command", "test_led_unavailable", "Nenhum LED de teste disponivel neste hardware.");
      sendCommandProgress(commandId, 100, "test-led-unavailable", "Nenhum LED de teste disponivel neste hardware.", 0);
      return ControlCommandHandleResult::Handled;
    }

    triggerTestLed();
    sendCommandProgress(commandId, 100, "test-led", "Teste de LED acionado.", 1);
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "set_brightness") == 0) {
    String brightnessRaw = parameters["brightness"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (brightnessRaw.length() == 0) {
      (void)publishDeviceLog("warning", "command", "set_brightness_missing", "Parametro brightness ausente.");
      sendCommandProgress(commandId, 100, "invalid", "brightness ausente.", 0);
      return ControlCommandHandleResult::Handled;
    }

    const int brightnessValue = brightnessRaw.toInt();
    if (brightnessValue == 0 && brightnessRaw != "0") {
      (void)publishDeviceLog("warning", "command", "set_brightness_invalid", "Parametro brightness invalido.");
      sendCommandProgress(commandId, 100, "invalid", "brightness invalido.", 0);
      return ControlCommandHandleResult::Handled;
    }

    gBrightnessCap = clampBrightnessToSafeRange(brightnessValue);
    gPrefs.putUChar("brightnessCap", gBrightnessCap);
    setMatrixBrightness(resolveAppliedBrightness());
    updateTestLedDutyFromBrightness(gAppliedBrightness);
    applyTestLedState();
    sendTelemetry(true);

    sendCommandProgress(commandId, 100, "set-brightness", "Brilho atualizado.", 1);
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "update_firmware") == 0) {
    const String requestedVersion = parameters["version"] | "";
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    (void)scheduleFirmwareUpdate(commandId, requestedVersion);
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "install_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      (void)publishDeviceLog("warning", "command", "install_app_missing_appid", "appId ausente para install_app.");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return ControlCommandHandleResult::Handled;
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
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "activate_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      (void)publishDeviceLog("warning", "command", "activate_app_missing_appid", "appId ausente para activate_app.");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return ControlCommandHandleResult::Handled;
    }

    gActiveAppId = appId;
    gActiveAppName = appName.length() > 0 ? appName : appId;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppName", gActiveAppName);
    sendTelemetry(true);

    sendCommandProgress(commandId, 100, "activate-app", "App ativado.", 1);
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "set_app_config") == 0) {
    String appId = parameters["appId"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      (void)publishDeviceLog("warning", "command", "set_app_config_missing_appid", "appId ausente para set_app_config.");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return ControlCommandHandleResult::Handled;
    }

    gActiveAppId = appId;
    gActiveAppConfig = configJson;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppConfig", gActiveAppConfig);
    sendTelemetry(true);

    sendCommandProgress(commandId, 100, "set-app-config", "Configuracao aplicada.", 1);
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "queue_panels_batch") == 0) {
    PanelsBatchDownloadRequest request = {};
    request.panelsSessionId = parameters["panelsSessionId"] | "";
    request.downloadUrl = parameters["downloadUrl"] | "";
    request.expectedSha256 = normalizeLowerHex(parameters["sha256"] | "");
    request.expectedContentType = parameters["contentType"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");

    if (!gAnimatedWebpBatchSupported) {
      sendCommandProgress(commandId, 100, "unsupported", "Firmware sem suporte ao transporte WebP batch.", 0);
      return ControlCommandHandleResult::Handled;
    }

    const bool parsedBatchSequence = tryParseUnsignedLongParameter(parameters["batchSequence"], request.batchSequence);
    const bool parsedFileSize = tryParseUnsignedLongParameter(parameters["fileSizeBytes"], request.fileSizeBytes);
    const bool parsedFrameCount = tryParseUnsignedLongParameter(parameters["frameCount"], request.frameCount);
    const bool parsedDuration = tryParseUnsignedLongParameter(parameters["durationMs"], request.durationMs);
    const bool outOfRange = request.frameCount > UINT16_MAX || request.durationMs > UINT16_MAX;
    if (request.panelsSessionId.length() == 0
        || request.downloadUrl.length() == 0
        || request.expectedSha256.length() != 64
        || !parsedBatchSequence
        || !parsedFileSize
        || !parsedFrameCount
        || !parsedDuration
        || request.fileSizeBytes == 0u
        || request.frameCount == 0u
        || request.durationMs == 0u
        || outOfRange) {
      (void)publishDeviceLog("warning", "command", "queue_panels_batch_invalid", "Parametros invalidos para queue_panels_batch.");
      sendCommandProgress(commandId, 100, "invalid", "Parametros invalidos para queue_panels_batch.", 0);
      return ControlCommandHandleResult::Handled;
    }

    if (!request.expectedContentType.equalsIgnoreCase(kPanelsBatchExpectedContentType)) {
      (void)publishDeviceLog("warning", "command", "queue_panels_batch_content_type_invalid", "contentType invalido para queue_panels_batch.");
      sendCommandProgress(commandId, 100, "invalid", "contentType invalido para queue_panels_batch.", 0);
      return ControlCommandHandleResult::Handled;
    }

    if (!schedulePanelsBatchDownload(commandId, request, command)) {
      return ControlCommandHandleResult::Deferred;
    }

    return ControlCommandHandleResult::Handled;
  }

  (void)publishDeviceLog("warning", "command", "unknown_command", String("Comando desconhecido: ") + command);
  sendCommandProgress(commandId, 100, "unknown", "Comando desconhecido.", 0);
  return ControlCommandHandleResult::Handled;
}

static ControlCommandHandleResult handleSessionAwareControlCommand(
    const ControlCommandEnvelope& envelope,
    const JsonDocument& control) {
  const char* command = control["command"] | "";
  const String commandId = control["commandId"] | "";
  const JsonObjectConst parameters = control["parameters"].as<JsonObjectConst>();

  if (!isRecognizedControlCommand(command) && !isSessionCommandName(command)) {
    return handleControlCommandMessageCore(control);
  }

  const unsigned long nowMs = millis();
  expireSessionLeases(nowMs);
  const bool ownerActive = hasActiveClientOwner(nowMs);
  const bool sameOwnerClient = ownerActive && gSessionShadowState.activeClientId.equals(envelope.clientId);
  const bool sessionAware = isSessionAwareEnvelope(envelope);
  if (!sessionAware) {
    return handleControlCommandMessageCore(control);
  }

  if (strcmp(command, "session_heartbeat") == 0) {
    ClientSessionMode modeHint = gSessionShadowState.mode;
    String modeRaw = parameters["mode"] | "";
    modeRaw.trim();
    modeRaw.toLowerCase();
    if (modeRaw == "visualizer") {
      modeHint = ClientSessionMode::Visualizer;
    } else if (modeRaw == "panels") {
      modeHint = ClientSessionMode::Panels;
    } else if (modeRaw == "idle") {
      modeHint = ClientSessionMode::Idle;
    }

    if (!hasActiveClientOwner(nowMs)) {
      adoptActiveClientOwner(envelope.clientId, nowMs);
    } else if (!isActiveClientOwner(envelope.clientId, envelope.ownerEpoch, nowMs)
               && !(sameOwnerClient && envelope.ownerEpoch == 0u)) {
      rejectSessionCommand(commandId, "stale_owner_epoch", "Heartbeat ignorado para owner antigo.");
      return ControlCommandHandleResult::Handled;
    } else {
      renewActiveClientOwner(nowMs);
    }

    setClientSessionMode(modeHint, nowMs);
    if (envelope.lockToken.length() > 0) {
      (void)tryRenewClientLock(envelope.clientId, envelope.lockToken, nowMs);
    }
    (void)publishClientSessionShadow(nowMs, false);
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "session_lock_acquire") == 0) {
    String failureCode;
    String failureMessage;
    const String reason = parameters["reason"] | "";
    const uint32_t requestedLeaseMs = parameters["leaseMs"] | 0u;

    if (!hasActiveClientOwner(nowMs)
        || (!isActiveClientOwner(envelope.clientId, envelope.ownerEpoch, nowMs)
            && !(sameOwnerClient && envelope.ownerEpoch == 0u))) {
      adoptActiveClientOwner(envelope.clientId, nowMs);
    } else {
      renewActiveClientOwner(nowMs);
    }

    if (!tryAcquireClientLock(
            envelope.clientId,
            envelope.lockToken,
            reason,
            requestedLeaseMs,
            nowMs,
            failureCode,
            failureMessage)) {
      rejectSessionCommand(commandId, failureCode.c_str(), failureMessage);
      return ControlCommandHandleResult::Handled;
    }

    if (commandId.length() > 0) {
      sendCommandProgress(commandId, 100, "lock-acquired", "Lock de sessao adquirido.", 1);
    }
    (void)publishClientSessionShadow(nowMs, false);
    return ControlCommandHandleResult::Handled;
  }

  if (strcmp(command, "session_lock_release") == 0) {
    String failureCode;
    String failureMessage;
    if (!tryReleaseClientLock(envelope.clientId, envelope.lockToken, nowMs, failureCode, failureMessage)) {
      rejectSessionCommand(commandId, failureCode.c_str(), failureMessage);
      return ControlCommandHandleResult::Handled;
    }

    if (commandId.length() > 0) {
      sendCommandProgress(commandId, 100, "lock-released", "Lock de sessao liberado.", 1);
    }
    (void)publishClientSessionShadow(nowMs, false);
    return ControlCommandHandleResult::Handled;
  }

  if (isClientLockHeldByAnotherClient(envelope.clientId, nowMs)) {
    rejectSessionCommand(commandId, "locked_by_other_client", "Outro cliente detem o lock ativo.");
    return ControlCommandHandleResult::Handled;
  }

  if (doesCommandRequireSessionLock(command) && !isClientLockHeldByClient(envelope.clientId, envelope.lockToken, nowMs)) {
    rejectSessionCommand(commandId, "lock_required", "Este comando exige lock ativo do cliente.");
    return ControlCommandHandleResult::Handled;
  }

  if (!hasActiveClientOwner(nowMs)) {
    adoptActiveClientOwner(envelope.clientId, nowMs);
  } else if (!isActiveClientOwner(envelope.clientId, envelope.ownerEpoch, nowMs)
             && !(sameOwnerClient && envelope.ownerEpoch == 0u)) {
    adoptActiveClientOwner(envelope.clientId, nowMs);
  } else {
    renewActiveClientOwner(nowMs);
  }

  const ControlCommandHandleResult result = handleControlCommandMessageCore(control);
  if (result != ControlCommandHandleResult::Deferred) {
    setClientSessionMode(resolveSessionModeHint(command, parameters), nowMs);
    (void)publishClientSessionShadow(nowMs, false);
  }

  return result;
}

static void processPanelsBatchSlowCommand(const SlowCommandRequest& request) {
  PanelsBatchBuffer batch = {};
  batch.panelsSessionId = request.panels.panelsSessionId;
  batch.batchSequence = request.panels.batchSequence;
  batch.frameCount = static_cast<uint16_t>(request.panels.frameCount);
  batch.durationMs = static_cast<uint16_t>(request.panels.durationMs);

  String errorCode;
  String errorMessage;

  setControlWorkerState(ControlWorkerState::PanelsDownloading);
  (void)enqueueAsyncCommandProgress(request.commandId, 35, "downloading", "Baixando lote WebP...");
  if (!tryDownloadPanelsBatch(
          request.panels.downloadUrl,
          request.panels.expectedSha256,
          request.panels.fileSizeBytes,
          request.panels.expectedContentType,
          batch,
          errorCode,
          errorMessage)) {
    clearPanelsBatchBuffer(batch);
    (void)enqueueAsyncDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
    (void)enqueueAsyncCommandProgress(request.commandId, 100, "failed", errorMessage, 0);
    setControlWorkerState(ControlWorkerState::Failed);
    completeSlowCommand(SlowCommandKind::QueuePanelsBatch);
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  setControlWorkerState(ControlWorkerState::PanelsValidating);
  (void)enqueueAsyncCommandProgress(request.commandId, 70, "validating", "Validando lote WebP...");
  if (!validatePanelsBatchWebp(batch, errorCode, errorMessage)) {
    clearPanelsBatchBuffer(batch);
    (void)enqueueAsyncDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
    (void)enqueueAsyncCommandProgress(request.commandId, 100, "failed", errorMessage, 0);
    setControlWorkerState(ControlWorkerState::Failed);
    completeSlowCommand(SlowCommandKind::QueuePanelsBatch);
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  if (!tryQueuePanelsBatchForPlayback(batch, errorCode, errorMessage)) {
    clearPanelsBatchBuffer(batch);
    (void)enqueueAsyncDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
    (void)enqueueAsyncCommandProgress(request.commandId, 100, "failed", errorMessage, 0);
    setControlWorkerState(ControlWorkerState::Failed);
    completeSlowCommand(SlowCommandKind::QueuePanelsBatch);
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  (void)enqueueAsyncCommandProgress(request.commandId, 100, "queued", "Lote WebP de Paineis enfileirado.", 1);
  completeSlowCommand(SlowCommandKind::QueuePanelsBatch);
  setControlWorkerState(ControlWorkerState::Idle);
}

static void processFirmwareSlowCommand(const SlowCommandRequest& request) {
  FirmwareReleaseInfo releaseInfo;
  String errorCode;
  String errorMessage;

  setControlWorkerState(ControlWorkerState::FetchingFirmware);
  if (!tryFetchLatestFirmwareRelease(releaseInfo, request.requestedVersion, errorCode, errorMessage)) {
    (void)enqueueAsyncDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
    (void)enqueueAsyncCommandProgress(request.commandId, 100, "failed", errorMessage, 0);
    setControlWorkerState(ControlWorkerState::Failed);
    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  if (gOtaDownloadTaskHandle != nullptr || gOtaTaskResult != OtaTaskResult::Idle) {
    (void)enqueueAsyncCommandProgress(request.commandId, 100, "failed", "OTA ja em andamento.", 0);
    setControlWorkerState(ControlWorkerState::Failed);
    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  OtaTaskParams* otaParams = new OtaTaskParams{
    releaseInfo.downloadPath,
    releaseInfo.sha256,
    releaseInfo.fileSizeBytes,
    request.commandId,
    String(kFirmwareVersion),
    releaseInfo.firmwareVersion,
  };

  if (!enqueueAsyncOtaStart(request.commandId, releaseInfo.firmwareVersion, otaParams)) {
    delete otaParams;
    (void)enqueueAsyncCommandProgress(request.commandId, 100, "failed", "Nao foi possivel iniciar a OTA no loop principal.", 0);
    setControlWorkerState(ControlWorkerState::Failed);
    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  setControlWorkerState(ControlWorkerState::AwaitingOtaResult);
}

static bool handleAsyncOtaStartEvent(AsyncControlEvent& event) {
  if (event.otaParams == nullptr) {
    sendCommandProgress(event.commandId, 100, "failed", "Evento OTA sem parametros.", 0);
    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    return false;
  }

  if (gOtaDownloadTaskHandle != nullptr || gOtaTaskResult != OtaTaskResult::Idle) {
    sendCommandProgress(event.commandId, 100, "failed", "OTA ja em andamento.", 0);
    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    return false;
  }

  sendCommandProgress(event.commandId, 35, "metadata", "Firmware oficial validado.");
  if (gWs.isConnected()) {
    gWs.disconnect();
  }

  gOtaInProgress = true;
  gOtaProgressPercent = 20;
  gOtaProgressStage = "recebido";
  gHub75FallbackDirty = true;
  gOtaBridgeCommandId = event.commandId;
  gOtaBridgeTargetVersion = event.otaTargetVersion;
  gOtaBridgeLastPercent = 35;
  gOtaTaskResult = OtaTaskResult::Idle;

  OtaTaskParams* otaParams = event.otaParams;
  BaseType_t rc = xTaskCreatePinnedToCore(
      otaDownloadTaskFn,
      "ota_download",
      kOtaDownloadTaskStackSize,
      otaParams,
      kOtaDownloadTaskPriority,
      &gOtaDownloadTaskHandle,
      0);

  if (rc != pdPASS) {
    delete otaParams;
    event.otaParams = nullptr;
    gOtaInProgress = false;
    gOtaDownloadTaskHandle = nullptr;
    sendCommandProgress(event.commandId, 100, "failed", "Falha ao criar task OTA.", 0);
    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    return false;
  }

  event.otaParams = nullptr;
  setControlWorkerState(ControlWorkerState::AwaitingOtaResult);
  return true;
}

}  // namespace

void initializeControlCommandRuntime() {
  initializeTaskWatchdogRuntime();
  initializeClientSessionRuntime();

  if (gControlCommandQueue == nullptr) {
    gControlCommandQueue = xQueueCreate(kControlCommandQueueDepth, sizeof(ControlCommandEnvelope*));
  }

  if (gAsyncControlEventQueue == nullptr) {
    gAsyncControlEventQueue = xQueueCreate(kAsyncControlEventQueueDepth, sizeof(AsyncControlEvent*));
  }
}

bool enqueueIncomingControlCommand(ControlCommandSource source, const uint8_t* payload, size_t length) {
  if (payload == nullptr || length == 0 || gControlCommandQueue == nullptr) {
    return false;
  }

  JsonDocument control;
  if (deserializeJson(control, payload, length) != DeserializationError::Ok) {
    return false;
  }

  const String command = control["command"] | "";
  if (command.length() == 0) {
    return false;
  }

  ControlCommandEnvelope* envelope = new ControlCommandEnvelope();
  envelope->source = source;
  envelope->command = command;
  envelope->commandId = control["commandId"] | "";
  envelope->clientId = control["clientId"] | "";
  envelope->ownerEpoch = control["ownerEpoch"] | 0u;
  envelope->lockToken = control["lockToken"] | "";
  envelope->payloadJson = copyPayloadToString(payload, length);

  ControlCommandEnvelope* queuedEnvelope = envelope;
  if (xQueueSend(gControlCommandQueue, &queuedEnvelope, 0) != pdTRUE) {
    Serial.printf("[control] fila de ingress cheia; comando descartado: %s\n", command.c_str());
    deleteControlCommandEnvelope(envelope);
    return false;
  }

  return true;
}

void processQueuedControlCommands() {
  expireSessionLeases(millis());

  for (uint8_t processed = 0; processed < kMaxControlCommandsPerLoop; processed++) {
    ControlCommandEnvelope* envelope = nullptr;
    if (gDeferredControlCommand != nullptr) {
      if (isSlowCommandDomainBusy()) {
        // Deferred command still blocked by slow domain; try queue items instead.
        if (gControlCommandQueue == nullptr || xQueueReceive(gControlCommandQueue, &envelope, 0) != pdTRUE) {
          break;
        }
      } else {
        envelope = gDeferredControlCommand;
        gDeferredControlCommand = nullptr;
      }
    } else {
      if (gControlCommandQueue == nullptr || xQueueReceive(gControlCommandQueue, &envelope, 0) != pdTRUE) {
        break;
      }
    }

    if (envelope == nullptr) {
      continue;
    }

    JsonDocument control;
    if (deserializeJson(control, envelope->payloadJson) != DeserializationError::Ok) {
      if (envelope->commandId.length() > 0) {
        sendCommandProgress(envelope->commandId, 100, "invalid", "Payload JSON invalido.", 0);
      }

      deleteControlCommandEnvelope(envelope);
      continue;
    }

    const ControlCommandHandleResult result = handleSessionAwareControlCommand(*envelope, control);
    if (result == ControlCommandHandleResult::Deferred) {
      if (gDeferredControlCommand != nullptr) {
        // Already holding a deferred command; discard the new one to avoid losing it.
        deleteControlCommandEnvelope(envelope);
      } else {
        gDeferredControlCommand = envelope;
      }
      break;
    }

    deleteControlCommandEnvelope(envelope);
  }
}

void processAsyncControlEvents() {
  for (uint8_t processed = 0; processed < kMaxAsyncEventsPerLoop; processed++) {
    AsyncControlEvent* event = nullptr;
    if (gAsyncControlEventQueue == nullptr || xQueueReceive(gAsyncControlEventQueue, &event, 0) != pdTRUE) {
      break;
    }

    if (event == nullptr) {
      continue;
    }

    switch (event->kind) {
      case AsyncControlEventKind::CommandProgress:
        sendCommandProgress(
            event->commandId,
            event->progressPercent,
            event->stage.c_str(),
            event->message,
            event->successFlag);
        break;

      case AsyncControlEventKind::DeviceLog:
        (void)publishDeviceLog(
            event->logLevel.c_str(),
            event->logCategory.c_str(),
            event->logEventCode.c_str(),
            event->message,
            event->includeTelemetrySequence);
        break;

      case AsyncControlEventKind::StartOta:
        (void)handleAsyncOtaStartEvent(*event);
        break;

      default:
        break;
    }

    deleteAsyncControlEvent(event);
  }
}

bool requestProvisioningPortalLaunch(const String& commandId, bool clearDeviceCredentials, const char* reason, bool allowDeferred) {
  if (gProvisioningLaunchPending) {
    return false;
  }

  if (gActiveSlowCommand != SlowCommandKind::None && !allowDeferred) {
    return false;
  }

  gProvisioningLaunchPending = true;
  gProvisioningLaunchClearCredentials = clearDeviceCredentials;
  gProvisioningLaunchCommandId = commandId;
  gProvisioningLaunchReason = reason == nullptr ? "" : reason;
  if (gActiveSlowCommand == SlowCommandKind::None) {
    setControlWorkerState(ControlWorkerState::ProvisioningPending);
  }
  return true;
}

bool isProvisioningPortalLaunchPending() {
  return gProvisioningLaunchPending;
}

void processProvisioningLaunchRequest() {
  if (!gProvisioningLaunchPending || gActiveSlowCommand != SlowCommandKind::None) {
    return;
  }

  if (gProvisioningLaunchCommandId.length() == 0 && WiFi.status() == WL_CONNECTED) {
    clearProvisioningLaunchRequest();
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  const String commandId = gProvisioningLaunchCommandId;
  const String reason = gProvisioningLaunchReason.length() > 0 ? gProvisioningLaunchReason : "command_enter_provisioning";
  const bool clearDeviceCredentials = gProvisioningLaunchClearCredentials;
  clearProvisioningLaunchRequest();

  setControlWorkerState(ControlWorkerState::ProvisioningPending);
  if (!tryBeginSlowCommand(SlowCommandKind::EnterProvisioning)) {
    setControlWorkerState(ControlWorkerState::Idle);
    return;
  }

  if (commandId.length() > 0) {
    sendCommandProgress(commandId, 100, "enter-provisioning", "Entrando em provisioning.", 1);
  }

  if (gLoopTaskWatchdogSubscribed) {
    unsubscribeCurrentTaskFromWatchdog();
    gLoopTaskWatchdogSubscribed = false;
  }

  if (clearDeviceCredentials) {
    gPrefs.remove("deviceId");
    gPrefs.remove("token");
    gDeviceId = "";
    gToken = "";
  }

  const bool provisioningStarted = startProvisioningPortal(reason.c_str());

  if (gTaskWatchdogReady) {
    subscribeCurrentTaskToWatchdog();
    gLoopTaskWatchdogSubscribed = true;
  }

  completeSlowCommand(SlowCommandKind::EnterProvisioning);
  setControlWorkerState(ControlWorkerState::Idle);

  if (!provisioningStarted && commandId.length() > 0 && gMqtt.connected()) {
    sendCommandProgress(commandId, 100, "failed", "Falha ao abrir o portal de provisioning.", 0);
  }
}

bool enqueueAsyncCommandProgress(
    const String& commandId,
    uint8_t progressPercent,
    const char* stage,
    const String& message,
    int successFlag) {
  AsyncControlEvent* event = new AsyncControlEvent();
  event->kind = AsyncControlEventKind::CommandProgress;
  event->commandId = commandId;
  event->progressPercent = progressPercent;
  event->stage = stage == nullptr ? "" : stage;
  event->message = message;
  event->successFlag = successFlag;
  if (!enqueueAsyncControlEvent(event, pdMS_TO_TICKS(20))) {
    deleteAsyncControlEvent(event);
    return false;
  }

  return true;
}

bool enqueueAsyncDeviceLog(
    const char* level,
    const char* category,
    const char* eventCode,
    const String& message,
    bool includeTelemetrySequence) {
  AsyncControlEvent* event = new AsyncControlEvent();
  event->kind = AsyncControlEventKind::DeviceLog;
  event->logLevel = level == nullptr ? "info" : level;
  event->logCategory = category == nullptr ? "device" : category;
  event->logEventCode = eventCode == nullptr ? "" : eventCode;
  event->message = message;
  event->includeTelemetrySequence = includeTelemetrySequence;
  if (!enqueueAsyncControlEvent(event, pdMS_TO_TICKS(20))) {
    deleteAsyncControlEvent(event);
    return false;
  }

  return true;
}

bool enqueueAsyncOtaStart(const String& commandId, const String& targetVersion, OtaTaskParams* otaParams) {
  AsyncControlEvent* event = new AsyncControlEvent();
  event->kind = AsyncControlEventKind::StartOta;
  event->commandId = commandId;
  event->otaTargetVersion = targetVersion;
  event->otaParams = otaParams;
  if (!enqueueAsyncControlEvent(event, pdMS_TO_TICKS(20))) {
    deleteAsyncControlEvent(event);
    return false;
  }

  return true;
}

void controlWorkerTask(void* parameter) {
  (void)parameter;

  while (true) {
    SlowCommandRequest* request = nullptr;
    if (gSlowCommandQueue == nullptr || xQueueReceive(gSlowCommandQueue, &request, portMAX_DELAY) != pdTRUE) {
      continue;
    }

    if (request == nullptr) {
      continue;
    }

    subscribeCurrentTaskToWatchdog();
    resetTaskWatchdog();
    switch (request->kind) {
      case SlowCommandKind::QueuePanelsBatch:
        processPanelsBatchSlowCommand(*request);
        break;

      case SlowCommandKind::UpdateFirmware:
        processFirmwareSlowCommand(*request);
        break;

      case SlowCommandKind::EnterProvisioning:
      case SlowCommandKind::None:
      default:
        completeSlowCommand(request->kind);
        setControlWorkerState(ControlWorkerState::Idle);
        break;
    }

    resetTaskWatchdog();
    unsubscribeCurrentTaskFromWatchdog();
    deleteSlowCommandRequest(request);
  }
}

void setControlWorkerState(ControlWorkerState state) {
  gControlWorkerState = state;
}

void setPanelsWorkerState(PanelsWorkerState state) {
  gPanelsWorkerState = state;
}

void completeSlowCommand(SlowCommandKind kind) {
  if (kind == SlowCommandKind::None) {
    return;
  }

  gLastSlowCommand = kind;
  if (gActiveSlowCommand == kind) {
    gLastSlowCommandDurationMs = gActiveSlowCommandStartedMs == 0
        ? 0
        : static_cast<uint32_t>(millis() - gActiveSlowCommandStartedMs);
    gActiveSlowCommand = SlowCommandKind::None;
    gActiveSlowCommandStartedMs = 0;
  }
}

void resetTaskWatchdog() {
  if (!gTaskWatchdogReady) {
    return;
  }

  (void)esp_task_wdt_reset();
}

void subscribeCurrentTaskToWatchdog() {
  if (!gTaskWatchdogReady) {
    return;
  }

  (void)esp_task_wdt_add(nullptr);
}

void unsubscribeCurrentTaskFromWatchdog() {
  if (!gTaskWatchdogReady) {
    return;
  }

  (void)esp_task_wdt_delete(nullptr);
}

bool isTaskWatchdogReady() {
  return gTaskWatchdogReady;
}
