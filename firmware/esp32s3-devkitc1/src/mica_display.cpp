#include "mica_display.h"
#include "mica_globals.h"
#include <Arduino.h>
#include <WiFi.h>
#include <soc/soc_caps.h>

// ---------------------------------------------------------------------------
// Fallback state name
// ---------------------------------------------------------------------------
const char* hub75FallbackStateName(Hub75FallbackState state) {
  switch (state) {
    case Hub75FallbackState::NoWifi:
      return "no_wifi";
    case Hub75FallbackState::NoServer:
      return "no_server";
    case Hub75FallbackState::Portal:
      return "portal";
    case Hub75FallbackState::Updating:
      return "updating";
    case Hub75FallbackState::None:
    default:
      return "none";
  }
}

// ---------------------------------------------------------------------------
// Shadow buffer helpers
// ---------------------------------------------------------------------------
size_t matrixPixelIndex(uint8_t x, uint8_t y) {
  return static_cast<size_t>(y) * static_cast<size_t>(kMatrixWidth) + static_cast<size_t>(x);
}

// ---------------------------------------------------------------------------
// Color conversion
// ---------------------------------------------------------------------------
uint16_t rgb888ToRgb565(uint8_t r, uint8_t g, uint8_t b) {
  return static_cast<uint16_t>(((r & 0xF8u) << 8) | ((g & 0xFCu) << 3) | (b >> 3));
}

void initializeColorConversionLookups() {
  for (uint8_t value = 0; value < 32; value++) {
    gRgb5To8Lut[value] = static_cast<uint8_t>((static_cast<uint16_t>(value) * 255u + 15u) / 31u);
  }

  for (uint8_t value = 0; value < 64; value++) {
    gRgb6To8Lut[value] = static_cast<uint8_t>((static_cast<uint16_t>(value) * 255u + 31u) / 63u);
  }
}

