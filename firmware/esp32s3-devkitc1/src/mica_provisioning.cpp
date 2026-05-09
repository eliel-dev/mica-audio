// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// STA-hardcoded boot. Reads mica_config.h for Wi-Fi/server constants, brings
// up STA, and then auto-registers with MicaAudio.Server when NVS has no
// deviceId/token cached. The /api/v1/auto-register endpoint is idempotent per
// MAC: re-flashing the firmware on the same physical device returns the same
// deviceId and token without disturbing existing pairings.

#include "mica_provisioning.h"

#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <WiFi.h>

#include "mica_commands.h"
#include "mica_config.h"
#include "mica_globals.h"
#include "mica_network.h"
#include "mica_prefs.h"

namespace {

constexpr unsigned long kAutoRegisterRetryBackoffMs = 2000;
constexpr unsigned long kAutoRegisterMaxAttempts = 3;

bool persistDeviceIdentity(const String& deviceId, const String& token) {
  if (deviceId.length() == 0 || token.length() == 0) {
    return false;
  }

  gDeviceId = deviceId;
  gToken = token;
  gPrefs.putString("deviceId", gDeviceId);
  gPrefs.putString("token", gToken);
  return true;
}

void persistMqttCoordinates(const String& mqttHost, int mqttPort, const String& mqttRootTopic) {
  if (mqttHost.length() > 0) {
    gMqttHost = mqttHost;
  }
  else if (gMqttHost.length() == 0) {
    gMqttHost = gServerHost;
  }

  if (mqttPort > 0) {
    gMqttPort = static_cast<uint16_t>(mqttPort);
  }
  else if (gMqttPort == 0) {
    gMqttPort = kDefaultMqttPort;
  }

  if (mqttRootTopic.length() > 0) {
    gMqttRootTopic = mqttRootTopic;
  }
  else if (gMqttRootTopic.length() == 0) {
    gMqttRootTopic = String(kDefaultMqttRootTopic);
  }

  persistMqttConfig();
}

}  // namespace

// ---------------------------------------------------------------------------
// New API
// ---------------------------------------------------------------------------

String getDeviceMacAddressHex() {
  uint8_t mac[6] = {0};
  if (WiFi.macAddress(mac) == nullptr) {
    return String();
  }

  return bytesToLowerHex(mac, sizeof(mac));
}

bool connectStaHardcoded(unsigned long timeoutMs) {
  WiFi.persistent(false);
  WiFi.setAutoReconnect(true);
  WiFi.mode(WIFI_STA);
  WiFi.begin(MICA_WIFI_SSID, MICA_WIFI_PASSWORD);
  setConnectivityState(kWifiStateConnecting, "wifi_connecting_hardcoded", true);

  Serial.printf(
      "[wifi_connecting] STA hardcoded ssid=\"%s\" timeout_ms=%lu\n",
      MICA_WIFI_SSID,
      timeoutMs);

  const unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED) {
    resetTaskWatchdog();
    if (millis() - start > timeoutMs) {
      Serial.println("[wifi_failed] timeout aguardando STA hardcoded.");
      setConnectivityState(kWifiStateDisconnected, "wifi_failed_hardcoded", true);
      return false;
    }
    delay(150);
  }

  gWifiDisconnectedSinceMs = 0;
  gLastWifiReconnectAttemptMs = 0;
  setConnectivityState(kWifiStateConnected, "wifi_connected_hardcoded", true);
  Serial.printf(
      "[wifi_connected] STA hardcoded ip=%s rssi=%d\n",
      WiFi.localIP().toString().c_str(),
      WiFi.RSSI());
  return true;
}

