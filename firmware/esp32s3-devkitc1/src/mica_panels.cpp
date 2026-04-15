// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao

#include "mica_panels.h"

#include <HTTPClient.h>
#include <esp_heap_caps.h>
#include <esp_timer.h>
#include <freertos/FreeRTOS.h>
#include <freertos/semphr.h>
#include <freertos/task.h>
#include <mbedtls/sha256.h>
#include <src/webp/demux.h>

#include "mica_display.h"
#include "mica_globals.h"
#include "mica_network.h"

// ===========================================================================
// Panels batch buffer management
// ===========================================================================

void clearPanelsBatchBuffer(PanelsBatchBuffer& buffer) {
  if (buffer.data != nullptr) {
    free(buffer.data);
    buffer.data = nullptr;
  }

  buffer.panelsSessionId = "";
  buffer.batchSequence = 0;
  buffer.length = 0;
  buffer.frameCount = 0;
  buffer.durationMs = 0;
}

void movePanelsBatchBuffer(PanelsBatchBuffer& source, PanelsBatchBuffer& destination) {
  if (&source == &destination) {
    return;
  }

  clearPanelsBatchBuffer(destination);
  destination.panelsSessionId = source.panelsSessionId;
  destination.batchSequence = source.batchSequence;
  destination.data = source.data;
  destination.length = source.length;
  destination.frameCount = source.frameCount;
  destination.durationMs = source.durationMs;

  source.panelsSessionId = "";
  source.batchSequence = 0;
  source.data = nullptr;
  source.length = 0;
  source.frameCount = 0;
  source.durationMs = 0;
}

void cancelPanelsBatchPlayback() {
  if (gPanelsBatchMutex == nullptr) {
    return;
  }

  if (xSemaphoreTake(gPanelsBatchMutex, portMAX_DELAY) != pdTRUE) {
    return;
  }

  gPanelsBatchCancelRequested = true;
  gPanelsBatchUnderrun = false;
  clearPanelsBatchBuffer(gPanelsBatchPending);
  xSemaphoreGive(gPanelsBatchMutex);
}

// ===========================================================================
// Panels batch runtime
// ===========================================================================

void initializePanelsBatchRuntime() {
  if (gPanelsBatchMutex == nullptr) {
    gPanelsBatchMutex = xSemaphoreCreateMutex();
  }

  if (gPanelsBatchTaskHandle == nullptr) {
    BaseType_t result = xTaskCreatePinnedToCore(
        panelsBatchPlaybackTask,
        "panels_batch_webp",
        kPanelsBatchTaskStackSize,
        nullptr,
        kPanelsBatchTaskPriority,
        &gPanelsBatchTaskHandle,
        1);

    if (result != pdPASS) {
      gAnimatedWebpBatchSupported = false;
      gPanelsBatchTaskHandle = nullptr;
      (void)publishDeviceLog(
          "error",
          "command",
          "panels_batch_runtime_init_failed",
          "Falha ao criar task de playback WebP para Paineis.",
          false);
    }
  }
}

// ===========================================================================
// Panels batch download & validation
// ===========================================================================

