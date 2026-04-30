// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---ap-first-com-hub75-adiado-no-boot-limpo

#include "mica_prefs.h"

#include "mica_globals.h"

static void registerMissingPrefKey(PrefReadSummary* summary) {
  if (summary == nullptr || summary->missingCount == UINT16_MAX) {
    return;
  }

  summary->missingCount++;
}

String prefsGetStringOrDefault(const char* key, const String& defaultValue, PrefReadSummary* summary) {
  if (!gPrefs.isKey(key)) {
    registerMissingPrefKey(summary);
    return defaultValue;
  }

  return gPrefs.getString(key, defaultValue);
}

bool prefsGetBoolOrDefault(const char* key, bool defaultValue, PrefReadSummary* summary) {
  if (!gPrefs.isKey(key)) {
    registerMissingPrefKey(summary);
    return defaultValue;
  }

  return gPrefs.getBool(key, defaultValue);
}

uint8_t prefsGetUCharOrDefault(const char* key, uint8_t defaultValue, PrefReadSummary* summary) {
  if (!gPrefs.isKey(key)) {
    registerMissingPrefKey(summary);
    return defaultValue;
  }

  return gPrefs.getUChar(key, defaultValue);
}

uint16_t prefsGetPortOrDefault(const char* key, uint16_t defaultValue, PrefReadSummary* summary) {
  if (!gPrefs.isKey(key)) {
    registerMissingPrefKey(summary);
    return defaultValue;
  }

  String rawValue = gPrefs.getString(key, String(defaultValue));
  rawValue.trim();
  const long parsedValue = rawValue.toInt();
  if (parsedValue <= 0 || parsedValue > 65535) {
    return defaultValue;
  }

  return static_cast<uint16_t>(parsedValue);
}

void logPrefsMissingSummary(const char* scope, const PrefReadSummary& summary) {
  if (summary.missingCount == 0) {
    return;
  }

  Serial.printf(
      "[prefs] %u chave(s) ausente(s) em %s; usando defaults seguros.\n",
      static_cast<unsigned>(summary.missingCount),
      scope == nullptr ? "-" : scope);
}