bool autoRegisterIfNeeded() {
  // Always seed the server endpoint from the build-time constants. NVS keys
  // "host"/"port" are no longer authoritative under STA-hardcoded; they remain
  // legible only as cache for older boots that never made the transition.
  gServerHost = String(MICA_SERVER_HOST);
  gServerPort = MICA_SERVER_PORT;

  if (gDeviceId.length() > 0 && gToken.length() > 0) {
    Serial.printf(
        "[auto_register] credenciais ja existentes em NVS deviceId=%s; pulando registro.\n",
        gDeviceId.c_str());
    return true;
  }

  const String mac = getDeviceMacAddressHex();
  if (mac.length() == 0) {
    Serial.println("[auto_register_failed] MAC indisponivel; WiFi.macAddress retornou vazio.");
    return false;
  }

  String url = "http://";
  url += MICA_SERVER_HOST;
  url += ":";
  url += String(MICA_SERVER_PORT);
  url += "/api/v1/auto-register";

  for (unsigned long attempt = 1; attempt <= kAutoRegisterMaxAttempts; attempt++) {
    HTTPClient http;
    if (!http.begin(url)) {
      Serial.printf("[auto_register_retry] http_begin_failed attempt=%lu\n", attempt);
      delay(kAutoRegisterRetryBackoffMs);
      continue;
    }

    http.addHeader("Content-Type", "application/json");

    JsonDocument req;
    req["macAddress"] = mac;
    req["firmwareVersion"] = kFirmwareVersion;
    req["boardModel"] = kBoardModel;
    req["panelType"] = kPanelType;
    req["profile"] = kFirmwareProfile;
#ifdef MICA_DEVICE_NAME
    if (sizeof(MICA_DEVICE_NAME) > 1) {  // not empty
      req["deviceName"] = MICA_DEVICE_NAME;
    }
#endif

    String body;
    serializeJson(req, body);
    const int code = http.POST(body);

    if (code >= 200 && code < 300) {
      JsonDocument resp;
      const auto err = deserializeJson(resp, http.getString());
      http.end();
      if (err != DeserializationError::Ok) {
        Serial.printf("[auto_register_retry] resposta_invalida attempt=%lu err=%s\n", attempt, err.c_str());
        delay(kAutoRegisterRetryBackoffMs);
        continue;
      }

      const String deviceId = resp["deviceId"] | "";
      const String token = resp["token"] | "";
      if (!persistDeviceIdentity(deviceId, token)) {
        Serial.printf("[auto_register_retry] resposta_sem_credenciais attempt=%lu\n", attempt);
        delay(kAutoRegisterRetryBackoffMs);
        continue;
      }

      const String mqttHost = resp["mqttHost"] | "";
      const int mqttPort = resp["mqttPort"] | 0;
      const String mqttRootTopic = resp["mqttRootTopic"] | "";
      persistMqttCoordinates(mqttHost, mqttPort, mqttRootTopic);

      const bool reusedExisting = resp["reusedExisting"] | false;
      Serial.printf(
          "[auto_register_success] deviceId=%s reused=%s mqtt=%s:%u root=%s\n",
          gDeviceId.c_str(),
          reusedExisting ? "true" : "false",
          gMqttHost.c_str(),
          static_cast<unsigned>(gMqttPort),
          gMqttRootTopic.c_str());
      setConnectivityState(kWifiStateConnected, "auto_register_success", true);
      return true;
    }

    Serial.printf("[auto_register_retry] http_status=%d attempt=%lu\n", code, attempt);
    http.end();
    delay(kAutoRegisterRetryBackoffMs);
  }

  Serial.println("[auto_register_failed] todas as tentativas esgotadas; manter device offline.");
  setConnectivityState(kWifiStateConnected, "auto_register_failed", true);
  return false;
}

// ---------------------------------------------------------------------------
// Legacy stubs (no-op replacements for the WiFiManager-based provisioning)
// ---------------------------------------------------------------------------

bool isProvisioningIncomplete() {
  return gDeviceId.isEmpty() || gToken.isEmpty();
}

const char* resolveProvisioningIncompleteReason() {
  return "boot_missing_device_credentials";
}

void sendSerialHello() {
  JsonDocument hello;
  hello["type"] = "hello";
  hello["protocol"] = kSerialProvisioningProtocol;
  hello["deviceId"] = gDeviceId;
  hello["firmwareVersion"] = kFirmwareVersion;
  hello["transport"] = "sta_hardcoded";

  String payload;
  serializeJson(hello, payload);
  Serial.println(payload);
}

void processSerialProvisioning() {
  // Serial provisioning was deprecated alongside the AP portal. The function
  // is kept so that main.cpp / mica_commands.cpp continue to link, but it
  // performs no work; runtime credentials live exclusively in mica_config.h
  // and the device auto-registration response.
}

bool startProvisioningPortal(const char* /*reason*/) {
  Serial.println(
      "[provisioning_disabled] portal AP foi removido. Edite mica_config.h e reflashe o firmware.");
  setProvisioningPortalActive(false, "portal_disabled");
  return false;
}

void enterProvisioningMode(bool clearDeviceCredentials, const char* /*reason*/) {
  if (clearDeviceCredentials) {
    gDeviceId = String();
    gToken = String();
    gPrefs.remove("deviceId");
    gPrefs.remove("token");
  }

  Serial.println(
      "[provisioning_disabled] solicitacao ignorada; portal AP foi removido. Reflashe com mica_config.h atualizado.");
  setProvisioningPortalActive(false, "portal_disabled");
}