void clearMatrixShadowBuffer(uint8_t bufferIndex) {
  if (bufferIndex >= kMatrixShadowBufferCount) {
    return;
  }

  memset(gMatrixShadowFrames[bufferIndex], 0, sizeof(gMatrixShadowFrames[bufferIndex]));
  memset(gMatrixShadowBarHeights[bufferIndex], 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
  gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Clear;
}

void resetMatrixShadowState() {
  for (uint8_t bufferIndex = 0; bufferIndex < kMatrixShadowBufferCount; bufferIndex++) {
    clearMatrixShadowBuffer(bufferIndex);
  }

  gMatrixShadowBackBufferIndex = 0;
}

void setMatrixShadowPixel(uint8_t x, uint8_t y, uint16_t rgb565) {
  if (x >= kMatrixWidth || y >= kMatrixHeight) {
    return;
  }

  gMatrixShadowFrames[gMatrixShadowBackBufferIndex][matrixPixelIndex(x, y)] = rgb565;
}

void fillMatrixShadowRect(int16_t x, int16_t y, int16_t w, int16_t h, uint16_t rgb565) {
  if (w <= 0 || h <= 0) {
    return;
  }

  const int16_t xStart = x < 0 ? 0 : x;
  const int16_t yStart = y < 0 ? 0 : y;
  const int16_t xEnd = (x + w) > static_cast<int16_t>(kMatrixWidth) ? static_cast<int16_t>(kMatrixWidth) : static_cast<int16_t>(x + w);
  const int16_t yEnd = (y + h) > static_cast<int16_t>(kMatrixHeight) ? static_cast<int16_t>(kMatrixHeight) : static_cast<int16_t>(y + h);
  if (xStart >= xEnd || yStart >= yEnd) {
    return;
  }

  for (int16_t row = yStart; row < yEnd; row++) {
    uint16_t* shadowRow =
        gMatrixShadowFrames[gMatrixShadowBackBufferIndex] + (static_cast<size_t>(row) * static_cast<size_t>(kMatrixWidth));
    for (int16_t column = xStart; column < xEnd; column++) {
      shadowRow[column] = rgb565;
    }
  }
}

RgbColor rainbowColorForColumn(uint16_t column, uint16_t columnCount) {
  if (columnCount <= 1) {
    return {255, 0, 0};
  }

  const uint8_t hue = static_cast<uint8_t>((column * 255u) / (columnCount - 1u));
  const uint8_t region = hue / 43u;
  const uint8_t remainder = static_cast<uint8_t>((hue - (region * 43u)) * 6u);
  const uint8_t q = static_cast<uint8_t>(255u - remainder);
  const uint8_t t = remainder;

  switch (region) {
    case 0:
      return {255, t, 0};
    case 1:
      return {q, 255, 0};
    case 2:
      return {0, 255, t};
    case 3:
      return {0, q, 255};
    case 4:
      return {t, 0, 255};
    default:
      return {255, 0, q};
  }
}

RgbColor rgb565ToRgb888(uint16_t rgb565) {
  return {
      gRgb5To8Lut[(rgb565 >> 11) & 0x1Fu],
      gRgb6To8Lut[(rgb565 >> 5) & 0x3Fu],
      gRgb5To8Lut[rgb565 & 0x1Fu]};
}

float clamp01f(float value) {
  if (value <= 0.0f) {
    return 0.0f;
  }

  return value >= 1.0f ? 1.0f : value;
}

uint8_t clampToByte(int value) {
  if (value <= 0) {
    return 0;
  }

  return value >= 255 ? 255 : static_cast<uint8_t>(value);
}

RgbColor scaleColor(const RgbColor& color, float amount) {
  const float scale = clamp01f(amount);
  return {
      clampToByte(static_cast<int>(roundf(color.r * scale))),
      clampToByte(static_cast<int>(roundf(color.g * scale))),
      clampToByte(static_cast<int>(roundf(color.b * scale)))};
}

RgbColor mixColor(const RgbColor& left, const RgbColor& right, float amount) {
  const float t = clamp01f(amount);
  return {
      clampToByte(static_cast<int>(roundf(left.r + ((right.r - left.r) * t)))),
      clampToByte(static_cast<int>(roundf(left.g + ((right.g - left.g) * t)))),
      clampToByte(static_cast<int>(roundf(left.b + ((right.b - left.b) * t))))};
}

RgbColor sampleGradientStops(const RgbColor* stops, size_t stopCount, float t) {
  if (stops == nullptr || stopCount == 0) {
    return {255, 255, 255};
  }

  if (stopCount == 1) {
    return stops[0];
  }

  const float clamped = clamp01f(t);
  const float scaled = clamped * static_cast<float>(stopCount - 1u);
  const size_t leftIndex = static_cast<size_t>(scaled);
  const size_t rightIndex = leftIndex + 1u >= stopCount ? stopCount - 1u : leftIndex + 1u;
  return mixColor(stops[leftIndex], stops[rightIndex], scaled - static_cast<float>(leftIndex));
}

// ---------------------------------------------------------------------------
// LED pin validation and management
// ---------------------------------------------------------------------------
bool isReservedHub75Pin(int pin) {
  for (uint8_t gpio : kMatrixRgbPins) {
    if (static_cast<int>(gpio) == pin) {
      return true;
    }
  }

  for (uint8_t gpio : kMatrixAddrPins) {
    if (static_cast<int>(gpio) == pin) {
      return true;
    }
  }

  return pin == static_cast<int>(kMatrixClockPin)
      || pin == static_cast<int>(kMatrixLatchPin)
      || pin == static_cast<int>(kMatrixOePin);
}

bool tryValidateAuxLedPin(int pin, String& reason) {
  if (pin < 0) {
    reason = "desabilitado por build flag";
    return false;
  }

  if (pin >= static_cast<int>(SOC_GPIO_PIN_COUNT)) {
    reason = "fora da faixa de GPIO fisico";
    return false;
  }

  if (isReservedHub75Pin(pin)) {
    reason = "conflito com pinos HUB75";
    return false;
  }

  if (pin == static_cast<int>(kSerialRxPin) || pin == static_cast<int>(kSerialTxPin)) {
    reason = "conflito com serial";
    return false;
  }

  return true;
}

void initializeOnboardTestLed() {
  gOnboardTestLedAvailable = false;

#if defined(RGB_BUILTIN) || defined(PIN_NEOPIXEL)
  if (kOnboardTestLedPin >= 0) {
    gOnboardTestLedAvailable = true;
    rgbLedWrite(kOnboardTestLedPin, 0, 0, 0);
    Serial.printf("[led] LED onboard habilitado no pino %d.\n", kOnboardTestLedPin);
    return;
  }
#endif

  Serial.println("[led] LED onboard indisponivel neste build.");
}

void initializeAuxLed() {
  gAuxLedAvailable = false;
  gTestLedPwmReady = false;
  gTestLedDuty = 0;
  gAuxLedUnavailableReason = "";

  String validationReason;
  if (!tryValidateAuxLedPin(kTestLedPin, validationReason)) {
    gAuxLedUnavailableReason = validationReason;
    gTestLedEnabled = false;
    gPrefs.putBool("testLedEnabled", false);
    Serial.printf("[led] LED auxiliar indisponivel (GPIO %d): %s\n", kTestLedPin, gAuxLedUnavailableReason.c_str());
    return;
  }

  if (!ledcAttach(kTestLedPin, kTestLedPwmFrequencyHz, kTestLedPwmResolutionBits)) {
    gAuxLedUnavailableReason = "falha no ledcAttach";
    gTestLedEnabled = false;
    gPrefs.putBool("testLedEnabled", false);
    Serial.printf("[led] Falha ao inicializar PWM do LED auxiliar (GPIO %d).\n", kTestLedPin);
    return;
  }
  gTestLedPwmReady = true;
  gAuxLedAvailable = true;
  Serial.printf("[led] LED auxiliar habilitado no GPIO %d.\n", kTestLedPin);
}

bool isTestLedAvailable() {
  return gOnboardTestLedAvailable || gAuxLedAvailable;
}

uint8_t clampBrightnessToSafeRange(int value) {
  if (value < static_cast<int>(kBrightnessSafeMin)) {
    return kBrightnessSafeMin;
  }

  if (value > static_cast<int>(kBrightnessSafeMax)) {
    return kBrightnessSafeMax;
  }

  return static_cast<uint8_t>(value);
}

uint8_t resolveRequestedBrightness() {
  return clampBrightnessToSafeRange(static_cast<int>(gStreamBrightness));
}

uint8_t resolveAppliedBrightness() {
  const uint8_t requested = resolveRequestedBrightness();
  return (requested < gBrightnessCap) ? requested : gBrightnessCap;
}

void applyAuxTestLedDuty(uint8_t duty) {
  if (!gAuxLedAvailable || !gTestLedPwmReady) {
    return;
  }

  ledcWrite(kTestLedPin, duty);
}

void applyOnboardTestLedDuty(uint8_t duty) {
  if (!gOnboardTestLedAvailable) {
    return;
  }

#if defined(RGB_BUILTIN) || defined(PIN_NEOPIXEL)
  rgbLedWrite(kOnboardTestLedPin, duty, duty, duty);
#else
  (void)duty;
#endif
}

void applyTestLedDutyToOutputs(uint8_t duty) {
  applyAuxTestLedDuty(duty);
  applyOnboardTestLedDuty(duty);
}

void applyTestLedState() {
  if (!isTestLedAvailable()) {
    return;
  }

  if (gTestLedUntilMs > 0) {
    applyTestLedDutyToOutputs(gTestLedState ? gTestLedPulseDuty : 0);
    return;
  }

  if (gAuxLedAvailable && gTestLedEnabled) {
    applyAuxTestLedDuty(gTestLedDuty);
  } else {
    applyAuxTestLedDuty(0);
  }

  applyOnboardTestLedDuty(0);
}

void updateTestLedDutyFromBrightness(uint8_t brightness) {
  if (!gAuxLedAvailable) {
    gTestLedDuty = 0;
  } else if (gTestLedDuty != brightness) {
    gTestLedDuty = brightness;
  }

  gTestLedPulseDuty = brightness;
  if (gTestLedUntilMs == 0) {
    applyTestLedState();
  }
}

// ---------------------------------------------------------------------------
// Matrix brightness, clear, draw, fill
// ---------------------------------------------------------------------------
void setMatrixBrightness(uint8_t brightness) {
  if (gAppliedBrightness == brightness) {
    return;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrixReady && gMatrix != nullptr) {
    gMatrix->setBrightness8(brightness);
  }
#endif

  gAppliedBrightness = brightness;
}

void clearMatrix() {
  if (!gMatrixReady) {
    return;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->clearScreen();
    clearMatrixShadowBuffer(gMatrixShadowBackBufferIndex);
  }
#endif
}

void drawMatrixPixel(uint8_t x, uint8_t y, const RgbColor& color) {

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->drawPixelRGB888(x, y, color.r, color.g, color.b);
    setMatrixShadowPixel(x, y, rgb888ToRgb565(color.r, color.g, color.b));
  }
#endif
}

