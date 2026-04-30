#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao

#include <Arduino.h>
#include <stdint.h>

#include "mica_types.h"

// ---------------------------------------------------------------------------
// Panels batch buffer management
// ---------------------------------------------------------------------------
void clearPanelsBatchBuffer(PanelsBatchBuffer& buffer);
void movePanelsBatchBuffer(PanelsBatchBuffer& source, PanelsBatchBuffer& destination);
void cancelPanelsBatchPlayback();

// ---------------------------------------------------------------------------
// Panels batch runtime
// ---------------------------------------------------------------------------
void initializePanelsBatchRuntime();

// ---------------------------------------------------------------------------
// Panels batch download & validation
// ---------------------------------------------------------------------------
bool tryDownloadPanelsBatch(
    const String& downloadUrl,
    const String& expectedSha256,
    size_t expectedSize,
    const String& expectedContentType,
    PanelsBatchBuffer& batch,
    String& errorCode,
    String& errorMessage);
bool validatePanelsBatchWebp(
    const PanelsBatchBuffer& batch,
    String& errorCode,
    String& errorMessage);

// ---------------------------------------------------------------------------
// Panels batch queue & playback
// ---------------------------------------------------------------------------
bool tryQueuePanelsBatchForPlayback(PanelsBatchBuffer& batch, String& errorCode, String& errorMessage);
bool tryPresentWebpRgbaFrame(const uint8_t* rgbaPixels, size_t rgbaLength);
bool waitForPanelsBatchTimestampUs(int64_t batchStartedUs, int timestampMs);
void panelsBatchPlaybackTask(void* parameter);
