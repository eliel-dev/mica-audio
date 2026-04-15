#include "mica_commands.h"

#include "mica_globals.h"
#include "mica_display.h"
#include "mica_network.h"
#include "mica_ota.h"
#include "mica_panels.h"

#include "mica_provisioning.h"

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

    if (gOtaDownloadTaskHandle != nullptr || gOtaTaskResult != OtaTaskResult::Idle) {
      sendCommandProgress(commandId, 100, "failed", "OTA ja em andamento.", 0);
      return;
    }

    sendCommandProgress(commandId, 35, "metadata", "Firmware oficial validado.");
    if (gWs.isConnected()) {
      gWs.disconnect();
    }

    gOtaInProgress = true;
    gOtaProgressPercent = 20;
    gOtaProgressStage = "recebido";
    gHub75FallbackDirty = true;

    gOtaBridgeCommandId = commandId;
    gOtaBridgeTargetVersion = releaseInfo.firmwareVersion;
    gOtaBridgeLastPercent = 35;
    gOtaTaskResult = OtaTaskResult::Idle;

    OtaTaskParams* params = new OtaTaskParams{
      releaseInfo.downloadPath, releaseInfo.sha256, releaseInfo.fileSizeBytes,
      commandId, String(kFirmwareVersion), releaseInfo.firmwareVersion
    };

    BaseType_t rc = xTaskCreatePinnedToCore(
        otaDownloadTaskFn, "ota_download",
        kOtaDownloadTaskStackSize, params,
        kOtaDownloadTaskPriority, &gOtaDownloadTaskHandle, 0);

    if (rc != pdPASS) {
      delete params;
      gOtaInProgress = false;
      gOtaDownloadTaskHandle = nullptr;
      sendCommandProgress(commandId, 100, "failed", "Falha ao criar task OTA.", 0);
    }
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

  if (strcmp(command, "queue_panels_batch") == 0) {
    String panelsSessionId = parameters["panelsSessionId"] | "";
    String downloadUrl = parameters["downloadUrl"] | "";
    String expectedSha256 = normalizeLowerHex(parameters["sha256"] | "");
    String expectedContentType = parameters["contentType"] | "";
    uint32_t batchSequence = 0u;
    uint32_t fileSizeBytes = 0u;
    uint32_t frameCount = 0u;
    uint32_t durationMs = 0u;

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");

    if (!gAnimatedWebpBatchSupported) {
      sendCommandProgress(commandId, 100, "unsupported", "Firmware sem suporte ao transporte WebP batch.", 0);
      return;
    }

    if (panelsSessionId.length() == 0
        || downloadUrl.length() == 0
        || expectedSha256.length() != 64
        || !tryParseUnsignedLongParameter(parameters["batchSequence"], batchSequence)
        || !tryParseUnsignedLongParameter(parameters["fileSizeBytes"], fileSizeBytes)
        || !tryParseUnsignedLongParameter(parameters["frameCount"], frameCount)
        || !tryParseUnsignedLongParameter(parameters["durationMs"], durationMs)) {
      (void)publishDeviceLog("warning", "command", "queue_panels_batch_invalid", "Parametros invalidos para queue_panels_batch.");
      sendCommandProgress(commandId, 100, "invalid", "Parametros invalidos para queue_panels_batch.", 0);
      return;
    }

    if (!expectedContentType.equalsIgnoreCase(kPanelsBatchExpectedContentType)) {
      (void)publishDeviceLog("warning", "command", "queue_panels_batch_content_type_invalid", "contentType invalido para queue_panels_batch.");
      sendCommandProgress(commandId, 100, "invalid", "contentType invalido para queue_panels_batch.", 0);
      return;
    }

    sendCommandProgress(commandId, 35, "downloading", "Baixando lote WebP...");

    PanelsBatchBuffer batch = {};
    batch.panelsSessionId = panelsSessionId;
    batch.batchSequence = batchSequence;
    batch.frameCount = static_cast<uint16_t>(frameCount);
    batch.durationMs = static_cast<uint16_t>(durationMs);

    String errorCode;
    String errorMessage;
    if (!tryDownloadPanelsBatch(downloadUrl, expectedSha256, fileSizeBytes, expectedContentType, batch, errorCode, errorMessage)) {
      clearPanelsBatchBuffer(batch);
      (void)publishDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
      sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
      return;
    }

    sendCommandProgress(commandId, 70, "validating", "Validando lote WebP...");
    if (!validatePanelsBatchWebp(batch, errorCode, errorMessage)) {
      clearPanelsBatchBuffer(batch);
      (void)publishDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
      sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
      return;
    }

    if (!tryQueuePanelsBatchForPlayback(batch, errorCode, errorMessage)) {
      clearPanelsBatchBuffer(batch);
      (void)publishDeviceLog("warning", "command", errorCode.c_str(), errorMessage);
      sendCommandProgress(commandId, 100, "failed", errorMessage, 0);
      return;
    }

    sendCommandProgress(commandId, 100, "queued", "Lote WebP de Paineis enfileirado.", 1);
    return;
  }

  (void)publishDeviceLog("warning", "command", "unknown_command", String("Comando desconhecido: ") + command);
  sendCommandProgress(commandId, 100, "unknown", "Comando desconhecido.", 0);
}