void fillMatrixRect(int16_t x, int16_t y, int16_t w, int16_t h, const RgbColor& color) {
  if (!gMatrixReady || w <= 0 || h <= 0) {
    return;
  }

  const int16_t xStart = x < 0 ? 0 : x;
  const int16_t yStart = y < 0 ? 0 : y;
  const int16_t xEnd = (x + w) > static_cast<int16_t>(kMatrixWidth) ? static_cast<int16_t>(kMatrixWidth) : static_cast<int16_t>(x + w);
  const int16_t yEnd = (y + h) > static_cast<int16_t>(kMatrixHeight) ? static_cast<int16_t>(kMatrixHeight) : static_cast<int16_t>(y + h);
  if (xStart >= xEnd || yStart >= yEnd) {
    return;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->fillRect(
        xStart,
        yStart,
        xEnd - xStart,
        yEnd - yStart,
        rgb888ToRgb565(color.r, color.g, color.b));
    fillMatrixShadowRect(xStart, yStart, xEnd - xStart, yEnd - yStart, rgb888ToRgb565(color.r, color.g, color.b));
  }
#endif
}

// ---------------------------------------------------------------------------
// Conditional HUB75 display primitives
// ---------------------------------------------------------------------------
#if defined(MICA_PROFILE_DMA_EXP)
void drawMatrixTextCentered(const char* text, int16_t baselineY, uint16_t color, uint8_t textSize) {
  if (gMatrix == nullptr || text == nullptr || text[0] == '\0') {
    return;
  }

  int16_t x1 = 0;
  int16_t y1 = 0;
  uint16_t textWidth = 0;
  uint16_t textHeight = 0;
  gMatrix->setTextWrap(false);
  gMatrix->setTextSize(textSize);
  gMatrix->getTextBounds(text, 0, baselineY, &x1, &y1, &textWidth, &textHeight);

  int16_t cursorX = ((static_cast<int16_t>(kMatrixWidth) - static_cast<int16_t>(textWidth)) / 2) - x1;
  if (cursorX < 0) {
    cursorX = 0;
  }

  gMatrix->setCursor(cursorX, baselineY);
  gMatrix->setTextColor(color);
  gMatrix->print(text);
}