bool tryDownloadPanelsBatch(
    const String& downloadUrl,
    const String& expectedSha256,
    size_t expectedSize,
    const String& expectedContentType,
    PanelsBatchBuffer& batch,
    String& errorCode,
    String& errorMessage) {
  errorCode = "";
  errorMessage = "";

  HTTPClient http;
  if (!beginHttpWithDeviceAuthUrl(http, downloadUrl)) {
    errorCode = "panels_batch_begin_failed";
    errorMessage = "Nao foi possivel iniciar o download do lote WebP.";
    return false;
  }

  const int code = http.GET();
  if (code < 200 || code >= 300) {
    errorCode = "panels_batch_http_error";
    errorMessage = String("Falha ao baixar lote WebP. HTTP ") + code + ".";
    http.end();
    return false;
  }

  const int contentLength = http.getSize();
  if (contentLength <= 0 || static_cast<size_t>(contentLength) != expectedSize) {
    errorCode = "panels_batch_size_mismatch";
    errorMessage = "Download do lote WebP retornou tamanho divergente.";
    http.end();
    return false;
  }

  const String responseContentType = http.header("Content-Type");
  if (expectedContentType.length() > 0
      && responseContentType.length() > 0
      && !responseContentType.startsWith(expectedContentType)) {
    errorCode = "panels_batch_content_type_invalid";
    errorMessage = "Servidor retornou content-type incompativel para lote WebP.";
    http.end();
    return false;
  }

  uint8_t* data = static_cast<uint8_t*>(heap_caps_malloc(expectedSize, MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT));
  if (data == nullptr) {
    data = static_cast<uint8_t*>(heap_caps_malloc(expectedSize, MALLOC_CAP_8BIT));
  }

  if (data == nullptr) {
    errorCode = "panels_batch_alloc_failed";
    errorMessage = "Sem memoria para armazenar o lote WebP.";
    http.end();
    return false;
  }

  WiFiClient* stream = http.getStreamPtr();
  if (stream == nullptr) {
    free(data);
    errorCode = "panels_batch_stream_unavailable";
    errorMessage = "Fluxo HTTP indisponivel para lote WebP.";
    http.end();
    return false;
  }

  mbedtls_sha256_context shaContext;
  mbedtls_sha256_init(&shaContext);
  if (mbedtls_sha256_starts(&shaContext, 0) != 0) {
    free(data);
    mbedtls_sha256_free(&shaContext);
    errorCode = "panels_batch_sha_init_failed";
    errorMessage = "Falha ao inicializar SHA-256 do lote WebP.";
    http.end();
    return false;
  }

  size_t totalRead = 0;
  unsigned long lastDataMs = millis();
  while (totalRead < expectedSize) {
    const size_t availableBytes = stream->available();
    if (availableBytes == 0u) {
      if (!http.connected()) {
        break;
      }

      if (millis() - lastDataMs > 15000u) {
        free(data);
        mbedtls_sha256_free(&shaContext);
        errorCode = "panels_batch_download_timeout";
        errorMessage = "Download do lote WebP interrompido por timeout.";
        http.end();
        return false;
      }

      delay(1);
      continue;
    }

    const size_t chunkSize = min(availableBytes, expectedSize - totalRead);
    const int readCount = stream->readBytes(reinterpret_cast<char*>(data + totalRead), chunkSize);
    if (readCount <= 0) {
      delay(1);
      continue;
    }

    lastDataMs = millis();
    if (mbedtls_sha256_update(&shaContext, data + totalRead, static_cast<size_t>(readCount)) != 0) {
      free(data);
      mbedtls_sha256_free(&shaContext);
      errorCode = "panels_batch_sha_update_failed";
      errorMessage = "Falha ao atualizar SHA-256 do lote WebP.";
      http.end();
      return false;
    }

    totalRead += static_cast<size_t>(readCount);
  }

  http.end();

  if (totalRead != expectedSize) {
    free(data);
    mbedtls_sha256_free(&shaContext);
    errorCode = "panels_batch_download_incomplete";
    errorMessage = "Download do lote WebP terminou antes de receber todos os bytes.";
    return false;
  }

  uint8_t shaBytes[32];
  if (mbedtls_sha256_finish(&shaContext, shaBytes) != 0) {
    free(data);
    mbedtls_sha256_free(&shaContext);
    errorCode = "panels_batch_sha_finish_failed";
    errorMessage = "Falha ao finalizar SHA-256 do lote WebP.";
    return false;
  }

  mbedtls_sha256_free(&shaContext);
  const String computedSha256 = bytesToLowerHex(shaBytes, sizeof(shaBytes));
  if (!computedSha256.equalsIgnoreCase(expectedSha256)) {
    free(data);
    errorCode = "panels_batch_sha_mismatch";
    errorMessage = "Hash SHA-256 divergente no lote WebP baixado.";
    return false;
  }

  clearPanelsBatchBuffer(batch);
  batch.data = data;
  batch.length = totalRead;
  return true;
}

bool validatePanelsBatchWebp(
    const PanelsBatchBuffer& batch,
    String& errorCode,
    String& errorMessage) {
  errorCode = "";
  errorMessage = "";

  if (batch.data == nullptr || batch.length == 0u) {
    errorCode = "panels_batch_empty";
    errorMessage = "Lote WebP vazio.";
    return false;
  }

  WebPData webpData;
  WebPDataInit(&webpData);
  webpData.bytes = batch.data;
  webpData.size = batch.length;

  WebPAnimDecoderOptions options;
  if (!WebPAnimDecoderOptionsInit(&options)) {
    errorCode = "panels_batch_decoder_options_failed";
    errorMessage = "Falha ao inicializar opcoes do decoder WebP.";
    return false;
  }

  options.color_mode = MODE_RGBA;
  WebPAnimDecoder* decoder = WebPAnimDecoderNew(&webpData, &options);
  if (decoder == nullptr) {
    errorCode = "panels_batch_decoder_create_failed";
    errorMessage = "Nao foi possivel criar decoder WebP.";
    return false;
  }

  WebPAnimInfo info;
  if (!WebPAnimDecoderGetInfo(decoder, &info)) {
    WebPAnimDecoderDelete(decoder);
    errorCode = "panels_batch_decoder_info_failed";
    errorMessage = "Nao foi possivel ler metadados do WebP animado.";
    return false;
  }

  if (info.canvas_width != static_cast<int>(kMatrixWidth) || info.canvas_height != static_cast<int>(kMatrixHeight)) {
    WebPAnimDecoderDelete(decoder);
    errorCode = "panels_batch_dimensions_invalid";
    errorMessage = "Canvas do WebP animado nao corresponde a 128x64.";
    return false;
  }

  uint32_t observedFrameCount = 0;
  int observedDurationMs = 0;
  while (WebPAnimDecoderHasMoreFrames(decoder)) {
    uint8_t* framePixels = nullptr;
    int timestampMs = 0;
    if (!WebPAnimDecoderGetNext(decoder, &framePixels, &timestampMs) || framePixels == nullptr) {
      WebPAnimDecoderDelete(decoder);
      errorCode = "panels_batch_decoder_frame_failed";
      errorMessage = "Falha ao decodificar frame do WebP animado.";
      return false;
    }

    observedFrameCount++;
    observedDurationMs = timestampMs;
  }

  WebPAnimDecoderDelete(decoder);

  if (batch.frameCount > 0u && observedFrameCount != batch.frameCount) {
    errorCode = "panels_batch_frame_count_mismatch";
    errorMessage = "Quantidade de frames do WebP difere do manifesto do lote.";
    return false;
  }

  if (batch.durationMs > 0u && abs(observedDurationMs - static_cast<int>(batch.durationMs)) > 20) {
    errorCode = "panels_batch_duration_mismatch";
    errorMessage = "Duracao do WebP difere do manifesto do lote.";
    return false;
  }

  return true;
}

