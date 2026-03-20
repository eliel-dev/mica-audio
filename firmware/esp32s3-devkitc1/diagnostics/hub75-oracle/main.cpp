#include <Arduino.h>
#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>

// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-diagnostic-matrix-envs
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-upstream-baseline-fluidity-recovery

#ifndef MICA_HUB75_LATCH_BLANKING
#define MICA_HUB75_LATCH_BLANKING 2
#endif

#ifndef MICA_HUB75_MIN_REFRESH_RATE
#define MICA_HUB75_MIN_REFRESH_RATE 60
#endif

#ifndef MICA_HUB75_CLKPHASE
#define MICA_HUB75_CLKPHASE 0
#endif

#ifndef MICA_HUB75_SHIFT_DRIVER
#define MICA_HUB75_SHIFT_DRIVER 0
#endif

static_assert(MICA_HUB75_LATCH_BLANKING >= 1 && MICA_HUB75_LATCH_BLANKING <= 4, "Latch blanking must stay between 1 and 4.");
static_assert(MICA_HUB75_MIN_REFRESH_RATE >= 1 && MICA_HUB75_MIN_REFRESH_RATE <= 240, "Min refresh rate must stay between 1 and 240.");
static_assert(MICA_HUB75_CLKPHASE == 0 || MICA_HUB75_CLKPHASE == 1, "Clock phase must be 0 or 1.");
static_assert(MICA_HUB75_SHIFT_DRIVER == 0 || MICA_HUB75_SHIFT_DRIVER == 1, "Shift driver must be 0 (SHIFTREG) or 1 (FM6124).");