void drawConnectivityFallbackIcon(Hub75FallbackState state, uint16_t accentColor, uint16_t neutralColor) {
  if (gMatrix == nullptr) {
    return;
  }

  constexpr int16_t kIconCenterX = kMatrixWidth / 2;
  constexpr int16_t kIconTopY = 8;
  switch (state) {
    case Hub75FallbackState::NoWifi:
      gMatrix->fillRect(kIconCenterX - 11, kIconTopY + 8, 3, 4, neutralColor);
      gMatrix->fillRect(kIconCenterX - 4, kIconTopY + 5, 3, 7, neutralColor);
      gMatrix->fillRect(kIconCenterX + 3, kIconTopY + 2, 3, 10, neutralColor);
      gMatrix->drawLine(kIconCenterX - 14, kIconTopY + 12, kIconCenterX + 10, kIconTopY, accentColor);
      gMatrix->drawLine(kIconCenterX - 13, kIconTopY + 12, kIconCenterX + 11, kIconTopY, accentColor);
      break;
    case Hub75FallbackState::NoServer:
      gMatrix->drawRect(kIconCenterX - 12, kIconTopY + 1, 24, 12, neutralColor);
      gMatrix->fillRect(kIconCenterX - 8, kIconTopY + 4, 3, 3, accentColor);
      gMatrix->fillRect(kIconCenterX - 1, kIconTopY + 4, 3, 3, accentColor);
      gMatrix->fillRect(kIconCenterX + 6, kIconTopY + 4, 3, 3, accentColor);
      gMatrix->drawLine(kIconCenterX - 5, kIconTopY + 16, kIconCenterX + 5, kIconTopY + 16, neutralColor);
      gMatrix->drawLine(kIconCenterX, kIconTopY + 13, kIconCenterX, kIconTopY + 18, neutralColor);
      break;
    case Hub75FallbackState::Portal:
      gMatrix->fillRect(kIconCenterX - 1, kIconTopY + 8, 3, 6, neutralColor);
      gMatrix->drawLine(kIconCenterX - 7, kIconTopY + 12, kIconCenterX - 1, kIconTopY + 9, accentColor);
      gMatrix->drawLine(kIconCenterX + 1, kIconTopY + 9, kIconCenterX + 7, kIconTopY + 12, accentColor);
      gMatrix->drawLine(kIconCenterX - 11, kIconTopY + 14, kIconCenterX - 3, kIconTopY + 10, accentColor);
      gMatrix->drawLine(kIconCenterX + 3, kIconTopY + 10, kIconCenterX + 11, kIconTopY + 14, accentColor);
      break;
    case Hub75FallbackState::None:
    default:
      break;
  }
}

