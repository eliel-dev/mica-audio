#include <Arduino.h>
#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <Preferences.h>
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
// DOCS: docs/wiki/modules/firmware-matrixportal-s3.md#fluxo-de-execucao
constexpr uint8_t kBinsCount = MICA_STREAM_BINS;
constexpr size_t kStreamFrameSize = 81;
constexpr uint8_t kStreamVersion = 1;
constexpr uint8_t kStreamBinsMessageType = 1;
constexpr unsigned long kProvisioningFallbackMs = 60000;
constexpr unsigned long kTelemetryIntervalMs = 2000;
constexpr uint8_t kMatrixWidth = MICA_MATRIX_WIDTH;
constexpr uint8_t kMatrixHeight = MICA_MATRIX_HEIGHT;
constexpr uint8_t kMatrixHalfHeight = kMatrixHeight / 2;
static_assert((kMatrixHeight % 2) == 0, "MICA_MATRIX_HEIGHT must be even.");

constexpr uint8_t kMatrixRgbPins[6] = {42, 41, 40, 38, 39, 37};
constexpr uint8_t kMatrixAddrPins[5] = {45, 36, 48, 35, 21};
constexpr uint8_t kMatrixClockPin = 2;
constexpr uint8_t kMatrixLatchPin = 47;
constexpr uint8_t kMatrixOePin = 14;

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

#if defined(MICA_SECURITY_PROFILE_RELEASE)
constexpr const char* kSecurityProfile = "release";
#else
constexpr const char* kSecurityProfile = "dev";
#endif

Preferences gPrefs;
WebSocketsClient gWs;
String gServerHost;
uint16_t gServerPort = 5272;
String gDeviceId;
String gToken;
String gActiveAppId;
String gActiveAppName;
String gActiveAppConfig;
uint8_t gBins[kBinsCount] = {0};
uint8_t gLevel = 0;
uint8_t gServerBrightness = 255;
unsigned long gLastFrameMs = 0;
unsigned long gDisconnectedSinceMs = 0;
unsigned long gLastTelemetryMs = 0;
unsigned long gTestLedUntilMs = 0;
unsigned long gTestLedNextToggleMs = 0;
bool gTestLedState = false;
bool gMatrixReady = false;
uint8_t gAppliedBrightness = 255;

#if defined(MICA_PROFILE_STABLE)
Adafruit_Protomatter gMatrix(
    kMatrixWidth,
    4,
    1,
    const_cast<uint8_t*>(kMatrixRgbPins),
    4,
    const_cast<uint8_t*>(kMatrixAddrPins),
    kMatrixClockPin,
    kMatrixLatchPin,
    kMatrixOePin,
    true);
#endif

#if defined(MICA_PROFILE_DMA_EXP)
MatrixPanel_I2S_DMA* gMatrix = nullptr;
#endif

struct RgbColor {
  uint8_t r;
  uint8_t g;
  uint8_t b;
};

RgbColor rainbowColorForColumn(uint16_t column, uint16_t columnCount) {
  if (columnCount <= 1) {
    return {255, 0, 0};
  }

  const uint8_t hue = static_cast<uint8_t>((column * 255u) / (columnCount - 1u));
  const uint8_t region = hue / 43u;
  const uint8_t remainder = static_cast<uint8_t>((hue - (region * 43u)) * 6u);
  const uint8_t q = static_cast<uint8_t>(255u - remainder);
  const uint8_t t = remainder;

  switch (region) {
    case 0:
      return {255, t, 0};
    case 1:
      return {q, 255, 0};
    case 2:
      return {0, 255, t};
    case 3:
      return {0, q, 255};
    case 4:
      return {t, 0, 255};
    default:
      return {255, 0, q};
  }
}

void setMatrixBrightness(uint8_t brightness) {
  if (!gMatrixReady || gAppliedBrightness == brightness) {
    return;
  }

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->setBrightness8(brightness);
  }
#endif

  gAppliedBrightness = brightness;
}

void clearMatrix() {
  if (!gMatrixReady) {
    return;
  }

#if defined(MICA_PROFILE_STABLE)
  gMatrix.fillScreen(0);
#endif

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->clearScreen();
  }
#endif
}

