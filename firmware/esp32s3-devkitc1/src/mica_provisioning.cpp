// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---rollback-para-ap-first-estavel
// DOCS: docs/wiki/modules/firmware-esp32s3-devkitc1.md#atualizacao-2026-04---ap-first-com-hub75-adiado-no-boot-limpo
// DOCS: docs/wiki/guides/setup-new-device.md#passos
// DOCS: docs/handoffs/2026-04-16-ap-first-wifi-mem-and-copy-logs.md
// DOCS: docs/handoffs/2026-04-18-wifi-reconnect-persistence-after-reset.md
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md

#include "mica_provisioning.h"

#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <WiFi.h>
#include <WiFiManager.h>

#include "mica_commands.h"
#include "mica_globals.h"
#include "mica_network.h"
#include "mica_panels.h"
#include "mica_prefs.h"

// ===========================================================================
// Internal helpers
// ===========================================================================

static void sendSerialJson(JsonDocument& document) {
  String payload;
  serializeJson(document, payload);
  Serial.println(payload);
}

bool isProvisioningIncomplete() {
  const bool missingServerConfig = gServerHost.isEmpty() || gServerPort == 0;
  const bool missingDeviceCredentials = gDeviceId.isEmpty() || gToken.isEmpty();
  return missingServerConfig || missingDeviceCredentials;
}

const char* resolveProvisioningIncompleteReason() {
  return (gServerHost.isEmpty() || gServerPort == 0)
      ? "boot_missing_server_config"
      : "boot_missing_device_credentials";
}

void sendSerialHello() {
  JsonDocument hello;
  hello["type"] = "hello";
  hello["protocol"] = kSerialProvisioningProtocol;
  hello["deviceId"] = gDeviceId;
  hello["firmwareVersion"] = kFirmwareVersion;
  JsonArray caps = hello["capabilities"].to<JsonArray>();
  caps.add("provision");
  sendSerialJson(hello);
}

static void sendSerialProgress(const String& requestId, const char* stage, const char* message) {
  if (requestId.length() == 0) {
    return;
  }

  JsonDocument progress;
  progress["type"] = "progress";
  progress["requestId"] = requestId;
  progress["stage"] = stage;
  progress["message"] = message;
  sendSerialJson(progress);
}

static void sendSerialResult(
    const String& requestId,
    bool ok,
    const String& message,
    const char* errorCode = nullptr) {
  if (requestId.length() == 0) {
    return;
  }

  JsonDocument result;
  result["type"] = "result";
  result["requestId"] = requestId;
  result["ok"] = ok;
  result["message"] = message;
  if (errorCode != nullptr && errorCode[0] != '\0') {
    result["errorCode"] = errorCode;
  }

  if (!gDeviceId.isEmpty()) {
    result["deviceId"] = gDeviceId;
  }

  sendSerialJson(result);
}

static bool tryParseServerBaseUrl(const String& rawBaseUrl, String& host, uint16_t& port) {
  String normalized = rawBaseUrl;
  normalized.trim();
  if (normalized.length() == 0) {
    return false;
  }

  if (normalized.startsWith("http://")) {
    normalized = normalized.substring(7);
  } else if (normalized.startsWith("https://")) {
    normalized = normalized.substring(8);
  }

  int slashIndex = normalized.indexOf('/');
  if (slashIndex >= 0) {
    normalized = normalized.substring(0, slashIndex);
  }

  int colonIndex = normalized.lastIndexOf(':');
  if (colonIndex >= 0) {
    host = normalized.substring(0, colonIndex);
    String portRaw = normalized.substring(colonIndex + 1);
    int parsedPort = portRaw.toInt();
    if (parsedPort <= 0 || parsedPort > 65535) {
      return false;
    }

    port = static_cast<uint16_t>(parsedPort);
    return host.length() > 0;
  }

  host = normalized;
  port = 5272;
  return host.length() > 0;
}

static String buildServerBaseUrl(const String& host, uint16_t port) {
  if (host.length() == 0) {
    return "";
  }

  const uint16_t resolvedPort = port == 0 ? 5272 : port;
  return "http://" + host + ":" + String(resolvedPort);
}

