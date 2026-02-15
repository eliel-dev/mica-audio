#include <Arduino.h>
#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <Preferences.h>
#include <Update.h>
#include <WebSocketsClient.h>
#include <WiFi.h>
#include <WiFiManager.h>

#if defined(MICA_PROFILE_STABLE)
#include <Adafruit_Protomatter.h>
#endif

#if defined(MICA_PROFILE_DMA_EXP)
#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>
#endif

namespace {
constexpr uint8_t kBinsCount = MICA_STREAM_BINS;
constexpr size_t kStreamFrameSize = 81;
constexpr unsigned long kProvisioningFallbackMs = 60000;
constexpr unsigned long kTelemetryIntervalMs = 2000;

#if defined(LED_BUILTIN)
constexpr int kTestLedPin = LED_BUILTIN;
#elif defined(PIN_LED)
constexpr int kTestLedPin = PIN_LED;
#else
constexpr int kTestLedPin = -1;
#endif

constexpr unsigned long kTestLedDurationMs = 1500;
constexpr unsigned long kTestLedTogglePeriodMs = 120;

#if defined(MICA_PROFILE_DMA_EXP)
constexpr const char* kFirmwareProfile = "dma_exp";
constexpr const char* kFirmwareVersion = "vNext-dma_exp";
#else
constexpr const char* kFirmwareProfile = "stable";
constexpr const char* kFirmwareVersion = "vNext-stable";
#endif

Preferences gPrefs;
WebSocketsClient gWs;
String gServerHost;
uint16_t gServerPort = 5272;
String gDeviceId;
String gToken;
uint8_t gBins[kBinsCount] = {0};
uint8_t gLevel = 0;
uint8_t gServerBrightness = 255;
unsigned long gLastFrameMs = 0;
unsigned long gDisconnectedSinceMs = 0;
unsigned long gLastTelemetryMs = 0;
unsigned long gTestLedUntilMs = 0;
unsigned long gTestLedNextToggleMs = 0;
bool gTestLedState = false;

bool postJsonWithAuth(const String& path, JsonDocument& doc) {
  if (gDeviceId.isEmpty() || gToken.isEmpty() || gServerHost.isEmpty()) {
    return false;
  }

  HTTPClient http;
  String url = "http://" + gServerHost + ":" + String(gServerPort) + path;
  if (!http.begin(url)) {
    return false;
  }

  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Id", gDeviceId);
  http.addHeader("X-Device-Token", gToken);

  String body;
  serializeJson(doc, body);
  int code = http.POST(body);
  http.end();

  return code >= 200 && code < 300;
}

void sendCommandProgress(
    const String& commandId,
    uint8_t progressPercent,
    const char* stage,
    const String& message,
    int successFlag = -1) {
  if (commandId.isEmpty() || !gWs.isConnected()) {
    return;
  }

  JsonDocument progress;
  progress["type"] = "command_progress";
  progress["deviceId"] = gDeviceId;
  progress["commandId"] = commandId;
  progress["progressPercent"] = progressPercent;
  if (stage != nullptr && stage[0] != '\0') {
    progress["stage"] = stage;
  }
  if (message.length() > 0) {
    progress["message"] = message;
  }
  if (successFlag == 0 || successFlag == 1) {
    progress["success"] = successFlag == 1;
  }

  String payload;
  serializeJson(progress, payload);
  gWs.sendTXT(payload);
}

void postCommandAck(
    const String& commandId,
    bool success,
    const String& message,
    int progressPercent,
    const char* stage,
    const char* errorCode = nullptr) {
  if (commandId.isEmpty()) {
    return;
  }

  JsonDocument ack;
  ack["deviceId"] = gDeviceId;
  ack["commandId"] = commandId;
  ack["success"] = success;
  ack["message"] = message;
  ack["progressPercent"] = progressPercent;
  if (stage != nullptr && stage[0] != '\0') {
    ack["stage"] = stage;
  }
  if (errorCode != nullptr && errorCode[0] != '\0') {
    ack["errorCode"] = errorCode;
  }

  postJsonWithAuth("/api/v1/device/command-ack", ack);
}

void postOtaResult(const String& commandId, bool success, const String& message, const char* errorCode = nullptr) {
  JsonDocument result;
  result["deviceId"] = gDeviceId;
  result["commandId"] = commandId;
  result["success"] = success;
  result["message"] = message;
  if (errorCode != nullptr && errorCode[0] != '\0') {
    result["errorCode"] = errorCode;
  }

  postJsonWithAuth("/api/v1/device/ota/result", result);
}

void sendTelemetry(bool force) {
  if (!gWs.isConnected() || gDeviceId.isEmpty()) {
    return;
  }

  unsigned long now = millis();
  if (!force && now - gLastTelemetryMs < kTelemetryIntervalMs) {
    return;
  }

  gLastTelemetryMs = now;

  JsonDocument telemetry;
  telemetry["type"] = "telemetry";
  telemetry["deviceId"] = gDeviceId;
  telemetry["rssi"] = WiFi.status() == WL_CONNECTED ? WiFi.RSSI() : -100;
  telemetry["firmwareVersion"] = kFirmwareVersion;
  telemetry["ipAddress"] = WiFi.localIP().toString();

  String payload;
  serializeJson(telemetry, payload);
  gWs.sendTXT(payload);
}

void clearTestLed() {
  if (kTestLedPin < 0) {
    return;
  }

  gTestLedState = false;
  gTestLedUntilMs = 0;
  gTestLedNextToggleMs = 0;
  digitalWrite(kTestLedPin, LOW);
}

void triggerTestLed() {
  if (kTestLedPin < 0) {
    return;
  }

  gTestLedState = false;
  gTestLedUntilMs = millis() + kTestLedDurationMs;
  gTestLedNextToggleMs = 0;
  digitalWrite(kTestLedPin, LOW);
}

void updateTestLed() {
  if (kTestLedPin < 0 || gTestLedUntilMs == 0) {
    return;
  }

  unsigned long now = millis();
  if (now >= gTestLedUntilMs) {
    clearTestLed();
    return;
  }

  if (gTestLedNextToggleMs == 0 || now >= gTestLedNextToggleMs) {
    gTestLedState = !gTestLedState;
    digitalWrite(kTestLedPin, gTestLedState ? HIGH : LOW);
    gTestLedNextToggleMs = now + kTestLedTogglePeriodMs;
  }
}

void handleOtaFailure(const String& commandId, const String& message, const char* errorCode) {
  sendCommandProgress(commandId, 99, "ota-failed", message, 0);
  postCommandAck(commandId, false, message, 99, "ota-failed", errorCode);
  postOtaResult(commandId, false, message, errorCode);
}

void startOta(const String& commandId) {
  if (commandId.isEmpty()) {
    return;
  }

  sendCommandProgress(commandId, 1, "ota-init", "Iniciando OTA...");

  HTTPClient latestHttp;
  String latestUrl = "http://" + gServerHost + ":" + String(gServerPort)
      + "/api/v1/device/firmware/latest?deviceId=" + gDeviceId + "&token=" + gToken;

  if (!latestHttp.begin(latestUrl)) {
    handleOtaFailure(commandId, "Falha ao abrir endpoint OTA (latest).", "ota_latest_begin_failed");
    return;
  }

  int latestCode = latestHttp.GET();
  String latestBody = latestHttp.getString();
  latestHttp.end();

  if (latestCode < 200 || latestCode >= 300) {
    handleOtaFailure(commandId, "Servidor nao disponibilizou firmware OTA.", "ota_latest_http_error");
    return;
  }

  JsonDocument latestJson;
  if (deserializeJson(latestJson, latestBody) != DeserializationError::Ok) {
    handleOtaFailure(commandId, "Resposta OTA invalida.", "ota_latest_parse_error");
    return;
  }

  String downloadUrl = latestJson["downloadUrl"] | "";
  if (downloadUrl.isEmpty()) {
    handleOtaFailure(commandId, "URL de download OTA ausente.", "ota_download_url_missing");
    return;
  }

  sendCommandProgress(commandId, 10, "ota-download", "Baixando firmware...");

  HTTPClient downloadHttp;
  if (!downloadHttp.begin(downloadUrl)) {
    handleOtaFailure(commandId, "Falha ao iniciar download OTA.", "ota_download_begin_failed");
    return;
  }

  int downloadCode = downloadHttp.GET();
  if (downloadCode != HTTP_CODE_OK) {
    String responseBody = downloadHttp.getString();
    downloadHttp.end();
    String message = "Download OTA retornou erro HTTP " + String(downloadCode);
    if (responseBody.length() > 0) {
      responseBody.replace("\n", " ");
      responseBody.replace("\r", " ");
      if (responseBody.length() > 120) {
        responseBody = responseBody.substring(0, 120);
      }
      message += ": " + responseBody;
    }

    handleOtaFailure(commandId, message, "ota_download_http_error");
    return;
  }

  int totalSize = downloadHttp.getSize();
  if (!Update.begin(totalSize > 0 ? static_cast<size_t>(totalSize) : UPDATE_SIZE_UNKNOWN)) {
    downloadHttp.end();
    handleOtaFailure(commandId, "Falha ao iniciar gravacao OTA.", "ota_update_begin_failed");
    return;
  }

  WiFiClient* stream = downloadHttp.getStreamPtr();
  uint8_t buffer[1024];
  size_t writtenTotal = 0;
  int lastPercent = 10;

  while (downloadHttp.connected() && (totalSize < 0 || writtenTotal < static_cast<size_t>(totalSize))) {
    size_t available = stream->available();
    if (available == 0) {
      delay(1);
      continue;
    }

    size_t toRead = available > sizeof(buffer) ? sizeof(buffer) : available;
    int readCount = stream->readBytes(reinterpret_cast<char*>(buffer), toRead);
    if (readCount <= 0) {
      delay(1);
      continue;
    }

    size_t written = Update.write(buffer, static_cast<size_t>(readCount));
    if (written != static_cast<size_t>(readCount)) {
      downloadHttp.end();
      handleOtaFailure(commandId, "Falha ao gravar bloco OTA.", "ota_update_write_failed");
      return;
    }

    writtenTotal += written;

    int percent = lastPercent;
    if (totalSize > 0) {
      percent = 10 + static_cast<int>((writtenTotal * 85ULL) / static_cast<size_t>(totalSize));
      if (percent > 95) {
        percent = 95;
      }
    } else if (percent < 95) {
      percent += 1;
    }

    if (percent > lastPercent) {
      lastPercent = percent;
      sendCommandProgress(commandId, static_cast<uint8_t>(lastPercent), "ota-write", "Aplicando firmware...");
    }
  }

  bool otaOk = Update.end(true) && Update.isFinished();
  downloadHttp.end();

  if (!otaOk) {
    handleOtaFailure(commandId, "Finalizacao OTA falhou.", "ota_finalize_failed");
    return;
  }

  sendCommandProgress(commandId, 100, "ota-complete", "OTA concluida. Reiniciando...", 1);
  postCommandAck(commandId, true, "OTA concluida.", 100, "ota-complete");
  postOtaResult(commandId, true, "OTA concluida.");

  delay(600);
  ESP.restart();
}

void startProvisioningPortal() {
  WiFiManager wm;
  wm.setConfigPortalBlocking(true);
  wm.setConfigPortalTimeout(300);

  WiFiManagerParameter pHost("host", "Servidor host", gPrefs.getString("host", "micaaudio.local").c_str(), 63);
  WiFiManagerParameter pPort("port", "Servidor porta", gPrefs.getString("port", "5272").c_str(), 8);
  WiFiManagerParameter pPair("pair", "Codigo pareamento", "", 12);
  WiFiManagerParameter pName("name", "Nome dispositivo", gPrefs.getString("name", "Matrix Portal S3").c_str(), 32);

  wm.addParameter(&pHost);
  wm.addParameter(&pPort);
  wm.addParameter(&pPair);
  wm.addParameter(&pName);

  String apName = "MicaAudio-Setup-" + String((uint32_t)ESP.getEfuseMac(), HEX).substring(6);
  if (!wm.autoConnect(apName.c_str())) {
    ESP.restart();
    return;
  }

  gPrefs.putString("host", pHost.getValue());
  gPrefs.putString("port", pPort.getValue());
  gPrefs.putString("name", pName.getValue());
  gServerHost = pHost.getValue();
  gServerPort = static_cast<uint16_t>(atoi(pPort.getValue()));

  String pairingCode = pPair.getValue();
  if (pairingCode.length() == 0) {
    return;
  }

  HTTPClient http;
  String url = "http://" + gServerHost + ":" + String(gServerPort) + "/api/v1/pair";
  if (!http.begin(url)) {
    return;
  }

  http.addHeader("Content-Type", "application/json");
  JsonDocument req;
  req["pairingCode"] = pairingCode;
  req["deviceName"] = pName.getValue();
  req["profile"] = kFirmwareProfile;
  req["firmwareVersion"] = kFirmwareVersion;

  String body;
  serializeJson(req, body);
  int code = http.POST(body);
  if (code >= 200 && code < 300) {
    JsonDocument resp;
    deserializeJson(resp, http.getString());
    gDeviceId = resp["deviceId"] | "";
    gToken = resp["token"] | "";
    if (!gDeviceId.isEmpty() && !gToken.isEmpty()) {
      gPrefs.putString("deviceId", gDeviceId);
      gPrefs.putString("token", gToken);
    }
  }

  http.end();
}

void enterProvisioningMode() {
  gWs.disconnect();
  gPrefs.remove("deviceId");
  gPrefs.remove("token");
  gDeviceId = "";
  gToken = "";
  startProvisioningPortal();
}

void onWsEvent(WStype_t type, uint8_t *payload, size_t len) {
  if (type == WStype_CONNECTED) {
    gDisconnectedSinceMs = 0;
    sendTelemetry(true);
    return;
  }

  if (type == WStype_DISCONNECTED) {
    gLastTelemetryMs = 0;
    return;
  }

  if (type == WStype_BIN && len >= kStreamFrameSize) {
    gLevel = payload[14];
    memcpy(gBins, payload + 15, kBinsCount);
    gServerBrightness = payload[79];
    gLastFrameMs = millis();
    return;
  }

  if (type != WStype_TEXT || payload == nullptr || len == 0) {
    return;
  }

  JsonDocument control;
  if (deserializeJson(control, payload, len) != DeserializationError::Ok) {
    return;
  }

  const char *command = control["command"] | "";
  String commandId = control["commandId"] | "";

  if (strcmp(command, "enter_provisioning") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    postCommandAck(commandId, true, "Entrando em provisioning.", 100, "enter-provisioning");
    sendCommandProgress(commandId, 100, "enter-provisioning", "Entrando em provisioning.", 1);
    enterProvisioningMode();
    return;
  }

  if (strcmp(command, "revoke_and_restart") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    postCommandAck(commandId, true, "Revogando e reiniciando.", 100, "revoke-restart");
    sendCommandProgress(commandId, 100, "revoke-restart", "Reiniciando dispositivo.", 1);
    gPrefs.remove("deviceId");
    gPrefs.remove("token");
    delay(200);
    ESP.restart();
    return;
  }

  if (strcmp(command, "test_led") == 0) {
    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    triggerTestLed();
    postCommandAck(commandId, true, "Teste de LED acionado.", 100, "test-led");
    sendCommandProgress(commandId, 100, "test-led", "Teste de LED acionado.", 1);
    return;
  }

  if (strcmp(command, "start_ota") == 0) {
    startOta(commandId);
    return;
  }

  postCommandAck(commandId, false, "Comando desconhecido.", 100, "unknown", "unknown_command");
  sendCommandProgress(commandId, 100, "unknown", "Comando desconhecido.", 0);
}

void connectWebSocket() {
  if (gDeviceId.isEmpty() || gToken.isEmpty() || gServerHost.isEmpty()) {
    return;
  }

  String path = "/ws/v1/stream?deviceId=" + gDeviceId + "&token=" + gToken;
  gWs.begin(gServerHost.c_str(), gServerPort, path.c_str());
  gWs.onEvent(onWsEvent);
  gWs.setReconnectInterval(2000);
}

void drawBars() {
  // TODO: render bins64 in 64x32 mirror bars using Protomatter (stable)
  // or HUB75 DMA library (dma_exp).
  // gServerBrightness already carries server-side brightness cap (0..255).
}
}  // namespace

