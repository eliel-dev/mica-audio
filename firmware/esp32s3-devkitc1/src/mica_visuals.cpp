#include "mica_visuals.h"
#include "mica_display.h"
#include "mica_globals.h"
#include <Arduino.h>
#include <freertos/semphr.h>
#include <math.h>
#include <string.h>

// ---------------------------------------------------------------------------
// Palette / style decode
// ---------------------------------------------------------------------------
Hub75BinsVisualStyle decodeBinsVisualStyle(uint8_t flags) {
  switch (flags >> 3u) {
    case 1u:
      return Hub75BinsVisualStyle::WaveMirror;
    case 2u:
      return Hub75BinsVisualStyle::MirrorLines;
    case 3u:
      return Hub75BinsVisualStyle::MirrorBlocks;
    case 4u:
      return Hub75BinsVisualStyle::ClassicBars;
    case 5u:
      return Hub75BinsVisualStyle::FlowLine;
    case 6u:
      return Hub75BinsVisualStyle::HistoryScan;
    case 7u:
      return Hub75BinsVisualStyle::RadialOrbit;
    case 8u:
      return Hub75BinsVisualStyle::Atmosphere;
    case 9u:
      return Hub75BinsVisualStyle::LaunchpadGrid;
    default:
      return Hub75BinsVisualStyle::LegacyFallback;
  }
}

Hub75BinsPaletteFamily decodeBinsPaletteFamily(uint8_t flags) {
  switch (flags & 0x07u) {
    case 1u:
      return Hub75BinsPaletteFamily::Rainbow;
    case 2u:
      return Hub75BinsPaletteFamily::Sunset;
    case 3u:
      return Hub75BinsPaletteFamily::Arctic;
    case 4u:
      return Hub75BinsPaletteFamily::Neon;
    case 5u:
      return Hub75BinsPaletteFamily::Aurora;
    case 6u:
      return Hub75BinsPaletteFamily::Plasma;
    case 7u:
      return Hub75BinsPaletteFamily::Mono;
    default:
      return Hub75BinsPaletteFamily::Canonical;
  }
}

Hub75BinsPaletteFamily resolveBinsEffectivePalette(Hub75BinsVisualStyle style, Hub75BinsPaletteFamily requestedPalette) {
  if (requestedPalette != Hub75BinsPaletteFamily::Canonical) {
    return requestedPalette;
  }

  switch (style) {
    case Hub75BinsVisualStyle::WaveMirror:
      return Hub75BinsPaletteFamily::Rainbow;
    case Hub75BinsVisualStyle::RadialOrbit:
      return Hub75BinsPaletteFamily::Mono;
    case Hub75BinsVisualStyle::Atmosphere:
      return Hub75BinsPaletteFamily::Aurora;
    case Hub75BinsVisualStyle::LaunchpadGrid:
      return Hub75BinsPaletteFamily::Canonical;
    default:
      return Hub75BinsPaletteFamily::Rainbow;
  }
}

