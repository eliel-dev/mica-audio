#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao

#include <ArduinoJson.h>

void handleControlCommandMessage(const JsonDocument& control);