void setup() {
  Serial.begin(115200);
  gPrefs.begin("micaaudio", false);

  if (kTestLedPin >= 0) {
    pinMode(kTestLedPin, OUTPUT);
    digitalWrite(kTestLedPin, LOW);
  }

  gServerHost = gPrefs.getString("host", "");
  gServerPort = static_cast<uint16_t>(atoi(gPrefs.getString("port", "5272").c_str()));
  gDeviceId = gPrefs.getString("deviceId", "");
  gToken = gPrefs.getString("token", "");

  if (gServerHost.isEmpty() || gServerPort == 0 || WiFi.status() != WL_CONNECTED) {
    startProvisioningPortal();
  }

  connectWebSocket();
}

void loop() {
  if (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    return;
  }

  gWs.loop();

  if (!gWs.isConnected()) {
    if (gDisconnectedSinceMs == 0) {
      gDisconnectedSinceMs = millis();
    }

    if (millis() - gDisconnectedSinceMs > kProvisioningFallbackMs) {
      enterProvisioningMode();
      connectWebSocket();
      gDisconnectedSinceMs = 0;
    }
  } else {
    gDisconnectedSinceMs = 0;
    sendTelemetry(false);
  }

  if (millis() - gLastFrameMs > 15000) {
    memset(gBins, 0, sizeof(gBins));
    gLevel = 0;
  }

  updateTestLed();
  drawBars();
}
