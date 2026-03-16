Import("env")

from pathlib import Path
import re


# DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-03---hub75-bulk-rgb565-back-buffer-fix

PATCH_MARKER = "MICA_HUB75_WRITE_FRAME_RGB565_PATCH"
LIB_ROOT = Path(env.subst("$PROJECT_LIBDEPS_DIR")) / env.subst("$PIOENV") / "ESP32 HUB75 LED MATRIX PANEL DMA Display" / "src"
TARGET_HEADER = LIB_ROOT / "ESP32-HUB75-MatrixPanel-I2S-DMA.h"
TARGET_CPP = LIB_ROOT / "ESP32-HUB75-MatrixPanel-I2S-DMA.cpp"

CPP_INCLUDE_SENTINEL = re.compile(
    r'^(#include "ESP32-HUB75-MatrixPanel-I2S-DMA\.h"\s*)$',
    re.MULTILINE,
)
HEADER_SENTINEL = re.compile(
    r"^(\s*void drawPixelRGB888\(int16_t x, int16_t y, uint8_t r, uint8_t g, uint8_t b\);\s*)$",
    re.MULTILINE,
)
CPP_SENTINEL = re.compile(
    r"(^\}\s*// updateMatrixDMABuffer \(full frame paint\)\s*$)",
    re.MULTILINE,
)
HEADER_LAYOUT_SENTINELS = [
    re.compile(r"inline void flipDMABuffer\(\)\s*\{[\s\S]*?dma_bus\.flip_dma_output_buffer\(back_buffer_id\);[\s\S]*?back_buffer_id\s*=\s*back_buffer_id\^1;[\s\S]*?fb\s*=\s*&frame_buffer\[back_buffer_id\];", re.MULTILINE),
    re.compile(r"frameStruct frame_buffer\[2\];"),
    re.compile(r"frameStruct \*fb;"),
    re.compile(r"volatile int back_buffer_id = 0;"),
]
CPP_LAYOUT_SENTINELS = [
    re.compile(r'#define getRowDataPtr\(row, _dpth\)\s*&\(fb->rowBits\[row\]->data\[_dpth \* fb->rowBits\[row\]->width\]\)'),
    re.compile(r"void MatrixPanel_I2S_DMA::clearFrameBuffer\(bool _buff_id\)"),
]


def ensure_layout(original: str, patterns, file_name: str) -> None:
    for pattern in patterns:
        if not pattern.search(original):
            raise RuntimeError(f"Layout esperado nao encontrado em {file_name}: {pattern.pattern}")


def patch_header(original: str, newline: str) -> str:
    ensure_layout(original, HEADER_LAYOUT_SENTINELS, TARGET_HEADER.name)

    if PATCH_MARKER in original:
        return original

    replacement = (
        r"\1"
        + newline
        + f"  void writeFrameRGB565(const uint16_t *frame565); // {PATCH_MARKER}"
    )
    patched, count = HEADER_SENTINEL.subn(replacement, original, count=1)
    if count != 1:
        raise RuntimeError("Assinatura esperada de drawPixelRGB888 nao encontrada em ESP32-HUB75-MatrixPanel-I2S-DMA.h.")
    return patched