void drawOtaProgressScreen(uint8_t percent, const char* stage) {
  if (!gMatrixReady || gMatrix == nullptr) {
    return;
  }

  gOtaProgressPercent = percent;
  gOtaProgressStage = stage;

  clearMatrix();

  const uint16_t accentColor = rgb888ToRgb565(48, 160, 255);
  const uint16_t titleColor = rgb888ToRgb565(244, 244, 244);
  const uint16_t subtitleColor = rgb888ToRgb565(158, 170, 180);
  const uint16_t barBgColor = rgb888ToRgb565(36, 48, 60);

  drawMatrixTextCentered("ATUALIZANDO", 14, titleColor, 1);
  gMatrix->drawFastHLine(24, 20, kMatrixWidth - 48, rgb888ToRgb565(56, 68, 80));

  constexpr int16_t barX = 10;
  constexpr int16_t barY = 28;
  constexpr int16_t barWidth = kMatrixWidth - 20;
  constexpr int16_t barHeight = 8;
  gMatrix->fillRect(barX, barY, barWidth, barHeight, barBgColor);
  gMatrix->drawRect(barX, barY, barWidth, barHeight, rgb888ToRgb565(56, 68, 80));

  const int16_t fillWidth = static_cast<int16_t>((static_cast<uint32_t>(percent) * static_cast<uint32_t>(barWidth - 2)) / 100u);
  if (fillWidth > 0) {
    gMatrix->fillRect(barX + 1, barY + 1, fillWidth, barHeight - 2, accentColor);
  }

  char percentText[8];
  snprintf(percentText, sizeof(percentText), "%u%%", static_cast<unsigned>(percent));
  drawMatrixTextCentered(percentText, 48, titleColor, 1);

  if (stage != nullptr && stage[0] != '\0') {
    drawMatrixTextCentered(stage, 58, subtitleColor, 1);
  }

  gMatrixBufferModes[gMatrixShadowBackBufferIndex] = MatrixBufferMode::Clear;
  commitMatrixFrame();
}
#endif

// ---------------------------------------------------------------------------
// Fallback state resolution
// ---------------------------------------------------------------------------
Hub75FallbackState resolveHub75FallbackCandidate() {
  if (gOtaInProgress) {
    return Hub75FallbackState::Updating;
  }

  if (gProvisioningPortalActive) {
    return Hub75FallbackState::Portal;
  }

  if (WiFi.status() != WL_CONNECTED) {
    return Hub75FallbackState::NoWifi;
  }

  if (!gWs.isConnected()) {
    return Hub75FallbackState::NoServer;
  }

  return Hub75FallbackState::None;
}

Hub75FallbackState resolveHub75FallbackState(unsigned long nowMs) {
  const Hub75FallbackState candidate = resolveHub75FallbackCandidate();
  if (candidate == Hub75FallbackState::None || candidate == Hub75FallbackState::Portal) {
    gHub75FallbackPendingState = candidate;
    gHub75FallbackPendingSinceMs = 0;
    return candidate;
  }

  if (candidate == gHub75FallbackState) {
    gHub75FallbackPendingState = candidate;
    gHub75FallbackPendingSinceMs = 0;
    return candidate;
  }

  if (candidate != gHub75FallbackPendingState) {
    gHub75FallbackPendingState = candidate;
    gHub75FallbackPendingSinceMs = nowMs;
    return Hub75FallbackState::None;
  }

  if (gHub75FallbackPendingSinceMs != 0
      && (nowMs - gHub75FallbackPendingSinceMs) >= kConnectivityFallbackDebounceMs) {
    return candidate;
  }

  return Hub75FallbackState::None;
}