RgbColor samplePaletteColor(Hub75BinsPaletteFamily palette, float t) {
  static constexpr RgbColor kSunsetStops[] = {
      {255, 72, 40},
      {255, 132, 32},
      {255, 188, 48},
      {220, 82, 196},
      {98, 54, 255}};
  static constexpr RgbColor kArcticStops[] = {
      {24, 214, 255},
      {0, 168, 255},
      {74, 224, 255},
      {138, 255, 214},
      {114, 92, 255}};
  static constexpr RgbColor kNeonStops[] = {
      {57, 255, 20},
      {0, 255, 180},
      {0, 220, 255},
      {120, 86, 255},
      {255, 60, 180}};
  static constexpr RgbColor kAuroraStops[] = {
      {48, 255, 170},
      {0, 222, 255},
      {88, 124, 255},
      {198, 84, 255},
      {255, 176, 226}};
  static constexpr RgbColor kPlasmaStops[] = {
      {255, 88, 46},
      {255, 168, 26},
      {255, 58, 168},
      {64, 108, 255},
      {72, 244, 255}};
  static constexpr RgbColor kMonoStops[] = {
      {88, 98, 118},
      {180, 192, 214},
      {255, 255, 255}};

  switch (palette) {
    case Hub75BinsPaletteFamily::Sunset:
      return sampleGradientStops(kSunsetStops, sizeof(kSunsetStops) / sizeof(kSunsetStops[0]), t);
    case Hub75BinsPaletteFamily::Arctic:
      return sampleGradientStops(kArcticStops, sizeof(kArcticStops) / sizeof(kArcticStops[0]), t);
    case Hub75BinsPaletteFamily::Neon:
      return sampleGradientStops(kNeonStops, sizeof(kNeonStops) / sizeof(kNeonStops[0]), t);
    case Hub75BinsPaletteFamily::Aurora:
      return sampleGradientStops(kAuroraStops, sizeof(kAuroraStops) / sizeof(kAuroraStops[0]), t);
    case Hub75BinsPaletteFamily::Plasma:
      return sampleGradientStops(kPlasmaStops, sizeof(kPlasmaStops) / sizeof(kPlasmaStops[0]), t);
    case Hub75BinsPaletteFamily::Mono:
      return sampleGradientStops(kMonoStops, sizeof(kMonoStops) / sizeof(kMonoStops[0]), t);
    case Hub75BinsPaletteFamily::Canonical:
      return {255, 255, 255};
    case Hub75BinsPaletteFamily::Rainbow:
    default:
      return rainbowColorForColumn(
          static_cast<uint16_t>(clampToByte(static_cast<int>(roundf(clamp01f(t) * 255.0f)))),
          256);
  }
}

// ---------------------------------------------------------------------------
// Bins sampling helpers
// ---------------------------------------------------------------------------
uint8_t smoothBinsSample(const uint8_t* bins, int index) {
  const int clampedIndex = index < 0 ? 0 : (index >= kBinsCount ? kBinsCount - 1 : index);
  const int leftIndex = clampedIndex > 0 ? clampedIndex - 1 : clampedIndex;
  const int rightIndex = clampedIndex + 1 < kBinsCount ? clampedIndex + 1 : clampedIndex;
  const int smoothed =
      static_cast<int>(bins[leftIndex]) + (static_cast<int>(bins[clampedIndex]) * 2) + static_cast<int>(bins[rightIndex]);
  return static_cast<uint8_t>(smoothed / 4);
}

uint8_t amplitudeToHeight(uint8_t amplitude, uint8_t maxHeight) {
  return static_cast<uint8_t>((static_cast<uint16_t>(amplitude) * maxHeight + 254u) / 255u);
}

uint8_t sampleBinsAverage(const uint8_t* bins, uint16_t startInclusive, uint16_t endExclusive) {
  if (startInclusive >= endExclusive || startInclusive >= kBinsCount) {
    return 0;
  }

  const uint16_t safeEnd = endExclusive > kBinsCount ? kBinsCount : endExclusive;
  uint32_t sum = 0;
  for (uint16_t index = startInclusive; index < safeEnd; index++) {
    sum += bins[index];
  }

  return static_cast<uint8_t>(sum / static_cast<uint32_t>(safeEnd - startInclusive));
}

