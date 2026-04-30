#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#campos-do-payload-de-telemetria-ws
// DOCS: docs/handoffs/2026-04-17-firmware-control-worker-hardening.md

#include <Arduino.h>

#include "mica_types.h"

void initializeControlCommandRuntime();
bool enqueueIncomingControlCommand(ControlCommandSource source, const uint8_t* payload, size_t length);
void processQueuedControlCommands();
void processAsyncControlEvents();
bool requestProvisioningPortalLaunch(const String& commandId, bool clearDeviceCredentials, const char* reason, bool allowDeferred);
bool isProvisioningPortalLaunchPending();
void processProvisioningLaunchRequest();
void controlWorkerTask(void* parameter);

bool enqueueAsyncCommandProgress(
    const String& commandId,
    uint8_t progressPercent,
    const char* stage,
    const String& message,
    int successFlag = -1);
bool enqueueAsyncDeviceLog(
    const char* level,
    const char* category,
    const char* eventCode,
    const String& message,
    bool includeTelemetrySequence = true);
bool enqueueAsyncOtaStart(const String& commandId, const String& targetVersion, OtaTaskParams* otaParams);

void setControlWorkerState(ControlWorkerState state);
void setPanelsWorkerState(PanelsWorkerState state);
void completeSlowCommand(SlowCommandKind kind);
void resetTaskWatchdog();
void subscribeCurrentTaskToWatchdog();
void unsubscribeCurrentTaskFromWatchdog();
bool isTaskWatchdogReady();