void drawMatrixPixel(uint8_t x, uint8_t y, const RgbColor& color) {
#if defined(MICA_PROFILE_STABLE)
  const uint8_t scaledR = static_cast<uint8_t>((static_cast<uint16_t>(color.r) * gAppliedBrightness) / 255u);
  const uint8_t scaledG = static_cast<uint8_t>((static_cast<uint16_t>(color.g) * gAppliedBrightness) / 255u);
  const uint8_t scaledB = static_cast<uint8_t>((static_cast<uint16_t>(color.b) * gAppliedBrightness) / 255u);
  gMatrix.drawPixel(x, y, gMatrix.color565(scaledR, scaledG, scaledB));
#endif

#if defined(MICA_PROFILE_DMA_EXP)
  if (gMatrix != nullptr) {
    gMatrix->drawPixelRGB888(x, y, color.r, color.g, color.b);
  }
#endif
}

void commitMatrixFrame() {
#if defined(MICA_PROFILE_STABLE)
  gMatrix.show();
#endif
}

// DOCS: docs/wiki/modules/firmware-matrixportal-s3.md#pontos-de-alteracao-frequente
bool initMatrixDisplay() {
#if defined(MICA_PROFILE_STABLE)
  ProtomatterStatus status = gMatrix.begin();
  if (status != PROTOMATTER_OK) {
    Serial.printf("Falha ao inicializar Protomatter (status=%d)\n", static_cast<int>(status));
    return false;
  }
#endif

#if defined(MICA_PROFILE_DMA_EXP)
  HUB75_I2S_CFG::i2s_pins pinMap = {
      static_cast<int8_t>(kMatrixRgbPins[0]),
      static_cast<int8_t>(kMatrixRgbPins[1]),
      static_cast<int8_t>(kMatrixRgbPins[2]),
      static_cast<int8_t>(kMatrixRgbPins[3]),
      static_cast<int8_t>(kMatrixRgbPins[4]),
      static_cast<int8_t>(kMatrixRgbPins[5]),
      static_cast<int8_t>(kMatrixAddrPins[0]),
      static_cast<int8_t>(kMatrixAddrPins[1]),
      static_cast<int8_t>(kMatrixAddrPins[2]),
      static_cast<int8_t>(kMatrixAddrPins[3]),
      static_cast<int8_t>(-1),
      static_cast<int8_t>(kMatrixLatchPin),
      static_cast<int8_t>(kMatrixOePin),
      static_cast<int8_t>(kMatrixClockPin)};

  HUB75_I2S_CFG config(kMatrixWidth, kMatrixHeight, 1, pinMap);
  config.i2sspeed = HUB75_I2S_CFG::HZ_10M;
  config.clkphase = false;

  gMatrix = new MatrixPanel_I2S_DMA(config);
  if (gMatrix == nullptr) {
    Serial.println("Falha ao alocar MatrixPanel_I2S_DMA.");
    return false;
  }

  if (!gMatrix->begin()) {
    Serial.println("Falha ao inicializar MatrixPanel_I2S_DMA.");
    delete gMatrix;
    gMatrix = nullptr;
    return false;
  }
#endif

  gMatrixReady = true;
  gAppliedBrightness = gServerBrightness;
  setMatrixBrightness(gServerBrightness);
  clearMatrix();
  commitMatrixFrame();
  return true;
}

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
  if (gActiveAppId.length() > 0) {
    telemetry["activeAppId"] = gActiveAppId;
  }
  if (gActiveAppName.length() > 0) {
    telemetry["activeAppName"] = gActiveAppName;
  }

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

// DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
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

  if (type == WStype_BIN) {
    if (payload == nullptr || len < kStreamFrameSize) {
      return;
    }

    if (payload[0] != kStreamVersion || payload[1] != kStreamBinsMessageType) {
      return;
    }

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
  JsonObjectConst parameters = control["parameters"].as<JsonObjectConst>();

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
  if (strcmp(command, "install_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      postCommandAck(commandId, false, "appId ausente.", 100, "invalid", "app_invalid");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    sendCommandProgress(commandId, 70, "install-app", "Salvando app...");
    gActiveAppId = appId;
    gActiveAppName = appName.length() > 0 ? appName : appId;
    gActiveAppConfig = configJson;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppName", gActiveAppName);
    gPrefs.putString("activeAppConfig", gActiveAppConfig);

    postCommandAck(commandId, true, "App instalado.", 100, "install-app");
    sendCommandProgress(commandId, 100, "install-app", "App instalado.", 1);
    return;
  }

  if (strcmp(command, "activate_app") == 0) {
    String appId = parameters["appId"] | "";
    String appName = parameters["displayName"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      postCommandAck(commandId, false, "appId ausente.", 100, "invalid", "app_invalid");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    gActiveAppId = appId;
    gActiveAppName = appName.length() > 0 ? appName : appId;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppName", gActiveAppName);

    postCommandAck(commandId, true, "App ativado.", 100, "activate-app");
    sendCommandProgress(commandId, 100, "activate-app", "App ativado.", 1);
    return;
  }

  if (strcmp(command, "set_app_config") == 0) {
    String appId = parameters["appId"] | "";
    String configJson = parameters["configJson"] | "";

    sendCommandProgress(commandId, 20, "received", "Comando recebido.");
    if (appId.length() == 0) {
      postCommandAck(commandId, false, "appId ausente.", 100, "invalid", "app_invalid");
      sendCommandProgress(commandId, 100, "invalid", "appId ausente.", 0);
      return;
    }

    gActiveAppId = appId;
    gActiveAppConfig = configJson;
    gPrefs.putString("activeAppId", gActiveAppId);
    gPrefs.putString("activeAppConfig", gActiveAppConfig);

    postCommandAck(commandId, true, "Configuracao de app aplicada.", 100, "set-app-config");
    sendCommandProgress(commandId, 100, "set-app-config", "Configuracao aplicada.", 1);
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

// DOCS: docs/wiki/modules/firmware-matrixportal-s3.md#fluxo-de-execucao
void drawBars() {
  if (!gMatrixReady) {
    return;
  }

  setMatrixBrightness(gServerBrightness);
  clearMatrix();

  const uint16_t columnCount = (kBinsCount < kMatrixWidth) ? kBinsCount : kMatrixWidth;
  for (uint16_t x = 0; x < columnCount; x++) {
    const uint16_t binIndex = (x * kBinsCount) / columnCount;
    const uint8_t amplitude = gBins[binIndex];
    const uint8_t barHeight =
        static_cast<uint8_t>((static_cast<uint16_t>(amplitude) * kMatrixHalfHeight + 254u) / 255u);

    if (barHeight == 0) {
      continue;
    }

    const RgbColor color = rainbowColorForColumn(x, columnCount);
    for (uint8_t offset = 0; offset < barHeight; offset++) {
      const uint8_t topY = static_cast<uint8_t>((kMatrixHalfHeight - 1u) - offset);
      const uint8_t bottomY = static_cast<uint8_t>(kMatrixHalfHeight + offset);
      drawMatrixPixel(static_cast<uint8_t>(x), topY, color);

      if (bottomY < kMatrixHeight) {
        drawMatrixPixel(static_cast<uint8_t>(x), bottomY, color);
      }
    }
  }

  commitMatrixFrame();
}
}  // namespace

void setup() {
  Serial.begin(115200);
  if (strcmp(kSecurityProfile, "dev") == 0) {
    Serial.printf("MicaAudio firmware profile=%s security=%s\\n", kFirmwareProfile, kSecurityProfile);
  }
  gPrefs.begin("micaaudio", false);

  if (!initMatrixDisplay()) {
    Serial.println("Painel HUB75 indisponivel: exibicao de barras desativada.");
  }
  if (kTestLedPin >= 0) {
    pinMode(kTestLedPin, OUTPUT);
    digitalWrite(kTestLedPin, LOW);
  }

  gServerHost = gPrefs.getString("host", "");
  gServerPort = static_cast<uint16_t>(atoi(gPrefs.getString("port", "5272").c_str()));
  gDeviceId = gPrefs.getString("deviceId", "");
  gToken = gPrefs.getString("token", "");
  gActiveAppId = gPrefs.getString("activeAppId", "");
  gActiveAppName = gPrefs.getString("activeAppName", "");
  gActiveAppConfig = gPrefs.getString("activeAppConfig", "");

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