void resetBinsVisualState() {
  memset(gBinsPeakHeights, 0, sizeof(gBinsPeakHeights));
  memset(gBinsHistory, 0, sizeof(gBinsHistory));
  gBinsHistoryHead = 0;
  memset(gLaunchpadPadLevels, 0, sizeof(gLaunchpadPadLevels));
  memset(gLaunchpadTopLevels, 0, sizeof(gLaunchpadTopLevels));
  memset(gLaunchpadSideLevels, 0, sizeof(gLaunchpadSideLevels));
  for (uint8_t bufferIndex = 0; bufferIndex < kMatrixShadowBufferCount; bufferIndex++) {
    memset(gMatrixShadowBarHeights[bufferIndex], 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
    if (gMatrixBufferModes[bufferIndex] == MatrixBufferMode::Bars) {
      gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Unknown;
    }
  }
}

bool finishBinsVisualFrame() {
  gMatrixBufferModes[gMatrixShadowBackBufferIndex] = MatrixBufferMode::Bars;
  return commitMatrixFrame();
}

// ---------------------------------------------------------------------------
// Visual renderers
// ---------------------------------------------------------------------------
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
bool drawBars() {
  if (!gMatrixReady) {
    return false;
  }

  uint8_t binsSnapshot[kBinsCount];
  uint8_t levelSnapshot = 0;
  uint8_t streamBrightnessSnapshot = 0;
  {
    portENTER_CRITICAL(&gStreamBufferMux);
    const uint8_t index = gBinsActiveIndex;
    memcpy(binsSnapshot, gBinsBuffers[index], sizeof(binsSnapshot));
    levelSnapshot = gLevel;
    streamBrightnessSnapshot = gStreamBrightness;
    portEXIT_CRITICAL(&gStreamBufferMux);
  }

  const uint8_t bufferIndex = gMatrixShadowBackBufferIndex;
  uint8_t* renderedHeights = gMatrixShadowBarHeights[bufferIndex];
  if (gMatrixBufferModes[bufferIndex] != MatrixBufferMode::Bars) {
    clearMatrix();
    memset(renderedHeights, 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
  }

  const uint16_t columnCount = (kBinsCount < kMatrixWidth) ? kBinsCount : kMatrixWidth;
  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    uint8_t targetHeight = 0;
    RgbColor targetColor = {0, 0, 0};
    if (x < columnCount) {
      const uint16_t binIndex = (x * kBinsCount) / columnCount;
      const uint8_t amplitude = binsSnapshot[binIndex];
      targetHeight =
          static_cast<uint8_t>((static_cast<uint16_t>(amplitude) * kMatrixHalfHeight + 254u) / 255u);
      targetColor = rainbowColorForColumn(x, columnCount);
    }

    const uint8_t previousHeight = renderedHeights[x];
    if (targetHeight > previousHeight) {
      const uint8_t deltaHeight = static_cast<uint8_t>(targetHeight - previousHeight);
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight - targetHeight),
          1,
          deltaHeight,
          targetColor);
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight + previousHeight),
          1,
          deltaHeight,
          targetColor);
    } else if (targetHeight < previousHeight) {
      const uint8_t deltaHeight = static_cast<uint8_t>(previousHeight - targetHeight);
      const RgbColor black = {0, 0, 0};
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight - previousHeight),
          1,
          deltaHeight,
          black);
      fillMatrixRect(
          static_cast<int16_t>(x),
          static_cast<int16_t>(kMatrixHalfHeight + targetHeight),
          1,
          deltaHeight,
          black);
    }

    renderedHeights[x] = targetHeight;
  }

  gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Bars;
  return commitMatrixFrame();
}

bool drawWaveMirrorVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::WaveMirror, palette);
  const int16_t midY = kMatrixHalfHeight;
  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    const RgbColor centerColor = samplePaletteColor(effectivePalette, x / static_cast<float>(kMatrixWidth - 1u));
    drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(midY), scaleColor(centerColor, 0.70f));
  }

  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    const uint8_t amplitude = smoothBinsSample(bins, static_cast<int>(x));
    const uint8_t halfHeight = amplitudeToHeight(amplitude, static_cast<uint8_t>(kMatrixHalfHeight - 1));
    const RgbColor color = samplePaletteColor(effectivePalette, x / static_cast<float>(kMatrixWidth - 1u));
    const RgbColor glow = scaleColor(color, 0.42f);
    for (uint8_t offset = 0; offset <= halfHeight; offset++) {
      const int16_t yTop = midY - offset;
      const int16_t yBottom = midY + offset;
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(yTop), color);
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(yBottom), color);
      if (offset > 0 && x > 0) {
        drawMatrixPixel(static_cast<uint8_t>(x - 1), static_cast<uint8_t>(yTop), glow);
        drawMatrixPixel(static_cast<uint8_t>(x - 1), static_cast<uint8_t>(yBottom), glow);
      }
      if (offset > 0 && x + 1u < kMatrixWidth) {
        drawMatrixPixel(static_cast<uint8_t>(x + 1), static_cast<uint8_t>(yTop), glow);
        drawMatrixPixel(static_cast<uint8_t>(x + 1), static_cast<uint8_t>(yBottom), glow);
      }
    }
  }

  return finishBinsVisualFrame();
}

