// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---ap-first-com-hub75-adiado-no-boot-limpo
// DOCS: docs/handoffs/2026-04-14-ota-firmware-update-flow-e-hub75-status.md
// DOCS: docs/handoffs/2026-04-14-freertos-ota-background-task.md
// DOCS: docs/handoffs/2026-04-16-ap-first-wifi-mem-and-copy-logs.md

#include "mica_ota.h"

#include <Update.h>
#include <esp_err.h>
#include <mbedtls/sha256.h>
#include <HTTPClient.h>

#include "mica_display.h"
#include "mica_globals.h"
#include "mica_commands.h"
#include "mica_network.h"
#include "mica_prefs.h"

// ===========================================================================
// OTA context & boot state
// ===========================================================================

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
  gPendingOtaCommandId = prefsGetStringOrDefault(kPrefsOtaCommandId, "");
  gPendingOtaSourceVersion = prefsGetStringOrDefault(kPrefsOtaSourceVersion, "");
  gPendingOtaTargetVersion = prefsGetStringOrDefault(kPrefsOtaTargetVersion, "");
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

// ===========================================================================
// Firmware fetch helpers (internal)
// ===========================================================================

static bool tryParseFirmwareReleaseInfo(const JsonDocument& document, FirmwareReleaseInfo& info) {
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

static bool validateFirmwareReleaseInfo(const FirmwareReleaseInfo& info, const String& requestedVersion, String& errorCode, String& errorMessage) {
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

// ===========================================================================
// Firmware fetch & download
// ===========================================================================

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

  gOtaInProgress = true;
  drawOtaProgressScreen(20, "recebido");

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
  if (mbedtls_sha256_starts(&shaContext, 0) != 0) {
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
    if (mbedtls_sha256_update(&shaContext, buffer, static_cast<size_t>(readCount)) != 0) {
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
      drawOtaProgressScreen(progress > 90u ? 90u : progress, "baixando...");
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
  if (mbedtls_sha256_finish(&shaContext, shaBytes) != 0) {
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
  drawOtaProgressScreen(92, "gravando...");
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

// ===========================================================================
// OTA background task (Core 0)
// ===========================================================================
// Runs the firmware download + SHA-256 verification + flash write entirely on Core 0.
// Does NOT call MQTT (sendCommandProgress) or matrix (drawOtaProgressScreen) functions.
// Communicates via volatile globals: gOtaProgressPercent, gOtaProgressStage, gOtaTaskResult.

void otaDownloadTaskFn(void* parameter) {
  subscribeCurrentTaskToWatchdog();
  OtaTaskParams* params = static_cast<OtaTaskParams*>(parameter);
  gOtaTaskResult = OtaTaskResult::Running;
  gOtaProgressPercent = 35;
  gOtaProgressStage = "baixando...";

  HTTPClient http;
  if (!beginHttpWithDeviceAuth(http, params->downloadPath)) {
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  int code = http.GET();
  if (code < 200 || code >= 300) {
    http.end();
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  const int contentLength = http.getSize();
  if (contentLength <= 0 || static_cast<uint32_t>(contentLength) != params->fileSizeBytes) {
    http.end();
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  if (!Update.begin(params->fileSizeBytes)) {
    http.end();
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  WiFiClient* stream = http.getStreamPtr();
  if (stream == nullptr) {
    Update.abort();
    http.end();
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  mbedtls_sha256_context shaContext;
  mbedtls_sha256_init(&shaContext);
  if (mbedtls_sha256_starts(&shaContext, 0) != 0) {
    Update.abort();
    mbedtls_sha256_free(&shaContext);
    http.end();
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  uint8_t buffer[4096];
  uint32_t totalRead = 0u;
  unsigned long lastDataMs = millis();
  uint8_t lastProgress = 35u;
  bool downloadOk = true;

  while (totalRead < params->fileSizeBytes) {
    resetTaskWatchdog();
    const size_t availableBytes = stream->available();
    if (availableBytes == 0u) {
      if (!http.connected()) {
        downloadOk = false;
        break;
      }

      if (millis() - lastDataMs > 15000u) {
        downloadOk = false;
        break;
      }

      vTaskDelay(pdMS_TO_TICKS(1));
      continue;
    }

    const size_t chunkSize = availableBytes < sizeof(buffer) ? availableBytes : sizeof(buffer);
    const int readCount = stream->readBytes(buffer, chunkSize);
    if (readCount <= 0) {
      vTaskDelay(pdMS_TO_TICKS(1));
      continue;
    }

    lastDataMs = millis();
    if (mbedtls_sha256_update(&shaContext, buffer, static_cast<size_t>(readCount)) != 0) {
      downloadOk = false;
      break;
    }

    if (Update.write(buffer, static_cast<size_t>(readCount)) != static_cast<size_t>(readCount)) {
      downloadOk = false;
      break;
    }

    totalRead += static_cast<uint32_t>(readCount);
    const uint8_t progress = static_cast<uint8_t>(35u + ((static_cast<uint64_t>(totalRead) * 55u) / params->fileSizeBytes));
    if (progress >= static_cast<uint8_t>(lastProgress + 5u) || totalRead >= params->fileSizeBytes) {
      lastProgress = progress;
      gOtaProgressPercent = progress > 90u ? 90u : progress;
    }
  }

  http.end();

  if (!downloadOk || totalRead != params->fileSizeBytes) {
    Update.abort();
    mbedtls_sha256_free(&shaContext);
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro download";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  uint8_t shaBytes[32];
  if (mbedtls_sha256_finish(&shaContext, shaBytes) != 0) {
    Update.abort();
    mbedtls_sha256_free(&shaContext);
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro sha256";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  mbedtls_sha256_free(&shaContext);
  const String computedSha256 = bytesToLowerHex(shaBytes, sizeof(shaBytes));
  if (!computedSha256.equalsIgnoreCase(params->sha256)) {
    Update.abort();
    gOtaProgressPercent = 0;
    gOtaProgressStage = "sha256 invalido";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  gOtaProgressPercent = 92;
  gOtaProgressStage = "gravando...";

  if (!Update.end() || !Update.isFinished()) {
    gOtaProgressPercent = 0;
    gOtaProgressStage = "erro flash";
    gOtaTaskResult = OtaTaskResult::Failed;
    delete params;
    vTaskDelete(nullptr);
    return;
  }

  gOtaTaskResult = OtaTaskResult::Success;
  delete params;
  vTaskDelete(nullptr);
}

// ===========================================================================
// OTA progress bridge (Core 1, main loop)
// ===========================================================================

void processOtaProgressBridge() {
  const OtaTaskResult result = static_cast<OtaTaskResult>(gOtaTaskResult);
  if (result == OtaTaskResult::Idle) {
    return;
  }

  if (result == OtaTaskResult::Running) {
    const uint8_t currentPercent = gOtaProgressPercent;
    if (currentPercent >= static_cast<uint8_t>(gOtaBridgeLastPercent + 5u)) {
      sendCommandProgress(gOtaBridgeCommandId,
          currentPercent > 90u ? 90u : currentPercent,
          "downloading", "Baixando e gravando firmware...");
      gOtaBridgeLastPercent = currentPercent;
    }
    return;
  }

  if (result == OtaTaskResult::Success) {
    gOtaDownloadTaskHandle = nullptr;

    if (!persistPendingOtaContext(gOtaBridgeCommandId, kFirmwareVersion, gOtaBridgeTargetVersion)) {
      gOtaInProgress = false;
      gOtaTaskResult = OtaTaskResult::Idle;
      gOtaBridgeLastPercent = 0;
      completeSlowCommand(SlowCommandKind::UpdateFirmware);
      setControlWorkerState(ControlWorkerState::Idle);
      sendCommandProgress(gOtaBridgeCommandId, 100, "failed",
          "Firmware baixado, mas falha ao registrar contexto.", 0);
      return;
    }

    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    sendCommandProgress(gOtaBridgeCommandId, 94, "rebooting",
        "Firmware aplicado. Reiniciando para validacao segura.");
    gOtaProgressPercent = 94;
    gOtaProgressStage = "reiniciando...";
    (void)publishPresence("offline");
    delay(250);
    ESP.restart();
    return;
  }

  if (result == OtaTaskResult::Failed) {
    gOtaDownloadTaskHandle = nullptr;
    gOtaInProgress = false;

    const char* failStage = gOtaProgressStage;
    String failMessage = String("Falha OTA: ") + (failStage != nullptr ? failStage : "erro desconhecido");
    (void)publishDeviceLog("warning", "command", "ota_task_failed", failMessage);
    sendCommandProgress(gOtaBridgeCommandId, 100, "failed", failMessage, 0);

    completeSlowCommand(SlowCommandKind::UpdateFirmware);
    setControlWorkerState(ControlWorkerState::Idle);
    gOtaTaskResult = OtaTaskResult::Idle;
    gOtaBridgeLastPercent = 0;
    return;
  }
}

// ===========================================================================
// OTA report, rollback & safe update
// ===========================================================================

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
      gOtaInProgress = true;
      drawOtaProgressScreen(97, "validando...");
    }

    if (millis() - gPendingOtaValidationStartedMs >= kOtaSelfTestWindowMs) {
      if (!String(kFirmwareVersion).equalsIgnoreCase(gPendingOtaTargetVersion)) {
        drawOtaProgressScreen(0, "rollback!");
        delay(2000);
        gOtaInProgress = false;
        requestPendingOtaRollbackAndReboot(
            "ota_target_version_mismatch",
            "Safe update mode detectou firmware diferente da versao alvo apos o reboot.");
        return;
      }

      const esp_err_t validationError = esp_ota_mark_app_valid_cancel_rollback();
      if (validationError != ESP_OK) {
        drawOtaProgressScreen(0, "rollback!");
        delay(2000);
        gOtaInProgress = false;
        requestPendingOtaRollbackAndReboot(
            "ota_mark_valid_failed",
            String("Falha ao confirmar a imagem OTA: ") + esp_err_to_name(validationError) + ".");
        return;
      }

      drawOtaProgressScreen(100, "concluido!");
      delay(2000);
      gOtaInProgress = false;

      gPendingOtaBootState = PendingOtaBootState::ValidatedPendingReport;
      gPendingOtaPendingVerifyAnnounced = false;
      gPendingOtaValidationStartedMs = 0u;
      Serial.printf("[ota] imagem OTA validada com sucesso: %s\n", kFirmwareVersion);
    }
  }

  publishPendingOtaReportIfNeeded();
}