namespace {
constexpr uint8_t kMatrixWidth = 128;
constexpr uint8_t kMatrixHeight = 64;
constexpr uint8_t kHub75ColorDepthBits = 6;
constexpr uint8_t kHub75Brightness = 96;
constexpr uint8_t kHub75LatchBlankingPulses = static_cast<uint8_t>(MICA_HUB75_LATCH_BLANKING);
constexpr uint8_t kHub75MinRefreshRate = static_cast<uint8_t>(MICA_HUB75_MIN_REFRESH_RATE);
constexpr bool kHub75ClockPhaseEnabled = MICA_HUB75_CLKPHASE != 0;
constexpr unsigned long kPatternHoldMs = 3000UL;
constexpr uint8_t kMatrixRgbPins[6] = {4, 5, 6, 7, 15, 16};
constexpr uint8_t kMatrixAddrPins[5] = {18, 8, 3, 42, 17};
constexpr uint8_t kMatrixClockPin = 41;
constexpr uint8_t kMatrixLatchPin = 40;
constexpr uint8_t kMatrixOePin = 2;

#if MICA_HUB75_SHIFT_DRIVER == 1
constexpr HUB75_I2S_CFG::shift_driver kHub75Driver = HUB75_I2S_CFG::FM6124;
#else
constexpr HUB75_I2S_CFG::shift_driver kHub75Driver = HUB75_I2S_CFG::SHIFTREG;
#endif

MatrixPanel_I2S_DMA* gMatrix = nullptr;
uint8_t gPatternIndex = 0;
unsigned long gLastPatternSwitchMs = 0;

const char* hub75DriverName(HUB75_I2S_CFG::shift_driver driver) {
  switch (driver) {
    case HUB75_I2S_CFG::SHIFTREG:
      return "SHIFTREG";
    case HUB75_I2S_CFG::FM6124:
      return "FM6124";
    default:
      return "OTHER";
  }
}

uint16_t rgb565(uint8_t r, uint8_t g, uint8_t b) {
  return gMatrix->color565(r, g, b);
}

void drawCheckerboard(uint16_t primary, uint16_t secondary) {
  for (uint16_t y = 0; y < kMatrixHeight; y++) {
    for (uint16_t x = 0; x < kMatrixWidth; x++) {
      const bool usePrimary = (((x / 4u) + (y / 4u)) % 2u) == 0u;
      gMatrix->drawPixel(x, y, usePrimary ? primary : secondary);
    }
  }
}

void drawPattern(uint8_t patternIndex) {
  const uint16_t black = rgb565(0, 0, 0);
  const uint16_t white = rgb565(255, 255, 255);
  const uint16_t red = rgb565(255, 32, 32);
  const uint16_t green = rgb565(32, 255, 64);
  const uint16_t blue = rgb565(32, 96, 255);
  const uint16_t cyan = rgb565(32, 255, 255);
  const uint16_t magenta = rgb565(255, 32, 255);
  const uint16_t yellow = rgb565(255, 255, 32);

  gMatrix->fillScreen(black);
  switch (patternIndex % 5u) {
    case 0:
      gMatrix->drawRect(0, 0, kMatrixWidth, kMatrixHeight, white);
      gMatrix->drawLine(0, 0, kMatrixWidth - 1, kMatrixHeight - 1, red);
      gMatrix->drawLine(kMatrixWidth - 1, 0, 0, kMatrixHeight - 1, green);
      gMatrix->drawFastHLine(0, kMatrixHeight / 2, kMatrixWidth, blue);
      gMatrix->drawFastVLine(kMatrixWidth / 2, 0, kMatrixHeight, yellow);
      break;
    case 1:
      gMatrix->fillRect(0, 0, kMatrixWidth / 2, kMatrixHeight / 2, red);
      gMatrix->fillRect(kMatrixWidth / 2, 0, kMatrixWidth / 2, kMatrixHeight / 2, green);
      gMatrix->fillRect(0, kMatrixHeight / 2, kMatrixWidth / 2, kMatrixHeight / 2, blue);
      gMatrix->fillRect(kMatrixWidth / 2, kMatrixHeight / 2, kMatrixWidth / 2, kMatrixHeight / 2, white);
      break;
    case 2:
      drawCheckerboard(cyan, black);
      gMatrix->drawRect(2, 2, kMatrixWidth - 4, kMatrixHeight - 4, magenta);
      break;
    case 3:
      gMatrix->fillCircle(24, 20, 12, blue);
      gMatrix->fillCircle(64, 32, 14, red);
      gMatrix->fillCircle(104, 44, 10, green);
      gMatrix->drawCircle(64, 32, 28, white);
      break;
    default:
      for (uint16_t x = 0; x < kMatrixWidth; x++) {
        uint8_t r = 0;
        uint8_t g = 0;
        uint8_t b = 0;
        if (x < 22) {
          r = 255;
        } else if (x < 44) {
          r = 255;
          g = 128;
        } else if (x < 66) {
          g = 255;
        } else if (x < 88) {
          g = 255;
          b = 255;
        } else if (x < 110) {
          b = 255;
        } else {
          r = 255;
          b = 255;
        }

        gMatrix->drawFastVLine(x, 0, kMatrixHeight, rgb565(r, g, b));
      }
      break;
  }
}

void presentCurrentPattern() {
  drawPattern(gPatternIndex);
  gMatrix->flipDMABuffer();
  Serial.printf("[hub75-oracle] pattern=%u driver=%s\n", static_cast<unsigned>(gPatternIndex), hub75DriverName(kHub75Driver));
}

bool initMatrixDisplay() {
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
  config.driver = kHub75Driver;
  config.latch_blanking = kHub75LatchBlankingPulses;
  config.min_refresh_rate = kHub75MinRefreshRate;
  config.setPixelColorDepthBits(kHub75ColorDepthBits);
  Serial.printf(
      "[hub75-oracle] config matrix=%ux%u driver=%s color_depth=%u i2s=10MHz clkphase=%u double_buffer=%u latch_blanking=%u min_refresh_rate=%u\n",
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
    Serial.println("[hub75-oracle] failed to allocate matrix.");
    return false;
  }

  if (!gMatrix->begin()) {
    Serial.println("[hub75-oracle] begin() failed.");
    delete gMatrix;
    gMatrix = nullptr;
    return false;
  }

  const uint8_t effectiveLatchBlanking = gMatrix->setLatBlanking(kHub75LatchBlankingPulses);
  gMatrix->setBrightness8(kHub75Brightness);
  Serial.printf(
      "[hub75-oracle] active driver=%s calculated_refresh_rate=%dHz latch_blanking=%u clkphase=%u double_buffer=%u min_refresh_rate=%u\n",
      hub75DriverName(config.driver),
      gMatrix->calculated_refresh_rate,
      static_cast<unsigned>(effectiveLatchBlanking),
      config.clkphase ? 1u : 0u,
      config.double_buff ? 1u : 0u,
      static_cast<unsigned>(config.min_refresh_rate));
  return true;
}
}  // namespace

void setup() {
  Serial.begin(115200);
  Serial.println("[hub75-oracle] boot");
  if (!initMatrixDisplay()) {
    return;
  }

  presentCurrentPattern();
  gLastPatternSwitchMs = millis();
}

void loop() {
  if (gMatrix == nullptr) {
    delay(100);
    return;
  }

  const unsigned long now = millis();
  if ((now - gLastPatternSwitchMs) < kPatternHoldMs) {
    delay(1);
    return;
  }

  gLastPatternSwitchMs = now;
  gPatternIndex = static_cast<uint8_t>((gPatternIndex + 1u) % 5u);
  presentCurrentPattern();
}
