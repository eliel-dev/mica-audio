#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao

#include "mica_types.h"
#include <stdint.h>

// ---------------------------------------------------------------------------
// Palette / style decode
// ---------------------------------------------------------------------------
Hub75BinsVisualStyle decodeBinsVisualStyle(uint8_t flags);
Hub75BinsPaletteFamily decodeBinsPaletteFamily(uint8_t flags);
Hub75BinsPaletteFamily resolveBinsEffectivePalette(Hub75BinsVisualStyle style, Hub75BinsPaletteFamily requestedPalette);
RgbColor samplePaletteColor(Hub75BinsPaletteFamily palette, float t);

// ---------------------------------------------------------------------------
// Bins sampling helpers
// ---------------------------------------------------------------------------
uint8_t smoothBinsSample(const uint8_t* bins, int index);
uint8_t amplitudeToHeight(uint8_t amplitude, uint8_t maxHeight);
uint8_t sampleBinsAverage(const uint8_t* bins, uint16_t startInclusive, uint16_t endExclusive);
void resetBinsVisualState();
bool finishBinsVisualFrame();

// ---------------------------------------------------------------------------
// Visual renderers
// ---------------------------------------------------------------------------
bool drawBars();
bool drawWaveMirrorVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette);
bool drawMirrorLinesVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette);
bool drawMirrorBlocksVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette);
bool drawClassicBarsVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette);
bool drawFlowLineVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette);
bool drawHistoryScanVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette);
bool drawRadialOrbitVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette);
bool drawAtmosphereVisual(const uint8_t* bins, Hub75BinsPaletteFamily palette, uint8_t level);
bool drawLaunchpadGridVisual(const uint8_t* bins);

// ---------------------------------------------------------------------------
// Dispatcher + frame render
// ---------------------------------------------------------------------------
bool drawBinsVisual();
bool drawFrame128x64();