// ===========================================================================
// Panels batch queue & playback
// ===========================================================================

bool tryQueuePanelsBatchForPlayback(PanelsBatchBuffer& batch, String& errorCode, String& errorMessage) {
  errorCode = "";
  errorMessage = "";

  if (gPanelsBatchMutex == nullptr) {
    errorCode = "panels_batch_runtime_unavailable";
    errorMessage = "Runtime de playback WebP nao inicializado.";
    return false;
  }

  if (xSemaphoreTake(gPanelsBatchMutex, portMAX_DELAY) != pdTRUE) {
    errorCode = "panels_batch_mutex_failed";
    errorMessage = "Falha ao sincronizar fila de playback WebP.";
    return false;
  }

  const bool sessionChanged = gPanelsBatchCurrentSessionId.length() > 0
      && !gPanelsBatchCurrentSessionId.equals(batch.panelsSessionId);

  if (sessionChanged) {
    gPanelsBatchCancelRequested = true;
    clearPanelsBatchBuffer(gPanelsBatchPending);
    gPanelsBatchCurrentSessionId = "";
    gPanelsBatchCurrentSequence = 0u;
  }

  if (batch.panelsSessionId.equals(gPanelsBatchCurrentSessionId)
      && batch.batchSequence <= gPanelsBatchCurrentSequence) {
    xSemaphoreGive(gPanelsBatchMutex);
    clearPanelsBatchBuffer(batch);
    return true;
  }

  if (gPanelsBatchPending.data != nullptr) {
    if (batch.panelsSessionId.equals(gPanelsBatchPending.panelsSessionId)
        && batch.batchSequence <= gPanelsBatchPending.batchSequence) {
      xSemaphoreGive(gPanelsBatchMutex);
      clearPanelsBatchBuffer(batch);
      return true;
    }

    clearPanelsBatchBuffer(gPanelsBatchPending);
  }

  movePanelsBatchBuffer(batch, gPanelsBatchPending);
  gPanelsBatchUnderrun = false;
  xSemaphoreGive(gPanelsBatchMutex);

  if (gPanelsBatchTaskHandle != nullptr) {
    xTaskNotifyGive(gPanelsBatchTaskHandle);
  }

  return true;
}

bool tryPresentWebpRgbaFrame(const uint8_t* rgbaPixels, size_t rgbaLength) {
  if (rgbaPixels == nullptr || rgbaLength < (kMatrixPixelCount * 4u)) {
    return false;
  }

  const uint8_t nextFrameIndex = static_cast<uint8_t>(gFrameRgb565ActiveIndex ^ 1u);
  uint16_t* frameBackBuffer = gFrameRgb565Buffers[nextFrameIndex];
  for (size_t pixelIndex = 0; pixelIndex < kMatrixPixelCount; pixelIndex++) {
    const size_t offset = pixelIndex * 4u;
    frameBackBuffer[pixelIndex] = rgb888ToRgb565(
        rgbaPixels[offset],
        rgbaPixels[offset + 1u],
        rgbaPixels[offset + 2u]);
  }

  portENTER_CRITICAL(&gStreamBufferMux);
  gFrameRgb565ActiveIndex = nextFrameIndex;
  portEXIT_CRITICAL(&gStreamBufferMux);

  gFrameModeActive = true;
  gLastFrameMs = millis();
  gMatrixSignalTimedOut = false;
  markMatrixFrameDirty(false);
  return true;
}

