#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---visual-udp-lan-opt-in
// DOCS: docs/wiki/reference/ws-protocol-v2.md#udp-visual-v1
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md

void ensureVisualUdpReceiver();
void stopVisualUdpReceiver();
void processVisualUdpReceiver();
