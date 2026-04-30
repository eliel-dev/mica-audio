#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-14-ota-firmware-update-flow-e-hub75-status.md
// DOCS: docs/handoffs/2026-04-14-freertos-ota-background-task.md

#include <Arduino.h>
#include <esp_ota_ops.h>

#include "mica_types.h"

// ---------------------------------------------------------------------------
// OTA context & boot state
// ---------------------------------------------------------------------------
bool hasPendingOtaContext();
bool tryGetRunningOtaState(esp_ota_img_states_t& otaState);
bool isRunningAppPendingVerify();
void loadPendingOtaContext();
void clearPendingOtaContext();
bool persistPendingOtaContext(const String& commandId, const String& sourceVersion, const String& targetVersion);
void initializePendingOtaBootState();

// ---------------------------------------------------------------------------
// Firmware fetch & download
// ---------------------------------------------------------------------------
bool tryFetchLatestFirmwareRelease(FirmwareReleaseInfo& info, const String& requestedVersion, String& errorCode, String& errorMessage);
bool performFirmwareOta(const FirmwareReleaseInfo& info, const String& commandId, String& errorCode, String& errorMessage);

// ---------------------------------------------------------------------------
// OTA background task & progress bridge
// ---------------------------------------------------------------------------
void otaDownloadTaskFn(void* parameter);
void processOtaProgressBridge();

// ---------------------------------------------------------------------------
// OTA report, rollback & safe update
// ---------------------------------------------------------------------------
void publishPendingOtaReportIfNeeded();
void requestPendingOtaRollbackAndReboot(const char* errorCode, const String& errorMessage);
void processPendingOtaSafeUpdate();