bool waitForPanelsBatchTimestampUs(int64_t batchStartedUs, int timestampMs) {
  const int64_t targetUs = batchStartedUs + (static_cast<int64_t>(timestampMs) * 1000LL);
  while (true) {
    if (gPanelsBatchMutex != nullptr && xSemaphoreTake(gPanelsBatchMutex, portMAX_DELAY) == pdTRUE) {
      const bool cancelled = gPanelsBatchCancelRequested;
      xSemaphoreGive(gPanelsBatchMutex);
      if (cancelled) {
        return false;
      }
    }

    const int64_t nowUs = esp_timer_get_time();
    if (nowUs >= targetUs) {
      return true;
    }

    const int64_t remainingUs = targetUs - nowUs;
    if (remainingUs > static_cast<int64_t>(kPanelsBatchWaitChunkMs) * 1000LL) {
      vTaskDelay(pdMS_TO_TICKS(kPanelsBatchWaitChunkMs));
    } else {
      taskYIELD();
    }
  }
}

void panelsBatchPlaybackTask(void* parameter) {
  (void)parameter;
  PanelsBatchBuffer current = {};

  while (true) {
    if (gPanelsBatchMutex != nullptr && xSemaphoreTake(gPanelsBatchMutex, portMAX_DELAY) == pdTRUE) {
      if (gPanelsBatchCancelRequested) {
        gPanelsBatchCancelRequested = false;
        clearPanelsBatchBuffer(current);
        gPanelsBatchCurrentSessionId = "";
        gPanelsBatchCurrentSequence = 0u;
      }

      if (current.data == nullptr && gPanelsBatchPending.data != nullptr) {
        movePanelsBatchBuffer(gPanelsBatchPending, current);
        gPanelsBatchCurrentSessionId = current.panelsSessionId;
        gPanelsBatchCurrentSequence = current.batchSequence;
      }

      xSemaphoreGive(gPanelsBatchMutex);
    }

    if (current.data == nullptr) {
      ulTaskNotifyTake(pdTRUE, pdMS_TO_TICKS(20));
      continue;
    }

    WebPData webpData;
    WebPDataInit(&webpData);
    webpData.bytes = current.data;
    webpData.size = current.length;

    WebPAnimDecoderOptions options;
    WebPAnimDecoderOptionsInit(&options);
    options.color_mode = MODE_RGBA;

    WebPAnimDecoder* decoder = WebPAnimDecoderNew(&webpData, &options);
    if (decoder == nullptr) {
      (void)publishDeviceLog("error", "command", "panels_batch_decoder_create_failed", "Falha ao criar decoder do lote WebP.", false);
      clearPanelsBatchBuffer(current);
      continue;
    }

    WebPAnimInfo info;
    if (!WebPAnimDecoderGetInfo(decoder, &info)) {
      (void)publishDeviceLog("error", "command", "panels_batch_decoder_info_failed", "Falha ao ler metadados do lote WebP.", false);
      WebPAnimDecoderDelete(decoder);
      clearPanelsBatchBuffer(current);
      continue;
    }

    const int64_t batchStartedUs = esp_timer_get_time();
    bool cancelled = false;
    bool decodeFailed = false;

    while (WebPAnimDecoderHasMoreFrames(decoder)) {
      uint8_t* rgbaPixels = nullptr;
      int timestampMs = 0;
      if (!WebPAnimDecoderGetNext(decoder, &rgbaPixels, &timestampMs) || rgbaPixels == nullptr) {
        decodeFailed = true;
        break;
      }

      if (!tryPresentWebpRgbaFrame(rgbaPixels, static_cast<size_t>(info.canvas_width) * static_cast<size_t>(info.canvas_height) * 4u)) {
        decodeFailed = true;
        break;
      }

      if (!waitForPanelsBatchTimestampUs(batchStartedUs, timestampMs)) {
        cancelled = true;
        break;
      }
    }

    WebPAnimDecoderDelete(decoder);

    if (gPanelsBatchMutex != nullptr && xSemaphoreTake(gPanelsBatchMutex, portMAX_DELAY) == pdTRUE) {
      if (gPanelsBatchCurrentSequence == current.batchSequence
          && gPanelsBatchCurrentSessionId.equals(current.panelsSessionId)) {
        gPanelsBatchCurrentSequence = 0u;
        gPanelsBatchCurrentSessionId = "";
      }

      if (!cancelled && gPanelsBatchPending.data == nullptr) {
        gPanelsBatchUnderrun = true;
      }

      xSemaphoreGive(gPanelsBatchMutex);
    }

    if (decodeFailed) {
      (void)publishDeviceLog("warning", "command", "panels_batch_decode_failed", "Falha ao reproduzir um lote WebP de Paineis.", false);
    }

    clearPanelsBatchBuffer(current);
  }
}
