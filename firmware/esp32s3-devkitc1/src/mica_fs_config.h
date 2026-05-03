#pragma once
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#configuracao-por-arquivo-json-no-fatfs

#include <Arduino.h>

struct FsConfigResult {
  bool loaded = false;
  bool hasWifi = false;
  String wifiSsid;
  String wifiPassword;
  bool hasServer = false;
  String serverHost;
  uint16_t serverPort = 0;
  bool hasDeviceName = false;
  String deviceName;
};

FsConfigResult tryLoadFsConfig();
