#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---ap-first-com-hub75-adiado-no-boot-limpo

#include <Arduino.h>

struct PrefReadSummary {
  uint16_t missingCount = 0;
};

String prefsGetStringOrDefault(const char* key, const String& defaultValue, PrefReadSummary* summary = nullptr);
bool prefsGetBoolOrDefault(const char* key, bool defaultValue, PrefReadSummary* summary = nullptr);
uint8_t prefsGetUCharOrDefault(const char* key, uint8_t defaultValue, PrefReadSummary* summary = nullptr);
uint16_t prefsGetPortOrDefault(const char* key, uint16_t defaultValue, PrefReadSummary* summary = nullptr);
void logPrefsMissingSummary(const char* scope, const PrefReadSummary& summary);