static bool tryApplyProvisioningPortalServer(
    const String& rawServerBaseUrl,
    const String& savedHost,
    uint16_t savedPort,
    String& errorCode,
    String& errorMessage) {
  String normalizedServerBaseUrl = rawServerBaseUrl;
  normalizedServerBaseUrl.trim();

  if (normalizedServerBaseUrl.length() == 0) {
    if (savedHost.length() > 0 && savedPort != 0) {
      gServerHost = savedHost;
      gServerPort = savedPort;
      gMqttHost = gServerHost;
      gMqttPort = kDefaultMqttPort;
      gMqttRootTopic = kDefaultMqttRootTopic;
      persistMqttConfig();
      setConnectivityState(kWifiStateConnected, "portal_server_empty_kept_saved", true);
      Serial.printf("[provisioning] campo Servidor vazio; mantendo configuracao salva %s.\n",
          buildServerBaseUrl(gServerHost, gServerPort).c_str());
      return true;
    }

    gServerHost = "";
    gServerPort = 5272;
    gMqttHost = "";
    gMqttPort = kDefaultMqttPort;
    gMqttRootTopic = kDefaultMqttRootTopic;
    persistMqttConfig();
    setConnectivityState(kWifiStateConnected, "portal_server_empty_discovery", true);
    Serial.println("[provisioning] campo Servidor vazio; discovery LAN tentara localizar o servidor.");
    return true;
  }

  String parsedHost;
  uint16_t parsedPort = 0;
  if (!tryParseServerBaseUrl(normalizedServerBaseUrl, parsedHost, parsedPort)) {
    if (savedHost.length() > 0 && savedPort != 0) {
      gServerHost = savedHost;
      gServerPort = savedPort;
      gMqttHost = gServerHost;
      gMqttPort = kDefaultMqttPort;
      gMqttRootTopic = kDefaultMqttRootTopic;
      persistMqttConfig();
      errorCode = "portal_server_invalid_kept_saved";
      errorMessage = "Campo Servidor invalido; mantendo configuracao salva.";
      setConnectivityState(kWifiStateConnected, "portal_server_invalid_kept_saved", true);
      Serial.printf("[provisioning] campo Servidor invalido '%s'; mantendo configuracao salva %s.\n",
          normalizedServerBaseUrl.c_str(),
          buildServerBaseUrl(gServerHost, gServerPort).c_str());
      return true;
    }

    errorCode = "portal_server_invalid";
    errorMessage = "Campo Servidor invalido no portal.";
    setConnectivityState(kWifiStateConnected, "portal_server_invalid", true);
    Serial.printf("[provisioning] campo Servidor invalido '%s' e sem configuracao salva.\n", normalizedServerBaseUrl.c_str());
    return false;
  }

  gServerHost = parsedHost;
  gServerPort = parsedPort;
  gPrefs.putString("host", gServerHost);
  gPrefs.putString("port", String(gServerPort));
  gMqttHost = gServerHost;
  gMqttPort = kDefaultMqttPort;
  gMqttRootTopic = kDefaultMqttRootTopic;
  persistMqttConfig();
  setConnectivityState(kWifiStateConnected, "portal_server_configured", true);
  Serial.printf("[provisioning] servidor configurado manualmente: %s.\n", buildServerBaseUrl(gServerHost, gServerPort).c_str());
  return true;
}

static bool connectWifiWithTimeout(const String& ssid, const String& password, unsigned long timeoutMs) {
  WiFi.setAutoReconnect(true);
  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid.c_str(), password.c_str());
  setConnectivityState(kWifiStateConnecting, "wifi_connecting", true);

  unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED) {
    resetTaskWatchdog();
    if (millis() - start > timeoutMs) {
      return false;
    }

    delay(200);
  }

  gWifiDisconnectedSinceMs = 0;
  gLastWifiReconnectAttemptMs = 0;
  setConnectivityState(kWifiStateConnected, "wifi_connected", true);
  return true;
}