bool drawMirrorLinesVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::MirrorLines, palette);
  const int16_t midY = kMatrixHalfHeight;
  for (uint16_t x = 0; x < kMatrixWidth; x++) {
    const uint8_t amplitude = smoothBinsSample(bins, static_cast<int>(x));
    const uint8_t halfHeight = amplitudeToHeight(amplitude, static_cast<uint8_t>(kMatrixHalfHeight - 1));
    const RgbColor color = samplePaletteColor(effectivePalette, x / static_cast<float>(kMatrixWidth - 1u));
    for (uint8_t offset = 0; offset < halfHeight; offset++) {
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(midY - offset), color);
      drawMatrixPixel(static_cast<uint8_t>(x), static_cast<uint8_t>(midY + offset), color);
    }
  }

  return finishBinsVisualFrame();
}

bool drawMirrorBlocksVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::MirrorBlocks, palette);
  const int16_t horizon = static_cast<int16_t>(kMatrixHeight * 0.62f);
  constexpr uint16_t kBlockCount = 64;
  for (uint16_t block = 0; block < kBlockCount; block++) {
    const uint16_t x = block * 2u;
    const uint8_t amplitude = sampleBinsAverage(bins, block * 2u, (block * 2u) + 2u);
    const uint8_t topHeight = amplitudeToHeight(amplitude, static_cast<uint8_t>(horizon));
    const uint8_t reflectionHeight = static_cast<uint8_t>(topHeight * 0.45f);
    const RgbColor color = samplePaletteColor(effectivePalette, block / static_cast<float>(kBlockCount - 1u));
    fillMatrixRect(static_cast<int16_t>(x), horizon - topHeight, 2, topHeight, color);
    fillMatrixRect(static_cast<int16_t>(x), horizon, 2, reflectionHeight, scaleColor(color, 0.38f));
  }

  return finishBinsVisualFrame();
}

bool drawClassicBarsVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::ClassicBars, palette);
  constexpr uint16_t kBarCount = 64;
  for (uint16_t bar = 0; bar < kBarCount; bar++) {
    const uint16_t x = bar * 2u;
    const uint8_t amplitude = sampleBinsAverage(bins, bar * 2u, (bar * 2u) + 2u);
    const uint8_t height = amplitudeToHeight(amplitude, kMatrixHeight - 1u);
    const uint8_t previousPeak = gBinsPeakHeights[bar];
    const uint8_t peak = height > previousPeak ? height : (previousPeak > 0 ? previousPeak - 1u : 0u);
    gBinsPeakHeights[bar] = peak;
    const RgbColor color = samplePaletteColor(effectivePalette, bar / static_cast<float>(kBarCount - 1u));
    fillMatrixRect(static_cast<int16_t>(x), static_cast<int16_t>(kMatrixHeight - height), 2, height, color);
    if (peak > 0) {
      fillMatrixRect(static_cast<int16_t>(x), static_cast<int16_t>(kMatrixHeight - peak), 2, 1, scaleColor(color, 0.95f));
    }
  }

  return finishBinsVisualFrame();
}

bool drawFlowLineVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::FlowLine, palette);
  constexpr uint16_t kSampleCount = 64;
  int16_t previousX = 0;
  int16_t previousY = static_cast<int16_t>(kMatrixHeight - amplitudeToHeight(sampleBinsAverage(bins, 0, 2), kMatrixHeight - 1u));
  for (uint16_t sample = 0; sample < kSampleCount; sample++) {
    const uint16_t x = sample * 2u;
    const uint8_t amplitude = sampleBinsAverage(bins, sample * 2u, (sample * 2u) + 2u);
    const int16_t y = static_cast<int16_t>(kMatrixHeight - amplitudeToHeight(amplitude, kMatrixHeight - 2u));
    const RgbColor color = samplePaletteColor(effectivePalette, sample / static_cast<float>(kSampleCount - 1u));
    gMatrix->drawLine(previousX, previousY, x, y, rgb888ToRgb565(color.r, color.g, color.b));
    if ((sample & 0x01u) == 0u) {
      fillMatrixRect(static_cast<int16_t>(x), y, 2, static_cast<int16_t>(kMatrixHeight - y), scaleColor(color, 0.22f));
    }

    previousX = static_cast<int16_t>(x);
    previousY = y;
  }

  return finishBinsVisualFrame();
}

