#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao

#include "mica_types.h"
#include <Arduino.h>

#if defined(MICA_PROFILE_DMA_EXP)
#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>
#endif

// ---------------------------------------------------------------------------
// HUB75 driver config (constexpr — internal linkage per TU, safe in header)
// ---------------------------------------------------------------------------
#if defined(MICA_PROFILE_DMA_EXP)
#if MICA_HUB75_SHIFT_DRIVER == 1
constexpr HUB75_I2S_CFG::shift_driver kHub75BaselineDriver = HUB75_I2S_CFG::FM6124;
#else
constexpr HUB75_I2S_CFG::shift_driver kHub75BaselineDriver = HUB75_I2S_CFG::SHIFTREG;
#endif
#endif

// ---------------------------------------------------------------------------
// Core display
// ---------------------------------------------------------------------------
bool initMatrixDisplay();
void setMatrixBrightness(uint8_t brightness);
void clearMatrix();
void drawMatrixPixel(uint8_t x, uint8_t y, const RgbColor& color);
void fillMatrixRect(int16_t x, int16_t y, int16_t w, int16_t h, const RgbColor& color);
bool commitMatrixFrame();

#if defined(MICA_PROFILE_DMA_EXP)
void drawMatrixTextCentered(const char* text, int16_t baselineY, uint16_t color, uint8_t textSize);
#endif

// ---------------------------------------------------------------------------
// Color conversion
// ---------------------------------------------------------------------------
uint16_t rgb888ToRgb565(uint8_t r, uint8_t g, uint8_t b);
void initializeColorConversionLookups();
RgbColor rainbowColorForColumn(uint16_t column, uint16_t columnCount);
RgbColor rgb565ToRgb888(uint16_t rgb565);
float clamp01f(float value);
uint8_t clampToByte(int value);
RgbColor scaleColor(const RgbColor& color, float amount);
RgbColor mixColor(const RgbColor& left, const RgbColor& right, float amount);
RgbColor sampleGradientStops(const RgbColor* stops, size_t stopCount, float t);

// ---------------------------------------------------------------------------
// Shadow buffer ops
// ---------------------------------------------------------------------------
size_t matrixPixelIndex(uint8_t x, uint8_t y);
void clearMatrixShadowBuffer(uint8_t bufferIndex);
void resetMatrixShadowState();
void setMatrixShadowPixel(uint8_t x, uint8_t y, uint16_t rgb565);
void fillMatrixShadowRect(int16_t x, int16_t y, int16_t w, int16_t h, uint16_t rgb565);

// ---------------------------------------------------------------------------
// Fallback display
// ---------------------------------------------------------------------------
const char* hub75FallbackStateName(Hub75FallbackState state);
Hub75FallbackState resolveHub75FallbackCandidate();
Hub75FallbackState resolveHub75FallbackState(unsigned long nowMs);
void updateHub75FallbackState(unsigned long nowMs);
bool clearConnectivityFallbackFrame();
bool drawConnectivityFallback(Hub75FallbackState state);

#if defined(MICA_PROFILE_DMA_EXP)
void drawConnectivityFallbackIcon(Hub75FallbackState state, uint16_t accentColor, uint16_t neutralColor);
void drawOtaProgressScreen(uint8_t percent, const char* stage);
#endif

// ---------------------------------------------------------------------------
// Pacing
// ---------------------------------------------------------------------------
uint32_t getPhysicalPresentIntervalUs();
uint32_t getEffectiveMatrixPresentIntervalUs();
bool shouldPresentMatrixFrame(uint32_t nowUs);
void markMatrixFrameDirty(bool countAsAppliedFrame);

// ---------------------------------------------------------------------------
// HUB75 driver helpers
// ---------------------------------------------------------------------------
#if defined(MICA_PROFILE_DMA_EXP)
const char* hub75DriverName(HUB75_I2S_CFG::shift_driver driver);
#endif
bool validateMatrixPinConfiguration();
void logMatrixPinout();

// ---------------------------------------------------------------------------
// LED management
// ---------------------------------------------------------------------------
bool isReservedHub75Pin(int pin);
bool tryValidateAuxLedPin(int pin, String& reason);
void initializeOnboardTestLed();
void initializeAuxLed();
bool isTestLedAvailable();
uint8_t clampBrightnessToSafeRange(int value);
uint8_t resolveRequestedBrightness();
uint8_t resolveAppliedBrightness();
void applyAuxTestLedDuty(uint8_t duty);
void applyOnboardTestLedDuty(uint8_t duty);
void applyTestLedDutyToOutputs(uint8_t duty);
void applyTestLedState();
void updateTestLedDutyFromBrightness(uint8_t brightness);
void clearTestLed();
void triggerTestLed();
void updateTestLed();
