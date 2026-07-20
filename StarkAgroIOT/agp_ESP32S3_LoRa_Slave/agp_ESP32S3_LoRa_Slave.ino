// ============================================================
// StarkAgro — ESP32-S3 LoRa Slave
// Sensor: SHT45 (temperatura + umidade) via I2C
// Radio:  LoRa SX1276 via SPI (RadioLib)
// Ciclo:  lê sensores → transmite payload → deep sleep 3h
// ============================================================

#include <Arduino.h>
#include <SPI.h>
#include <Wire.h>
#include <RadioLib.h>
#include <Adafruit_SHT4x.h>
#include "esp_sleep.h"
#include "esp_efuse.h"
#include "esp_efuse_table.h"

// ------------------------------------------------------------
// Configurações ajustáveis
// ------------------------------------------------------------
#define LORA_FREQUENCY   915.0   // MHz — banda ISM Brasil/Anatel
#define LORA_BANDWIDTH   125.0
#define LORA_SF          9
#define LORA_CR          7
#define LORA_SYNC_WORD   0xAB    // rede privada (igual no gateway)
#define SLEEP_SECONDS    (3UL * 3600UL)  // 3 horas
#define NUM_AMOSTRAS     3
#define INTERVALO_MS     1000

// ------------------------------------------------------------
// Pinagem ESP32-S3
// ------------------------------------------------------------
// SHT45 I2C
#define PIN_SDA   8
#define PIN_SCL   9

// LoRa SPI
#define PIN_NSS   10
#define PIN_SCK   12
#define PIN_MOSI  11
#define PIN_MISO  13
#define PIN_DIO0   2
#define PIN_RST   14

// ------------------------------------------------------------
// Objetos globais
// ------------------------------------------------------------
SPIClass loraSPI(HSPI);
SX1276 radio = new Module(PIN_NSS, PIN_DIO0, PIN_RST, RADIOLIB_NC, loraSPI);
Adafruit_SHT4x sht4x;

// ------------------------------------------------------------
// CRC-8 polinômio 0x31 (Dallas/Maxim)
// ------------------------------------------------------------
uint8_t crc8(const uint8_t* data, size_t len) {
    uint8_t crc = 0xFF;
    for (size_t i = 0; i < len; i++) {
        crc ^= data[i];
        for (uint8_t b = 0; b < 8; b++) {
            if (crc & 0x80) crc = (crc << 1) ^ 0x31;
            else            crc <<= 1;
        }
    }
    return crc;
}

// ------------------------------------------------------------
// Leitura SHT45: média de NUM_AMOSTRAS leituras
// Retorna true em sucesso; preenche temp e hum
// ------------------------------------------------------------
bool readSHT45(float& temp, float& hum) {
    float sumT = 0, sumH = 0;
    int ok = 0;
    for (int i = 0; i < NUM_AMOSTRAS; i++) {
        sensors_event_t eT, eH;
        if (sht4x.getEvent(&eH, &eT)) {
            sumT += eT.temperature;
            sumH += eH.relative_humidity;
            ok++;
        }
        if (i < NUM_AMOSTRAS - 1) delay(INTERVALO_MS);
    }
    if (ok == 0) return false;
    temp = sumT / ok;
    hum  = sumH / ok;
    return true;
}

// ------------------------------------------------------------
// Monta payload binário de 11 bytes
//   [0-5]  Device EUI (6 bytes do MAC)
//   [6-7]  Temperature int16 × 100, big-endian
//   [8-9]  Humidity   uint16 × 100, big-endian
//   [10]   CRC8 dos bytes 0-9
// ------------------------------------------------------------
void buildPayload(uint8_t* buf, const uint8_t* eui6, float temp, float hum) {
    memcpy(buf, eui6, 6);

    int16_t  tRaw = (int16_t)(temp * 100.0f);
    uint16_t hRaw = (uint16_t)(hum  * 100.0f);

    buf[6] = (tRaw >> 8) & 0xFF;
    buf[7] =  tRaw       & 0xFF;
    buf[8] = (hRaw >> 8) & 0xFF;
    buf[9] =  hRaw       & 0xFF;

    buf[10] = crc8(buf, 10);
}

// ------------------------------------------------------------
// Deep sleep
// ------------------------------------------------------------
void enterDeepSleep() {
    Serial.println("Entering deep sleep...");
    Serial.flush();
    esp_sleep_enable_timer_wakeup((uint64_t)SLEEP_SECONDS * 1000000ULL);
    esp_deep_sleep_start();
}

// ============================================================
// setup — toda a lógica (loop fica vazio com deep sleep)
// ============================================================
void setup() {
    Serial.begin(115200);
    delay(200);
    Serial.println("\n=== StarkAgro ESP32-S3 LoRa Slave ===");

    // --- Obter EUI (6 bytes do MAC base) ---
    uint8_t eui[6];
    uint8_t mac[8];
    esp_efuse_mac_get_default(mac);
    // O MAC base tem 6 bytes; usamos os 6 primeiros
    memcpy(eui, mac, 6);
    Serial.printf("EUI: %02X%02X%02X%02X%02X%02X\n",
                  eui[0], eui[1], eui[2], eui[3], eui[4], eui[5]);

    // --- Inicializar I2C + SHT45 ---
    Wire.begin(PIN_SDA, PIN_SCL);
    if (!sht4x.begin()) {
        Serial.println("ERROR: SHT45 not found. Check wiring.");
        enterDeepSleep();
    }
    sht4x.setPrecision(SHT4X_HIGH_PRECISION);
    Serial.println("SHT45 initialized.");

    // --- Ler sensor ---
    float temp, hum;
    if (!readSHT45(temp, hum)) {
        Serial.println("ERROR: Failed to read SHT45.");
        enterDeepSleep();
    }
    Serial.printf("Temperature: %.2f C  Humidity: %.2f %%\n", temp, hum);

    // --- Inicializar LoRa ---
    loraSPI.begin(PIN_SCK, PIN_MISO, PIN_MOSI, PIN_NSS);
    int state = radio.begin(LORA_FREQUENCY, LORA_BANDWIDTH, LORA_SF, LORA_CR);
    if (state != RADIOLIB_ERR_NONE) {
        Serial.printf("ERROR: LoRa init failed, code %d\n", state);
        enterDeepSleep();
    }
    radio.setSyncWord(LORA_SYNC_WORD);
    Serial.println("LoRa initialized.");

    // --- Montar e transmitir payload ---
    uint8_t payload[11];
    buildPayload(payload, eui, temp, hum);

    Serial.print("Payload (hex):");
    for (int i = 0; i < 11; i++) Serial.printf(" %02X", payload[i]);
    Serial.println();

    state = radio.transmit(payload, 11);
    if (state == RADIOLIB_ERR_NONE) {
        Serial.println("LoRa TX OK.");
    } else {
        Serial.printf("ERROR: LoRa TX failed, code %d\n", state);
    }

    // --- Desligar rádio e dormir ---
    radio.sleep();
    enterDeepSleep();
}

void loop() {
    // Nunca executado — deep sleep reinicia via setup()
}