def patch_cpp(original: str, newline: str) -> str:
    ensure_layout(original, CPP_LAYOUT_SENTINELS, TARGET_CPP.name)

    if PATCH_MARKER in original:
        return original

    include_replacement = r"\1" + newline + "#include <Arduino.h> // " + PATCH_MARKER
    patched, include_count = CPP_INCLUDE_SENTINEL.subn(include_replacement, original, count=1)
    if include_count != 1:
        raise RuntimeError("Include esperado de ESP32-HUB75-MatrixPanel-I2S-DMA.h nao encontrado em ESP32-HUB75-MatrixPanel-I2S-DMA.cpp.")

    method = newline.join(
        [
            r"\1",
            "",
            f"void MatrixPanel_I2S_DMA::writeFrameRGB565(const uint16_t *frame565) // {PATCH_MARKER}",
            "{",
            "  if (!initialized || frame565 == nullptr)",
            "    return;",
            "",
            "  frameStruct *target_fb = fb;",
            "  int target_buffer_id = -1;",
            "  if (target_fb == &frame_buffer[0])",
            "    target_buffer_id = 0;",
            "  else if (target_fb == &frame_buffer[1])",
            "    target_buffer_id = 1;",
            "  else",
            "    return;",
            "",
            "  const int observed_back_buffer_id = back_buffer_id;",
            "  const uint8_t colour_depth_count = m_cfg.getPixelColorDepthBits();",
            "  static unsigned long last_audit_log_ms = 0;",
            "  const unsigned long now_ms = millis();",
            "  if ((now_ms - last_audit_log_ms) >= 1000UL)",
            "  {",
            "    last_audit_log_ms = now_ms;",
            "    Serial.printf(\"[hub75-rgb565] target_buffer_id=%d back_buffer_id=%d rows_per_frame=%u pixels_per_row=%u\\\\n\",",
            "                  target_buffer_id,",
            "                  observed_back_buffer_id,",
            "                  static_cast<unsigned>(ROWS_PER_FRAME),",
            "                  static_cast<unsigned>(PIXELS_PER_ROW));",
            "    if (m_cfg.mx_height == 64 && PIXELS_PER_ROW == 128 && ROWS_PER_FRAME != 32)",
            "    {",
            "      Serial.printf(\"[hub75-rgb565] warning expected_rows_per_frame=32 observed=%u\\\\n\",",
            "                    static_cast<unsigned>(ROWS_PER_FRAME));",
            "    }",
            "  }",
            "",
            "  for (uint8_t row_idx = 0; row_idx < ROWS_PER_FRAME; row_idx++)",
            "  {",
            "    const uint16_t *upper_row = frame565 + (static_cast<size_t>(row_idx) * static_cast<size_t>(PIXELS_PER_ROW));",
            "    const uint16_t *lower_row = frame565 + (static_cast<size_t>(row_idx + ROWS_PER_FRAME) * static_cast<size_t>(PIXELS_PER_ROW));",
            "    ESP32_I2S_DMA_STORAGE_TYPE *plane_rows[PIXEL_COLOR_DEPTH_BITS_MAX] = {nullptr};",
            "    const size_t plane_bytes = target_fb->rowBits[row_idx]->getColorDepthSize(true);",
            "",
            "    for (uint8_t colour_depth_idx = 0; colour_depth_idx < colour_depth_count; colour_depth_idx++)",
            "    {",
            "      plane_rows[colour_depth_idx] = target_fb->rowBits[row_idx]->getDataPtr(colour_depth_idx);",
            "    }",
            "",
            "    for (uint16_t x = 0; x < PIXELS_PER_ROW; x++)",
            "    {",
            "      const uint16_t upper_pixel = upper_row[x];",
            "      const uint16_t lower_pixel = lower_row[x];",
            "",
            "      const uint8_t upper_r5 = static_cast<uint8_t>((upper_pixel >> 11) & 0x1FU);",
            "      const uint8_t upper_g6 = static_cast<uint8_t>((upper_pixel >> 5) & 0x3FU);",
            "      const uint8_t upper_b5 = static_cast<uint8_t>(upper_pixel & 0x1FU);",
            "      const uint8_t lower_r5 = static_cast<uint8_t>((lower_pixel >> 11) & 0x1FU);",
            "      const uint8_t lower_g6 = static_cast<uint8_t>((lower_pixel >> 5) & 0x3FU);",
            "      const uint8_t lower_b5 = static_cast<uint8_t>(lower_pixel & 0x1FU);",
            "",
            "      const uint8_t upper_r6 = static_cast<uint8_t>((upper_r5 << 1) | (upper_r5 >> 4));",
            "      const uint8_t upper_b6 = static_cast<uint8_t>((upper_b5 << 1) | (upper_b5 >> 4));",
            "      const uint8_t lower_r6 = static_cast<uint8_t>((lower_r5 << 1) | (lower_r5 >> 4));",
            "      const uint8_t lower_b6 = static_cast<uint8_t>((lower_b5 << 1) | (lower_b5 >> 4));",
            "      const uint16_t adjusted_x = ESP32_TX_FIFO_POSITION_ADJUST(x);",
            "",
            "      for (uint8_t colour_depth_idx = 0; colour_depth_idx < colour_depth_count; colour_depth_idx++)",
            "      {",
            "        const uint8_t bit_mask = static_cast<uint8_t>(1U << colour_depth_idx);",
            "        uint16_t rgb_output_bits = 0;",
            "",
            "        if ((upper_r6 & bit_mask) != 0)",
            "          rgb_output_bits |= BIT_R1;",
            "        if ((upper_g6 & bit_mask) != 0)",
            "          rgb_output_bits |= BIT_G1;",
            "        if ((upper_b6 & bit_mask) != 0)",
            "          rgb_output_bits |= BIT_B1;",
            "        if ((lower_r6 & bit_mask) != 0)",
            "          rgb_output_bits |= BIT_R2;",
            "        if ((lower_g6 & bit_mask) != 0)",
            "          rgb_output_bits |= BIT_G2;",
            "        if ((lower_b6 & bit_mask) != 0)",
            "          rgb_output_bits |= BIT_B2;",
            "",
            "        plane_rows[colour_depth_idx][adjusted_x] &= BITMASK_RGB12_CLEAR;",
            "        plane_rows[colour_depth_idx][adjusted_x] |= rgb_output_bits;",
            "",
            "      }",
            "",
            "#if defined(SPIRAM_DMA_BUFFER)",
            "      Cache_WriteBack_Addr((uint32_t)plane_rows[colour_depth_idx], plane_bytes);",
            "#endif",
            "    }",
            "  }",
            "}",
        ]
    )

    patched, count = CPP_SENTINEL.subn(method, patched, count=1)
    if count != 1:
        raise RuntimeError("Assinatura esperada de updateMatrixDMABuffer(full frame paint) nao encontrada em ESP32-HUB75-MatrixPanel-I2S-DMA.cpp.")
    return patched


def patch_file(path: Path, patcher):
    if not path.exists():
        raise RuntimeError(f"Arquivo alvo nao encontrado para patch HUB75 RGB565: {path}")

    original = path.read_text(encoding="utf-8")
    newline = "\r\n" if "\r\n" in original else "\n"
    patched = patcher(original, newline)
    if patched == original:
        print(f"[mica][hub75-rgb565] sem alteracao: {path}")
        return

    with path.open("w", encoding="utf-8", newline="") as stream:
        stream.write(patched)
    print(f"[mica][hub75-rgb565] patch aplicado: {path}")


patch_file(TARGET_HEADER, patch_header)
patch_file(TARGET_CPP, patch_cpp)