bool drawHistoryScanVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::HistoryScan, palette);
  constexpr uint16_t kColumnCount = 64;
  uint8_t* currentRow = gBinsHistory[gBinsHistoryHead];
  for (uint16_t column = 0; column < kColumnCount; column++) {
    currentRow[column] = sampleBinsAverage(bins, column * 2u, (column * 2u) + 2u);
  }

  gBinsHistoryHead = static_cast<uint8_t>((gBinsHistoryHead + 1u) % kMatrixHeight);

  for (uint16_t row = 0; row < kMatrixHeight; row++) {
    const int historyIndex = (static_cast<int>(gBinsHistoryHead) - 1 - static_cast<int>(row) + kMatrixHeight) % kMatrixHeight;
    const uint8_t* historyRow = gBinsHistory[historyIndex];
    for (uint16_t column = 0; column < kColumnCount; column++) {
      const uint8_t amplitude = historyRow[column];
      if (amplitude < 6u) {
        continue;
      }

      const float paletteT = clamp01f((amplitude / 255.0f) * 0.85f + (column / static_cast<float>(kColumnCount - 1u)) * 0.15f);
      const RgbColor color = samplePaletteColor(effectivePalette, paletteT);
      fillMatrixRect(static_cast<int16_t>(column * 2u), static_cast<int16_t>(kMatrixHeight - 1u - row), 2, 1, color);
    }
  }

  return finishBinsVisualFrame();
}

bool drawRadialOrbitVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::RadialOrbit, palette);
  const int16_t centerX = kMatrixWidth / 2;
  const int16_t centerY = kMatrixHeight / 2;
  constexpr uint16_t kRayCount = 48;
  const float baseRadius = 7.0f;
  const float maxRadius = 28.0f;
  for (uint16_t ray = 0; ray < kRayCount; ray++) {
    const uint16_t binStart = static_cast<uint16_t>((ray * kBinsCount) / kRayCount);
    const uint16_t binEnd = static_cast<uint16_t>(((ray + 1u) * kBinsCount) / kRayCount);
    const uint8_t amplitude = sampleBinsAverage(bins, binStart, binEnd);
    const float angle = (static_cast<float>(ray) / static_cast<float>(kRayCount)) * 6.2831853f - 1.5707963f;
    const float radius = baseRadius + ((amplitude / 255.0f) * maxRadius);
    const int16_t endX = static_cast<int16_t>(roundf(centerX + cosf(angle) * radius));
    const int16_t endY = static_cast<int16_t>(roundf(centerY + sinf(angle) * radius));
    const RgbColor color = samplePaletteColor(effectivePalette, ray / static_cast<float>(kRayCount - 1u));
    gMatrix->drawLine(centerX, centerY, endX, endY, rgb888ToRgb565(color.r, color.g, color.b));
    fillMatrixRect(endX - 1, endY - 1, 2, 2, scaleColor(color, 0.68f));
  }

  fillMatrixRect(centerX - 2, centerY - 2, 4, 4, scaleColor(samplePaletteColor(effectivePalette, 0.5f), 0.72f));
  return finishBinsVisualFrame();
}

