// ============================================================
// AgripeWeb — ESP8266 Soil Moisture Node
// WiFi:   WiFiManager captive portal (tzapu/WiFiManager >= 2.0.17)
// Config: LittleFS (/cfg.json) — never committed to git
// Sensor: MPX10DP on A0 (capacitive soil moisture)
//
// First boot / reprovisioning:
//   Hold GPIO0 (FLASH button) at boot to erase saved credentials
//   and open the "AgripeWeb-Setup" configuration portal.
// ============================================================

#include <ESP8266WiFi.h>
#include <ESP8266HTTPClient.h>
#include <WiFiClient.h>
#include <ArduinoJson.h>
#include <WiFiManager.h>    // tzapu/WiFiManager >= 2.0.17
#include <LittleFS.h>

static const char LOGIN_ENDPOINT[] = "http://www.agripeweb.com:8080/v1/Auth/LogIn";
static const char API_ENDPOINT[]   = "http://www.agripeweb.com:8080/v1/reads/add";

#define SENSOR_PIN          A0
#define SENSOR_PIN_ACTIVE   2
#define NUM_AMOSTRAS        5
#define INTERVALO_LEITURAS  1000
#define SLEEP_TIME          60e6   // 60 seconds in microseconds
#define PORTAL_TIMEOUT_S    180    // captive-portal auto-close
#define RESET_PIN           0      // GPIO0 — hold at boot to re-provision

#define CONFIG_FILE "/cfg.json"

// IoT API credentials loaded from LittleFS — never hardcoded
struct IotConfig {
  char email[64];
  char iotpass[64];
};

static IotConfig cfg;
static String    token      = "";
static bool      shouldSave = false;

// ── LittleFS config persistence ─────────────────────────────────

static bool loadConfig() {
  memset(&cfg, 0, sizeof(cfg));
  if (!LittleFS.begin()) {
    Serial.println("[cfg] LittleFS mount failed");
    return false;
  }
  if (!LittleFS.exists(CONFIG_FILE)) return false;
  File f = LittleFS.open(CONFIG_FILE, "r");
  if (!f) return false;
  StaticJsonDocument<256> doc;
  DeserializationError err = deserializeJson(doc, f);
  f.close();
  if (err) return false;
  strlcpy(cfg.email,   doc["email"]   | "", sizeof(cfg.email));
  strlcpy(cfg.iotpass, doc["iotpass"] | "", sizeof(cfg.iotpass));
  return (strlen(cfg.email) > 0 && strlen(cfg.iotpass) > 0);
}

static void persistConfig() {
  if (!LittleFS.begin()) { Serial.println("[cfg] Save: mount failed"); return; }
  File f = LittleFS.open(CONFIG_FILE, "w");
  if (!f) { Serial.println("[cfg] Save: open failed"); return; }
  StaticJsonDocument<256> doc;
  doc["email"]   = cfg.email;
  doc["iotpass"] = cfg.iotpass;
  serializeJson(doc, f);
  f.close();
  Serial.println("[cfg] Config saved");
}

// ── WiFiManager provisioning ─────────────────────────────────────

static void onSaveParams() { shouldSave = true; }

static void connectToWiFi() {
  // Hold GPIO0 at boot → erase WiFi + IoT config and re-open portal
  pinMode(RESET_PIN, INPUT_PULLUP);
  if (digitalRead(RESET_PIN) == LOW) {
    Serial.println("[wifi] Reset pin held — erasing saved config");
    WiFiManager wm;
    wm.resetSettings();
    if (LittleFS.begin()) LittleFS.remove(CONFIG_FILE);
    delay(500);
    ESP.restart();
  }

  WiFiManagerParameter paramEmail("email",   "IoT API Email",    cfg.email,   63);
  WiFiManagerParameter paramPass ("iotpass", "IoT API Password", cfg.iotpass, 63);

  WiFiManager wm;
  wm.setSaveParamsCallback(onSaveParams);
  wm.addParameter(&paramEmail);
  wm.addParameter(&paramPass);
  wm.setConfigPortalTimeout(PORTAL_TIMEOUT_S);

  Serial.println("[wifi] Connecting (portal: AgripeWeb-Setup if unconfigured)...");
  if (!wm.autoConnect("AgripeWeb-Setup")) {
    Serial.println("[wifi] Portal timed out — deep sleep 30 s, will retry");
    ESP.deepSleep(30e6);
  }

  if (shouldSave) {
    strlcpy(cfg.email,   paramEmail.getValue(), sizeof(cfg.email));
    strlcpy(cfg.iotpass, paramPass.getValue(),  sizeof(cfg.iotpass));
    persistConfig();
    shouldSave = false;
  }

  Serial.print("[wifi] Connected — IP: ");
  Serial.print(WiFi.localIP());
  Serial.print("  MAC: ");
  Serial.println(WiFi.macAddress());
}