void updateHub75FallbackState(unsigned long nowMs) {
  const Hub75FallbackState nextState = resolveHub75FallbackState(nowMs);
  if (nextState == gHub75FallbackState) {
    return;
  }

  const Hub75FallbackState previousState = gHub75FallbackState;
  gHub75FallbackState = nextState;
  gHub75FallbackDirty = nextState != Hub75FallbackState::None;
  gHub75FallbackClearPending = previousState != Hub75FallbackState::None && nextState == Hub75FallbackState::None;
  if (nextState != Hub75FallbackState::None) {
    gHub75FallbackClearPending = false;
  }

  Serial.printf(
      "[hub75_fallback] previous=%s next=%s\n",
      hub75FallbackStateName(previousState),
      hub75FallbackStateName(nextState));
}

bool clearConnectivityFallbackFrame() {
  if (!gMatrixReady) {
    return false;
  }

  clearMatrix();
  return commitMatrixFrame();
}

bool drawConnectivityFallback(Hub75FallbackState state) {
  if (!gMatrixReady || state == Hub75FallbackState::None) {
    return false;
  }

  const char* title = "";
  const char* subtitle = "";
  RgbColor accent = {0, 0, 0};
  switch (state) {
    case Hub75FallbackState::NoWifi:
      title = "SEM WIFI";
      subtitle = "Conecte a rede";
      accent = {255, 170, 48};
      break;
    case Hub75FallbackState::NoServer:
      title = "SEM SERV";
      subtitle = "Abra o MicaAudio";
      accent = {255, 96, 96};
      break;
    case Hub75FallbackState::Portal:
      title = "SETUP WIFI";
      subtitle = "Conecte no portal";
      accent = {96, 220, 255};
      break;
    case Hub75FallbackState::Updating:
#if defined(MICA_PROFILE_DMA_EXP)
      drawOtaProgressScreen(gOtaProgressPercent, gOtaProgressStage);
      return true;
#else
      return false;
#endif
    case Hub75FallbackState::None:
    default:
      return false;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  clearMatrix();
  const uint16_t accentColor = rgb888ToRgb565(accent.r, accent.g, accent.b);
  const uint16_t titleColor = rgb888ToRgb565(244, 244, 244);
  const uint16_t subtitleColor = rgb888ToRgb565(158, 170, 180);
  drawConnectivityFallbackIcon(state, accentColor, titleColor);
  gMatrix->drawFastHLine(24, 22, kMatrixWidth - 48, rgb888ToRgb565(36, 48, 60));
  drawMatrixTextCentered(title, 36, titleColor, 2);
  drawMatrixTextCentered(subtitle, 50, subtitleColor, 1);
  gMatrixBufferModes[gMatrixShadowBackBufferIndex] = MatrixBufferMode::Clear;
  return commitMatrixFrame();
#else
  return false;
#endif
}

// ---------------------------------------------------------------------------
// Frame commit and pacing
// ---------------------------------------------------------------------------
bool commitMatrixFrame() {
#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
#if CORE_DEBUG_LEVEL >= 3
    configASSERT(xPortGetCoreID() == 1);
#endif
    gMatrix->flipDMABuffer();
    gLastMatrixPresentUs = micros();
    gHub75PresentFrames++;
    gMatrixShadowBackBufferIndex ^= 1u;
    return true;
  }
#endif

  return false;
}

uint32_t getPhysicalPresentIntervalUs() {
#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr && gMatrix->calculated_refresh_rate > 0) {
    const uint32_t refreshRate = static_cast<uint32_t>(gMatrix->calculated_refresh_rate);
    const uint32_t intervalUs = ceilDivideU32(kMicrosPerSecond, refreshRate);
    return intervalUs == 0 ? 1u : intervalUs;
  }
#endif

  return kHub75FallbackPresentIntervalUs;
}

uint32_t getEffectiveMatrixPresentIntervalUs() {
  const uint32_t physicalPresentIntervalUs = getPhysicalPresentIntervalUs();
  return physicalPresentIntervalUs > kHub75TargetPresentIntervalUs
      ? physicalPresentIntervalUs
      : kHub75TargetPresentIntervalUs;
}

bool shouldPresentMatrixFrame(uint32_t nowUs) {
  return gLastMatrixPresentUs == 0
      || static_cast<uint32_t>(nowUs - gLastMatrixPresentUs) >= getEffectiveMatrixPresentIntervalUs();
}