static bool pairWithServer(const String& pairingCode, const String& deviceName, String& errorCode, String& errorMessage) {
  if (pairingCode.length() == 0) {
    setConnectivityState(kWifiStateConnected, "provisioned_without_pairing", true);
    return true;
  }

  HTTPClient http;
  String url = "http://" + gServerHost + ":" + String(gServerPort) + "/api/v1/pair";
  if (!http.begin(url)) {
    errorCode = "pair_http_begin_failed";
    errorMessage = "Nao foi possivel iniciar conexao HTTP para pareamento.";
    setConnectivityState(kWifiStateConnected, "pair_http_begin_failed", true);
    return false;
  }

  http.setConnectTimeout(5000);
  http.setTimeout(15000);
  http.addHeader("Content-Type", "application/json");
  JsonDocument req;
  req["pairingCode"] = pairingCode;
  req["deviceName"] = deviceName;
  req["deviceMac"] = WiFi.macAddress();
  req["profile"] = kFirmwareProfile;
  req["firmwareVersion"] = kFirmwareVersion;
  req["boardModel"] = kBoardModel;
  req["panelType"] = kPanelType;

  String body;
  serializeJson(req, body);
  resetTaskWatchdog();
  int code = http.POST(body);
  resetTaskWatchdog();
  if (code >= 200 && code < 300) {
    JsonDocument resp;
    if (deserializeJson(resp, http.getString()) != DeserializationError::Ok) {
      errorCode = "pair_response_invalid";
      errorMessage = "Resposta de pareamento invalida.";
      http.end();
      return false;
    }

    gDeviceId = resp["deviceId"] | "";
    gToken = resp["token"] | "";
    if (gDeviceId.isEmpty() || gToken.isEmpty()) {
      errorCode = "pair_response_missing_auth";
      errorMessage = "Resposta de pareamento sem credenciais.";
      http.end();
      return false;
    }

    gPrefs.putString("deviceId", gDeviceId);
    gPrefs.putString("token", gToken);

    // Atualiza host/port a partir do httpBase retornado pelo servidor,
    // garantindo que o dispositivo use o IP real em vez de micaaudio.local.
    String httpBase = resp["httpBase"] | "";
    String parsedHost;
    uint16_t parsedPort = 0;
    if (httpBase.length() > 0 && tryParseServerBaseUrl(httpBase, parsedHost, parsedPort)) {
      gServerHost = parsedHost;
      gServerPort = parsedPort;
      gPrefs.putString("host", gServerHost);
      gPrefs.putString("port", String(gServerPort));
    }

    String mqttHost = resp["mqttHost"] | "";
    int mqttPort = resp["mqttPort"] | 0;
    String mqttRootTopic = resp["mqttRootTopic"] | "";
    gMqttHost = mqttHost.length() > 0 ? mqttHost : gServerHost;
    gMqttPort = mqttPort > 0 ? static_cast<uint16_t>(mqttPort) : kDefaultMqttPort;
    gMqttRootTopic = mqttRootTopic.length() > 0 ? mqttRootTopic : kDefaultMqttRootTopic;
    persistMqttConfig();

    setConnectivityState(kWifiStateConnected, "pair_success", true);
    http.end();
    return true;
  }

  errorCode = "pair_http_error";
  errorMessage = "Servidor rejeitou pareamento.";
  setConnectivityState(kWifiStateConnected, "pair_http_error", true);
  http.end();
  return false;
}

static void handleSerialProvisioningLine(const String& line) {
  JsonDocument command;
  if (deserializeJson(command, line) != DeserializationError::Ok) {
    return;
  }

  String type = command["type"] | "";
  if (!type.equalsIgnoreCase("provision")) {
    return;
  }

  String requestId = command["requestId"] | "";
  String ssid = command["ssid"] | "";
  String password = command["password"] | "";
  String serverBaseUrl = command["serverBaseUrl"] | "";
  String pairCode = command["pairCode"] | "";
  String requestedDeviceName = command["deviceName"] | "";
  requestedDeviceName.trim();

  if (requestId.length() == 0) {
    return;
  }

  if (ssid.length() == 0) {
    sendSerialResult(requestId, false, "SSID ausente no request de provisionamento.", "ssid_required");
    return;
  }

  String parsedHost;
  uint16_t parsedPort = 0;
  if (!tryParseServerBaseUrl(serverBaseUrl, parsedHost, parsedPort)) {
    sendSerialResult(requestId, false, "serverBaseUrl invalido.", "server_base_url_invalid");
    return;
  }

  sendSerialProgress(requestId, "wifi_connecting", "Conectando ao Wi-Fi.");
  if (!connectWifiWithTimeout(ssid, password, kWifiConnectAttemptTimeoutMs)) {
    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);
    sendSerialResult(requestId, false, "Falha ao conectar no Wi-Fi.", "wifi_connect_failed");
    return;
  }

  gServerHost = parsedHost;
  gServerPort = parsedPort;
  gPrefs.putString("host", gServerHost);
  gPrefs.putString("port", String(gServerPort));
  gMqttHost = gServerHost;
  gMqttPort = kDefaultMqttPort;
  gMqttRootTopic = kDefaultMqttRootTopic;
  persistMqttConfig();
  sendSerialProgress(requestId, "wifi_connected", "Wi-Fi conectado.");

  if (requestedDeviceName.length() > 0) {
    gPrefs.putString("name", requestedDeviceName);
  }

  String deviceName = requestedDeviceName.length() > 0
      ? requestedDeviceName
      : prefsGetStringOrDefault("name", String(kBoardDisplayName));
  String pairErrorCode;
  String pairErrorMessage;
  sendSerialProgress(requestId, "pairing", "Executando pareamento com o servidor.");
  if (!pairWithServer(pairCode, deviceName, pairErrorCode, pairErrorMessage)) {
    sendSerialResult(requestId, false, pairErrorMessage, pairErrorCode.c_str());
    return;
  }

  gSerialProvisioningWindowActive = false;
  gSerialProvisioningWindowStartedMs = 0;
  connectMqtt();
  connectWebSocket();
  sendTelemetry(true);
  sendSerialProgress(requestId, "done", "Provisionamento concluido.");
  sendSerialResult(requestId, true, "Provisionamento concluido com sucesso.");
}