// ── Sensor ───────────────────────────────────────────────────────

static float readMPX10DP() {
  float total = 0;
  for (int i = 0; i < NUM_AMOSTRAS; i++) {
    total += analogRead(SENSOR_PIN);
    delay(INTERVALO_LEITURAS);
  }
  return total / NUM_AMOSTRAS;
}

// ── Auth token ───────────────────────────────────────────────────

static bool getAuthToken() {
  if (WiFi.status() != WL_CONNECTED) return false;
  if (strlen(cfg.email) == 0 || strlen(cfg.iotpass) == 0) {
    Serial.println("[auth] IoT credentials not provisioned — open portal to configure");
    return false;
  }

  WiFiClient client;
  HTTPClient http;
  http.begin(client, LOGIN_ENDPOINT);
  http.addHeader("Content-Type", "application/json");

  StaticJsonDocument<256> req;
  req["email"]    = cfg.email;
  req["password"] = cfg.iotpass;
  String body;
  serializeJson(req, body);

  int code = http.POST(body);
  Serial.printf("[auth] Login HTTP %d\n", code);

  bool ok = false;
  if (code == HTTP_CODE_OK) {
    StaticJsonDocument<512> res;
    if (!deserializeJson(res, http.getString()) && res.containsKey("token")) {
      token = res["token"].as<String>();
      ok = true;
    }
  }
  http.end();
  return ok;
}

// ── Send sensor data ─────────────────────────────────────────────

static void sendSensorData() {
  if (WiFi.status() != WL_CONNECTED || token.length() == 0) {
    Serial.println("[send] Skip — no WiFi or no token");
    return;
  }

  StaticJsonDocument<200> doc;
  doc["code"]  = WiFi.macAddress();
  doc["value"] = readMPX10DP();
  String body;
  serializeJson(doc, body);

  WiFiClient client;
  HTTPClient http;
  http.begin(client, API_ENDPOINT);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("Authorization", "Bearer " + token);

  int code = http.POST(body);
  Serial.printf("[send] POST HTTP %d\n", code);
  if (code == HTTP_CODE_OK || code == HTTP_CODE_CREATED) {
    Serial.println("[send] OK: " + http.getString());
  } else {
    Serial.printf("[send] Error: %s\n", http.errorToString(code).c_str());
  }
  http.end();
}

// ── Arduino entry points ─────────────────────────────────────────

void setup() {
  pinMode(SENSOR_PIN_ACTIVE, OUTPUT);
  digitalWrite(SENSOR_PIN_ACTIVE, HIGH);
  delay(1000);

  Serial.begin(115200);
  delay(100);
  Serial.println("\n=== AgripeWeb ESP8266 Soil Node ===");

  loadConfig();
  connectToWiFi();

  if (getAuthToken()) {
    sendSensorData();
  } else {
    Serial.println("[main] Auth failed — skipping send");
  }

  WiFi.disconnect(true);
  delay(1);
  digitalWrite(SENSOR_PIN_ACTIVE, LOW);

  Serial.println("[main] Deep sleep 60 s");
  ESP.deepSleep(SLEEP_TIME, WAKE_RF_DISABLED);
}

void loop() {
  // Empty — all logic runs in setup() before deep sleep
}