void markMatrixFrameDirty(bool countAsAppliedFrame) {
  gMatrixFrameDirty = true;
  if (countAsAppliedFrame) {
    gPendingMatrixPresentCountsAsApplied = true;
  }
}

// ---------------------------------------------------------------------------
// HUB75 driver helpers
// ---------------------------------------------------------------------------
#if defined(MICA_PROFILE_DMA_EXP)
const char* hub75DriverName(HUB75_I2S_CFG::shift_driver driver) {
  switch (driver) {
    case HUB75_I2S_CFG::SHIFTREG:
      return "SHIFTREG";
    case HUB75_I2S_CFG::FM6124:
      return "FM6124";
    case HUB75_I2S_CFG::FM6126A:
      return "FM6126A";
    case HUB75_I2S_CFG::ICN2038S:
      return "ICN2038S";
    case HUB75_I2S_CFG::MBI5124:
      return "MBI5124";
    default:
      return "UNKNOWN";
  }
}
#endif

bool validateMatrixPinConfiguration() {
  if (kMatrixHeight < 64) {
    return true;
  }

  const int ePin = static_cast<int>(kMatrixAddrPins[4]);
  if (ePin < 0) {
    Serial.println("Pinout HUB75 invalido: painel 128x64 exige linha E.");
    return false;
  }

  if (ePin == static_cast<int>(kMatrixClockPin)
      || ePin == static_cast<int>(kMatrixLatchPin)
      || ePin == static_cast<int>(kMatrixOePin)) {
    Serial.printf(
        "Pinout HUB75 invalido: linha E=%d conflita com CLK/LAT/OE.\n",
        ePin);
    return false;
  }

  return true;
}

void logMatrixPinout() {
  Serial.printf(
      "HUB75 pinout RGB={%u,%u,%u,%u,%u,%u} ADDR={%u,%u,%u,%u,%u} LAT=%u OE=%u CLK=%u\n",
      static_cast<unsigned>(kMatrixRgbPins[0]),
      static_cast<unsigned>(kMatrixRgbPins[1]),
      static_cast<unsigned>(kMatrixRgbPins[2]),
      static_cast<unsigned>(kMatrixRgbPins[3]),
      static_cast<unsigned>(kMatrixRgbPins[4]),
      static_cast<unsigned>(kMatrixRgbPins[5]),
      static_cast<unsigned>(kMatrixAddrPins[0]),
      static_cast<unsigned>(kMatrixAddrPins[1]),
      static_cast<unsigned>(kMatrixAddrPins[2]),
      static_cast<unsigned>(kMatrixAddrPins[3]),
      static_cast<unsigned>(kMatrixAddrPins[4]),
      static_cast<unsigned>(kMatrixLatchPin),
      static_cast<unsigned>(kMatrixOePin),
      static_cast<unsigned>(kMatrixClockPin));
}