// ===========================================================================
// Public API
// ===========================================================================

void processSerialProvisioning() {
  const unsigned long now = millis();
  if (gLastSerialHelloMs == 0 || (now - gLastSerialHelloMs) >= kSerialHelloIntervalMs) {
    sendSerialHello();
    gLastSerialHelloMs = now;
  }

  while (Serial.available() > 0) {
    char character = static_cast<char>(Serial.read());
    if (character == '\r') {
      continue;
    }

    if (character == '\n') {
      String line = gSerialInputBuffer;
      gSerialInputBuffer = "";
      line.trim();
      if (line.length() > 0) {
        handleSerialProvisioningLine(line);
      }

      continue;
    }

    if (gSerialInputBuffer.length() < kSerialInputMaxLength) {
      gSerialInputBuffer += character;
    }
  }
}

// DOCS: docs/wiki/guides/setup-new-device.md#passos
bool startProvisioningPortal(const char* reason) {
  gSerialProvisioningWindowActive = false;
  gSerialProvisioningWindowStartedMs = 0;
  (void)publishDeviceLog(
      "warning",
      "portal",
      reason == nullptr ? "portal_open" : reason,
      String("Abrindo portal de provisioning. motivo=") + (reason == nullptr ? "-" : reason),
      false);
  cancelPanelsBatchPlayback();
  disconnectMqtt(true);
  gWs.disconnect();
  gLastTelemetryMs = 0;
  setProvisioningPortalActive(true, reason);
  Serial.printf("[portal_open] motivo=%s\n", reason == nullptr ? "-" : reason);

  WiFiManager wm;
  wm.setConfigPortalBlocking(true);
  wm.setConfigPortalTimeout(0);
  wm.setConnectTimeout(kWifiConnectAttemptTimeoutMs / 1000UL);
  wm.setSaveConnectTimeout(kWifiConnectAttemptTimeoutMs / 1000UL);

  String savedHost = prefsGetStringOrDefault("host", "");
  uint16_t savedPort = prefsGetPortOrDefault("port", 5272);
  String savedServerBaseUrl = buildServerBaseUrl(savedHost, savedPort);
  String savedDeviceName = prefsGetStringOrDefault("name", String(kBoardDisplayName));

  WiFiManagerParameter pServer("server", "Servidor", savedServerBaseUrl.c_str(), 96);
  WiFiManagerParameter pName("name", "Nome dispositivo", savedDeviceName.c_str(), 32);

  wm.addParameter(&pServer);
  wm.addParameter(&pName);

  String apName = "MicaAudio-Setup-" + String((uint32_t)ESP.getEfuseMac(), HEX).substring(6);
  Serial.printf("[provisioning] AP=%s reason=%s\n", apName.c_str(), reason == nullptr ? "-" : reason);
  // Open the AP portal immediately when provisioning is explicitly requested.
  if (!wm.startConfigPortal(apName.c_str())) {
    setProvisioningPortalActive(false, "portal_error");
    setConnectivityState(kWifiStatePortal, "portal_error", true);
    Serial.println("[provisioning] startConfigPortal retornou false; portal nao foi aberto ou foi encerrado sem conexao.");
    return false;
  }

  setProvisioningPortalActive(false, "wifi_connected");
  Serial.println("[portal_close] provisioning encerrado apos conexao Wi-Fi.");
  gWifiDisconnectedSinceMs = 0;
  gLastWifiReconnectAttemptMs = 0;
  setConnectivityState(kWifiStateConnected, "wifi_connected", true);
  gPrefs.putBool("wifiConfigured", true);

  gPrefs.putString("name", pName.getValue());

  String serverConfigErrorCode;
  String serverConfigErrorMessage;
  if (!tryApplyProvisioningPortalServer(pServer.getValue(), savedHost, savedPort, serverConfigErrorCode, serverConfigErrorMessage)) {
    Serial.printf("[provisioning] falha ao aplicar Servidor do portal: %s (%s)\n",
        serverConfigErrorMessage.c_str(),
        serverConfigErrorCode.c_str());
    return false;
  }

  Serial.println("[provisioning] Wi-Fi configurado; aguardando auto-registro via discovery LAN.");
  return true;
}

void enterProvisioningMode(bool clearDeviceCredentials, const char* reason) {
  if (clearDeviceCredentials) {
    gPrefs.remove("deviceId");
    gPrefs.remove("token");
    gDeviceId = "";
    gToken = "";
  }

  (void)startProvisioningPortal(reason);
}