bool drawAtmosphereVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette, uint8_t level) {
  clearMatrix();
  const Hub75BinsPaletteFamily effectivePalette = resolveBinsEffectivePalette(Hub75BinsVisualStyle::Atmosphere, palette);
  constexpr uint16_t kSampleCount = 64;
  const float time = millis() / 1000.0f;
  const float globalLevel = level / 255.0f;
  for (uint8_t ribbon = 0; ribbon < 3u; ribbon++) {
    const float baseline = kMatrixHeight * (0.24f + (ribbon * 0.22f));
    const float phase = time * (0.9f + (ribbon * 0.22f));
    int16_t previousX = 0;
    int16_t previousY = static_cast<int16_t>(baseline);
    for (uint16_t sample = 0; sample < kSampleCount; sample++) {
      const uint16_t x = sample * 2u;
      const uint8_t amplitude = sampleBinsAverage(bins, sample * 2u, (sample * 2u) + 2u);
      const float energy = amplitude / 255.0f;
      const float wave = sinf((sample * 0.22f) + phase + ribbon) * (5.0f + (globalLevel * 4.0f));
      const int16_t y = static_cast<int16_t>(roundf(baseline + wave - (energy * 16.0f)));
      const float colorT = clamp01f((sample / static_cast<float>(kSampleCount - 1u)) * 0.65f + (ribbon * 0.18f));
      const RgbColor color = samplePaletteColor(effectivePalette, colorT);
      gMatrix->drawLine(previousX, previousY, x, y, rgb888ToRgb565(color.r, color.g, color.b));
      if ((sample % 6u) == 0u) {
        fillMatrixRect(x - 1, y - 1, 3, 3, scaleColor(color, 0.52f + (energy * 0.30f)));
      }

      previousX = static_cast<int16_t>(x);
      previousY = y;
    }
  }

  const RgbColor bloom = samplePaletteColor(effectivePalette, 0.35f + (globalLevel * 0.25f));
  fillMatrixRect((kMatrixWidth / 2) - 6, (kMatrixHeight / 2) - 4, 12, 8, scaleColor(bloom, 0.30f + (globalLevel * 0.30f)));
  return finishBinsVisualFrame();
}

bool drawLaunchpadGridVisual(const uint8_t* bins) {
  clearMatrix();
  const RgbColor deviceBody = {10, 12, 16};
  const RgbColor padOff = {34, 38, 46};
  const RgbColor topOff = {58, 64, 72};
  fillMatrixRect(18, 4, 92, 56, deviceBody);
  constexpr uint8_t kGridSize = 8;
  constexpr uint8_t kPadWidth = 9;
  constexpr uint8_t kPadHeight = 5;
  constexpr uint8_t kPadGapX = 2;
  constexpr uint8_t kPadGapY = 1;
  constexpr uint8_t kGridLeft = 22;
  constexpr uint8_t kGridTop = 12;
  for (uint8_t row = 0; row < kGridSize; row++) {
    for (uint8_t col = 0; col < kGridSize; col++) {
      const uint8_t padIndex = static_cast<uint8_t>(row * kGridSize + col);
      const uint8_t target = sampleBinsAverage(bins, padIndex * 2u, (padIndex * 2u) + 2u);
      const uint8_t previous = gLaunchpadPadLevels[padIndex];
      const uint8_t levelValue = target > previous ? target : (previous > 14u ? previous - 14u : 0u);
      gLaunchpadPadLevels[padIndex] = levelValue;
      const uint8_t x = kGridLeft + col * (kPadWidth + kPadGapX);
      const uint8_t y = kGridTop + row * (kPadHeight + kPadGapY);
      fillMatrixRect(x, y, kPadWidth, kPadHeight, padOff);
      if (levelValue > 6u) {
        const float colorT = clamp01f((col / 7.0f) * 0.55f + (row / 7.0f) * 0.25f + (levelValue / 255.0f) * 0.20f);
        const RgbColor activeColor = samplePaletteColor(Hub75BinsPaletteFamily::Neon, colorT);
        fillMatrixRect(x + 1, y + 1, kPadWidth - 2, kPadHeight - 2, scaleColor(activeColor, 0.35f + (levelValue / 255.0f) * 0.65f));
      }
    }
  }

  for (uint8_t col = 0; col < kGridSize; col++) {
    const uint8_t target = sampleBinsAverage(bins, col * 4u, (col * 4u) + 4u);
    const uint8_t previous = gLaunchpadTopLevels[col];
    gLaunchpadTopLevels[col] = target > previous ? target : (previous > 18u ? previous - 18u : 0u);
    const uint8_t x = kGridLeft + col * (kPadWidth + kPadGapX) + 2u;
    fillMatrixRect(x, 7, kPadWidth - 4u, 3, topOff);
    if (gLaunchpadTopLevels[col] > 12u) {
      const RgbColor color = samplePaletteColor(Hub75BinsPaletteFamily::Plasma, col / 7.0f);
      fillMatrixRect(x, 7, kPadWidth - 4u, 3, scaleColor(color, 0.28f + (gLaunchpadTopLevels[col] / 255.0f) * 0.72f));
    }
  }

  for (uint8_t row = 0; row < kGridSize; row++) {
    const uint8_t target = sampleBinsAverage(bins, row * 4u, (row * 4u) + 4u);
    const uint8_t previous = gLaunchpadSideLevels[row];
    gLaunchpadSideLevels[row] = target > previous ? target : (previous > 18u ? previous - 18u : 0u);
    const uint8_t y = kGridTop + row * (kPadHeight + kPadGapY) + 1u;
    fillMatrixRect(112, y, 3, kPadHeight - 2u, topOff);
    if (gLaunchpadSideLevels[row] > 12u) {
      const RgbColor color = samplePaletteColor(Hub75BinsPaletteFamily::Aurora, row / 7.0f);
      fillMatrixRect(112, y, 3, kPadHeight - 2u, scaleColor(color, 0.28f + (gLaunchpadSideLevels[row] / 255.0f) * 0.72f));
    }
  }

  return finishBinsVisualFrame();
}

