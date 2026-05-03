// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#configuracao-por-arquivo-json-no-fatfs

#include "mica_fs_config.h"

#include <FFat.h>
#include <ArduinoJson.h>

#include "mica_globals.h"
#include "mica_network.h"
#include "mica_prefs.h"
#include "mica_types.h"

FsConfigResult tryLoadFsConfig() {
  FsConfigResult result;

  if (!FFat.begin(true)) {
    Serial.println("[fs_config] FFat falhou ao montar.");
    return result;
  }

  File file = FFat.open("/config.json", "r");
  if (!file) {
    Serial.println("[fs_config] /config.json nao encontrado no FATFS.");
    FFat.end();
    return result;
  }

  const size_t size = file.size();
  if (size == 0 || size > 4096) {
    Serial.printf("[fs_config] /config.json tamanho invalido: %u bytes (max 4096).\n", static_cast<unsigned>(size));
    file.close();
    FFat.end();
    return result;
  }

  String content = file.readString();
  file.close();
  FFat.end();

  JsonDocument doc;
  DeserializationError jsonError = deserializeJson(doc, content);
  if (jsonError) {
    Serial.printf("[fs_config] JSON invalido: %s\n", jsonError.c_str());
    return result;
  }

  const char* wifiSsid = doc["wifi_ssid"] | "";
  const char* wifiPassword = doc["wifi_password"] | "";
  if (wifiSsid[0] != '\0') {
    result.hasWifi = true;
    result.wifiSsid = wifiSsid;
    result.wifiPassword = wifiPassword;
    gPrefs.putString("wifiSsid", wifiSsid);
    gPrefs.putString("wifiPassword", wifiPassword);
    gPrefs.putBool("wifiConfigured", true);
    Serial.printf("[fs_config] WiFi configurado via JSON: SSID='%s'\n", wifiSsid);
  } else {
    Serial.println("[fs_config] campo wifi_ssid ausente ou vazio no JSON.");
  }

  const char* serverHost = doc["server_host"] | "";
  const int serverPort = doc["server_port"] | 0;
  if (serverHost[0] != '\0' && serverPort > 0) {
    result.hasServer = true;
    result.serverHost = serverHost;
    result.serverPort = static_cast<uint16_t>(serverPort);
    gServerHost = result.serverHost;
    gServerPort = result.serverPort;
    gPrefs.putString("host", gServerHost);
    gPrefs.putString("port", String(gServerPort));
    gMqttHost = gServerHost;
    gMqttPort = kDefaultMqttPort;
    gMqttRootTopic = kDefaultMqttRootTopic;
    persistMqttConfig();
    Serial.printf("[fs_config] Servidor configurado via JSON: %s:%u\n", serverHost, serverPort);
  } else {
    Serial.println("[fs_config] campos server_host/server_port ausentes ou invalidos no JSON.");
  }

  const char* deviceName = doc["device_name"] | "";
  if (deviceName[0] != '\0') {
    result.hasDeviceName = true;
    result.deviceName = deviceName;
    gPrefs.putString("name", deviceName);
    Serial.printf("[fs_config] Nome do device configurado via JSON: '%s'\n", deviceName);
  }

  result.loaded = true;
  Serial.println("[fs_config] config.json carregado com sucesso do FATFS.");
  return result;
}