// ---------------------------------------------------------------------------
// Matrix initialization
// ---------------------------------------------------------------------------
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#pontos-de-alteracao-frequente
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-128x64-single-canvas-mapping
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-anti-flicker-com-double-buffer
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-upstream-baseline-fluidity-recovery
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-60-fps-com-pacing-fisico-correto
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-diagnostic-matrix-envs
bool initMatrixDisplay() {

#if defined(MICA_PROFILE_DMA_EXP)
  if (!validateMatrixPinConfiguration()) {
    return false;
  }

  logMatrixPinout();

  HUB75_I2S_CFG::i2s_pins pinMap = {
      static_cast<int8_t>(kMatrixRgbPins[0]),
      static_cast<int8_t>(kMatrixRgbPins[1]),
      static_cast<int8_t>(kMatrixRgbPins[2]),
      static_cast<int8_t>(kMatrixRgbPins[3]),
      static_cast<int8_t>(kMatrixRgbPins[4]),
      static_cast<int8_t>(kMatrixRgbPins[5]),
      static_cast<int8_t>(kMatrixAddrPins[0]),
      static_cast<int8_t>(kMatrixAddrPins[1]),
      static_cast<int8_t>(kMatrixAddrPins[2]),
      static_cast<int8_t>(kMatrixAddrPins[3]),
      static_cast<int8_t>(kMatrixAddrPins[4]),
      static_cast<int8_t>(kMatrixLatchPin),
      static_cast<int8_t>(kMatrixOePin),
      static_cast<int8_t>(kMatrixClockPin)};

  HUB75_I2S_CFG config(kMatrixWidth, kMatrixHeight, 1, pinMap);
  config.double_buff = true;
  config.i2sspeed = HUB75_I2S_CFG::HZ_10M;
  config.clkphase = kHub75ClockPhaseEnabled;
  config.driver = kHub75BaselineDriver;
  config.latch_blanking = kHub75LatchBlankingPulses;
  config.min_refresh_rate = kHub75MinRefreshRate;
  config.setPixelColorDepthBits(kHub75ColorDepthBits);
  Serial.printf(
      "[hub75] config matrix=%ux%u driver=%s color_depth=%u i2s=10MHz clkphase=%u double_buffer=%u latch_blanking=%u min_refresh_rate=%u\n",
      static_cast<unsigned>(kMatrixWidth),
      static_cast<unsigned>(kMatrixHeight),
      hub75DriverName(config.driver),
      static_cast<unsigned>(config.getPixelColorDepthBits()),
      config.clkphase ? 1u : 0u,
      config.double_buff ? 1u : 0u,
      static_cast<unsigned>(config.latch_blanking),
      static_cast<unsigned>(config.min_refresh_rate));

  gMatrix = new MatrixPanel_I2S_DMA(config);
  if (gMatrix == nullptr) {
    Serial.println("Falha ao alocar MatrixPanel_I2S_DMA.");
    return false;
  }

  if (!gMatrix->begin()) {
    Serial.println("Falha ao inicializar MatrixPanel_I2S_DMA.");
    delete gMatrix;
    gMatrix = nullptr;
    return false;
  }

  const uint8_t effectiveLatchBlanking = gMatrix->setLatBlanking(kHub75LatchBlankingPulses);
  Serial.printf(
      "[hub75] active driver=%s calculated_refresh_rate=%dHz latch_blanking=%u physical_present_interval_us=%lu target_present_interval_us=%lu effective_present_interval_us=%lu clkphase=%u double_buffer=%u min_refresh_rate=%u\n",
      hub75DriverName(config.driver),
      gMatrix->calculated_refresh_rate,
      static_cast<unsigned>(effectiveLatchBlanking),
      static_cast<unsigned long>(getPhysicalPresentIntervalUs()),
      static_cast<unsigned long>(kHub75TargetPresentIntervalUs),
      static_cast<unsigned long>(getEffectiveMatrixPresentIntervalUs()),
      config.clkphase ? 1u : 0u,
      config.double_buff ? 1u : 0u,
      static_cast<unsigned>(config.min_refresh_rate));
#if defined(MICA_HUB75_DIAGNOSTIC_MODE)
  Serial.println("[hub75] diagnostic_mode=1 oracle_compare=shiftreg_vs_fm6124");
#endif
#endif

  gMatrixReady = true;
  gAppliedBrightness = 0;
  resetMatrixShadowState();
  setMatrixBrightness(resolveAppliedBrightness());
  updateTestLedDutyFromBrightness(gAppliedBrightness);
  clearMatrix();
  (void)commitMatrixFrame();
  clearMatrix();
  (void)commitMatrixFrame();
  gLastMatrixPresentUs = 0;
  gHub75PresentFrames = 0;
  return true;
}

void clearTestLed() {
  if (!isTestLedAvailable()) {
    return;
  }

  gTestLedState = false;
  gTestLedUntilMs = 0;
  gTestLedNextToggleMs = 0;
  applyTestLedState();
}

void triggerTestLed() {
  if (!isTestLedAvailable()) {
    return;
  }

  gTestLedState = false;
  gTestLedPulseDuty = resolveAppliedBrightness();
  gTestLedUntilMs = millis() + kTestLedDurationMs;
  gTestLedNextToggleMs = 0;
  applyTestLedState();
}

void updateTestLed() {
  if (!isTestLedAvailable() || gTestLedUntilMs == 0) {
    return;
  }

  unsigned long now = millis();
  if (now >= gTestLedUntilMs) {
    clearTestLed();
    return;
  }

  if (gTestLedNextToggleMs == 0 || now >= gTestLedNextToggleMs) {
    gTestLedState = !gTestLedState;
    applyTestLedState();
    gTestLedNextToggleMs = now + kTestLedTogglePeriodMs;
  }
}
