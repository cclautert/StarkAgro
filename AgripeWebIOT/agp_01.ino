#include <ESP8266WiFi.h>
#include <ESP8266HTTPClient.h>
#include <WiFiClient.h>
#include <ArduinoJson.h>

// WiFi credentials
const char* ssid = "WIFI_SSID_REDACTED"; // Replace with your WiFi SSID
const char* password = "WIFI_PASS_REDACTED"; // Replace with your WiFi password

// API endpoint
const char* api_endpoint = "http://192.168.68.33:8080/v1/reads/add";

// MPX10DP sensor pin
#define SENSOR_PIN A0
#define SENSOR_PIN_ACTIVE 2

// Sleep time in microseconds (60 seconds)
#define SLEEP_TIME 60e6 //60 seconds

// Function to connect to WiFi
void connectToWiFi() {
  Serial.println("Connecting to WiFi...");
  WiFi.begin(ssid, password);
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    delay(500);
    Serial.print(".");
    attempts++;
  }
  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\nConnected to WiFi");
    Serial.print("IP address: ");
    Serial.println(WiFi.localIP());
    Serial.print("MAC address: ");
    Serial.println(WiFi.macAddress());
  } else {
    Serial.println("\nFailed to connect to WiFi");
  }
}

// Function to read MPX10DP sensor
float readMPX10DP() {
  int rawValue = analogRead(SENSOR_PIN); // Read raw ADC value (0-1023)
  float voltage = (rawValue / 1023.0) * 5.0; // Convert to voltage (assuming 5V reference)
  // MPX10DP sensitivity: 3.5 mV/kPa, mid-point at 2.5V
  float pressure_kPa = ((voltage - 2.5) / 0.0035); // Convert voltage to kPa
  
  Serial.println("KPA: " + String(pressure_kPa, 2)); // print with 2 decimal places

  return pressure_kPa;
}

// Function to send sensor data to API
void sendSensorData() {
  if (WiFi.status() == WL_CONNECTED) {
    WiFiClient client;
    HTTPClient http;
    
    // Prepare JSON payload
    StaticJsonDocument<200> doc;
    doc["code"] = WiFi.macAddress();
    doc["value"] = readMPX10DP();
    char buffer[256];
    serializeJson(doc, buffer);
    
    // Start HTTP POST request
    http.begin(client, api_endpoint);
    http.addHeader("Content-Type", "application/json");
    
    // Send POST request
    int httpCode = http.POST(buffer);
    
    // Check response
    if (httpCode > 0) {
      Serial.printf("HTTP POST response code: %d\n", httpCode);
      if (httpCode == HTTP_CODE_OK || httpCode == HTTP_CODE_CREATED) {
        String payload = http.getString();
        Serial.println("Response: " + payload);
      }
    } else {
      Serial.printf("HTTP POST failed, error: %s\n", http.errorToString(httpCode).c_str());
    }
    
    http.end();
  } else {
    Serial.println("WiFi not connected, skipping API call");
  }
}

void activateSensor() {
  digitalWrite(SENSOR_PIN_ACTIVE, HIGH);
}

void desactivateSensor() {
  digitalWrite(SENSOR_PIN_ACTIVE, LOW);
}

void setup() {
  pinMode(SENSOR_PIN_ACTIVE, OUTPUT);
  activateSensor();
  delay(1000);

  Serial.begin(115200);
  delay(100);

  // Connect to WiFi
  connectToWiFi();

  // Send sensor data to API
  sendSensorData();

  // Disconnect WiFi
  WiFi.disconnect(true);
  delay(1);

  desactivateSensor();

  // Enter deep sleep
  Serial.println("Entering deep sleep for 60 seconds...");
  ESP.deepSleep(SLEEP_TIME, WAKE_RF_DISABLED);
}

void loop() {
  // Empty loop, as ESP8266 will reset after deep sleep
}