// ---------------------------------------------------------------------------
// Dispatcher + frame render
// ---------------------------------------------------------------------------
bool drawBinsVisual() {
  if (!gMatrixReady) {
    return false;
  }

  uint8_t binsSnapshot[kBinsCount];
  uint8_t levelSnapshot = 0;
  uint8_t flagsSnapshot = 0;
  {
    portENTER_CRITICAL(&gStreamBufferMux);
    const uint8_t index = gBinsActiveIndex;
    memcpy(binsSnapshot, gBinsBuffers[index], sizeof(binsSnapshot));
    levelSnapshot = gLevel;
    flagsSnapshot = gBinsFlags;
    portEXIT_CRITICAL(&gStreamBufferMux);
  }

  const Hub75BinsVisualStyle style = decodeBinsVisualStyle(flagsSnapshot);
  const Hub75BinsPaletteFamily palette = decodeBinsPaletteFamily(flagsSnapshot);
  const uint8_t styleId = static_cast<uint8_t>(style);
  if (styleId != gLastBinsStyleId) {
    resetBinsVisualState();
    gLastBinsStyleId = styleId;
  }

  switch (style) {
    case Hub75BinsVisualStyle::WaveMirror:
      return drawWaveMirrorVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::MirrorLines:
      return drawMirrorLinesVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::MirrorBlocks:
      return drawMirrorBlocksVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::ClassicBars:
      return drawClassicBarsVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::FlowLine:
      return drawFlowLineVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::HistoryScan:
      return drawHistoryScanVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::RadialOrbit:
      return drawRadialOrbitVisual(binsSnapshot, palette);
    case Hub75BinsVisualStyle::Atmosphere:
      return drawAtmosphereVisual(binsSnapshot, palette, levelSnapshot);
    case Hub75BinsVisualStyle::LaunchpadGrid:
      return drawLaunchpadGridVisual(binsSnapshot);
    case Hub75BinsVisualStyle::LegacyFallback:
    default:
      return drawBars();
  }
}

bool drawFrame128x64() {
  if (!gMatrixReady) {
    return false;
  }

  const uint8_t bufferIndex = gMatrixShadowBackBufferIndex;
  const uint8_t frameIndex = gFrameRgb565ActiveIndex;
  const uint16_t* frame = gFrameRgb565Buffers[frameIndex];
  // Bulk RGB565 path writes the full frame directly into the HUB75 BCM back buffer.
  gMatrix->writeFrameRGB565(frame);
  memcpy(gMatrixShadowFrames[bufferIndex], frame, sizeof(gFrameRgb565Buffers[0]));
  memset(gMatrixShadowBarHeights[bufferIndex], 0, sizeof(gMatrixShadowBarHeights[bufferIndex]));
  gMatrixBufferModes[bufferIndex] = MatrixBufferMode::Frame;
  return commitMatrixFrame();
}
