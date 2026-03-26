// ============================================================
// AgripeWeb — ESP32 LoRa Gateway  (FreeRTOS Multi-Task)
// Radio:    LoRa SX1276 via SPI (RadioLib)
// Conexão:  WiFi (primário) → 4G SIM7600 (fallback)
// Função:   recebe pacotes LoRa dos slaves, repassa para a API
//
// Arquitetura de tasks:
//   Core 1, pri 10 — LoRa RX Task  (ISR → semáforo → readData)
//   Core 0, pri  6 — Parser Task   (parse + dedup → xSendQueue)
//   Core 0, pri  4 — Network Task  (HTTP POST com retry/backoff)
// ============================================================

// IMPORTANTE: definir ANTES do include do TinyGSM
#define TINY_GSM_MODEM_SIM7600

#include <Arduino.h>
#include <SPI.h>
#include <RadioLib.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <TinyGsmClient.h>
#include <ArduinoJson.h>
#include <time.h>

// ------------------------------------------------------------
// Configurações ajustáveis
// ------------------------------------------------------------
#define WIFI_SSID           "SSID_AQUI"
#define WIFI_PASSWORD       "SENHA_AQUI"
#define APN                 "claro.com.br"       // APN da operadora
#define GSM_USER            ""
#define GSM_PASS            ""
#define LOGIN_ENDPOINT      "http://www.agripeweb.com:8080/v1/Auth/LogIn"
#define API_ENDPOINT        "http://www.agripeweb.com:8080/v1/reads/add"
#define TOKEN_TTL_SECONDS   (8UL * 3600UL)       // JWT expira em 8h
#define LORA_FREQUENCY      915.0
#define LORA_BANDWIDTH      125.0
#define LORA_SF             9
#define LORA_CR             7
#define LORA_SYNC_WORD      0xAB                 // deve ser igual ao slave

// Credenciais IoT (device dedicado na API)
#define IOT_EMAIL           "IOT_EMAIL_REDACTED"
#define IOT_PASSWORD        "CHANGE_ME"

// ------------------------------------------------------------
// Pinagem ESP32
// ------------------------------------------------------------
#define PIN_NSS   5
#define PIN_SCK   18
#define PIN_MOSI  23
#define PIN_MISO  19
#define PIN_DIO0  26
#define PIN_RST   14

// UART para módulo 4G (Serial2)
#define GSM_TX    17
#define GSM_RX    16
#define GSM_BAUD  115200

// ------------------------------------------------------------
// Constantes FreeRTOS / protocolo
// ------------------------------------------------------------
#define DEDUP_TABLE_SIZE  16
#define DEDUP_WINDOW_MS   30000UL
#define NET_MAX_RETRIES   3
#define NET_RETRY_BASE_MS 2000UL

// ------------------------------------------------------------
// Tipos de mensagem entre tasks
// ------------------------------------------------------------
typedef struct {
    uint8_t buf[11];
    int     rssi;
} LoraPacket_t;

typedef struct {
    char  eui[13];   // 12 hex chars + '\0'
    float temp;
    float hum;
} SensorReading_t;

typedef struct {
    char     eui[13];
    uint32_t last_seen_ticks;
} DedupEntry_t;

// ------------------------------------------------------------
// Token persistido em RAM RTC (sobrevive deep sleep)
// ------------------------------------------------------------
RTC_DATA_ATTR char   rtcToken[512] = "";
RTC_DATA_ATTR time_t rtcTokenTime  = 0;

// ------------------------------------------------------------
// Objetos de hardware (instanciados uma vez)
// ------------------------------------------------------------
SX1276 radio = new Module(PIN_NSS, PIN_DIO0, PIN_RST, RADIOLIB_NC);

HardwareSerial SerialGSM(2);
TinyGsm        modem(SerialGSM);
TinyGsmClient  gsmClient(modem);

bool useGSM = false;   // true quando WiFi não disponível

// ------------------------------------------------------------
// Handles FreeRTOS
// ------------------------------------------------------------
static QueueHandle_t     xLoRaQueue;    // LoraPacket_t,    profundidade 10
static QueueHandle_t     xSendQueue;    // SensorReading_t, profundidade 20
static SemaphoreHandle_t xPktSem;       // binário: ISR → LoRa RX Task
static SemaphoreHandle_t xRadioMutex;   // protege objeto radio
static SemaphoreHandle_t xNetMutex;     // protege rtcToken, useGSM, gsmClient
static TaskHandle_t      hLoRaTask, hParserTask, hNetTask;

// ============================================================
// ISR — DIO0 rising edge (IRAM_ATTR obrigatório)
// ============================================================
IRAM_ATTR void onDio0Rise() {
    BaseType_t hp = pdFALSE;
    xSemaphoreGiveFromISR(xPktSem, &hp);
    portYIELD_FROM_ISR(hp);
}

// ============================================================
// CRC-8 polinômio 0x31 (mesmo algoritmo do slave)
// ============================================================
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

// ============================================================
// Converte 6 bytes EUI para string hex uppercase (sem separador)
// Escreve diretamente em out[13] — sem alocação de heap
// ============================================================
void euiToString(const uint8_t* eui, char out[13]) {
    snprintf(out, 13,
             "%02X%02X%02X%02X%02X%02X",
             eui[0], eui[1], eui[2], eui[3], eui[4], eui[5]);
}

// ============================================================
// Parsear payload de 11 bytes — buffers na stack, sem String
//   Retorna false se tamanho < 11 ou CRC inválido
// ============================================================
bool parsePayload(const uint8_t* buf, int len,
                  char eui_out[13], float& temp, float& hum) {
    if (len < 11) {
        Serial.println("ERROR: payload too short");
        return false;
    }
    uint8_t expectedCrc = crc8(buf, 10);
    if (buf[10] != expectedCrc) {
        Serial.printf("ERROR: CRC mismatch (got 0x%02X, expected 0x%02X)\n",
                      buf[10], expectedCrc);
        return false;
    }
    euiToString(buf, eui_out);

    int16_t  tRaw = ((int16_t)buf[6]  << 8) | buf[7];
    uint16_t hRaw = ((uint16_t)buf[8] << 8) | buf[9];
    temp = (float)tRaw / 100.0f;
    hum  = (float)hRaw / 100.0f;
    return true;
}

// ============================================================
// WiFi
// ============================================================
bool connectWiFi() {
    Serial.printf("Connecting to WiFi SSID: %s\n", WIFI_SSID);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    for (int i = 0; i < 20 && WiFi.status() != WL_CONNECTED; i++) {
        delay(500);
        Serial.print(".");
    }
    Serial.println();
    if (WiFi.status() == WL_CONNECTED) {
        Serial.print("WiFi connected. IP: ");
        Serial.println(WiFi.localIP());
        return true;
    }
    Serial.println("WiFi connection failed.");
    return false;
}

// ============================================================
// 4G GSM (TinyGSM SIM7600)
// ============================================================
bool connectGSM() {
    Serial.println("Initializing 4G modem (SIM7600)...");
    SerialGSM.begin(GSM_BAUD, SERIAL_8N1, GSM_RX, GSM_TX);
    delay(3000);
    if (!modem.begin()) {
        Serial.println("ERROR: Modem init failed.");
        return false;
    }
    Serial.print("Modem info: ");
    Serial.println(modem.getModemInfo());

    Serial.printf("Connecting to APN: %s\n", APN);
    if (!modem.gprsConnect(APN, GSM_USER, GSM_PASS)) {
        Serial.println("ERROR: GPRS connection failed.");
        return false;
    }
    Serial.println("4G connected.");
    return true;
}

// ============================================================
// Verificar se token precisa ser renovado
// Chamada somente da Network Task (já sob xNetMutex)
// ============================================================
bool tokenExpired() {
    if (strlen(rtcToken) == 0) return true;
    time_t now = time(nullptr);
    return (now - rtcTokenTime) >= (time_t)TOKEN_TTL_SECONDS;
}

// ============================================================
// Autenticação na API — armazena token em RAM RTC
// Chamada somente da Network Task (já sob xNetMutex)
// ============================================================
bool getAuthToken() {
    Serial.println("Requesting auth token...");

    StaticJsonDocument<256> reqDoc;
    reqDoc["email"]    = IOT_EMAIL;
    reqDoc["password"] = IOT_PASSWORD;
    String body;
    serializeJson(reqDoc, body);

    int    httpCode = -1;
    String response = "";

    if (!useGSM) {
        HTTPClient http;
        http.begin(LOGIN_ENDPOINT);
        http.addHeader("Content-Type", "application/json");
        httpCode = http.POST(body);
        if (httpCode > 0) response = http.getString();
        http.end();
    } else {
        // TinyGSM: HTTP manual via TCP
        if (!gsmClient.connect("www.agripeweb.com", 8080)) {
            Serial.println("ERROR: GSM TCP connect failed for login.");
            return false;
        }
        String req = "POST /v1/Auth/LogIn HTTP/1.1\r\n"
                     "Host: www.agripeweb.com:8080\r\n"
                     "Content-Type: application/json\r\n"
                     "Content-Length: " + String(body.length()) + "\r\n"
                     "Connection: close\r\n\r\n" + body;
        gsmClient.print(req);
        unsigned long t = millis();
        while (gsmClient.connected() && millis() - t < 10000) {
            while (gsmClient.available()) response += (char)gsmClient.read();
        }
        gsmClient.stop();
        int sep = response.indexOf("\r\n\r\n");
        if (sep >= 0) response = response.substring(sep + 4);
        httpCode = 200;
    }

    Serial.printf("Login response code: %d\n", httpCode);
    if (httpCode == 200) {
        StaticJsonDocument<512> resDoc;
        if (!deserializeJson(resDoc, response) && resDoc.containsKey("token")) {
            String tok = resDoc["token"].as<String>();
            tok.toCharArray(rtcToken, sizeof(rtcToken));
            rtcTokenTime = time(nullptr);
            Serial.println("Token obtained.");
            return true;
        }
    }
    Serial.println("ERROR: Failed to obtain token.");
    return false;
}

// ============================================================
// Enviar leitura para a API
// Trata 401 com reautenticação automática (uma tentativa)
// Retry externo (até NET_MAX_RETRIES) feito pela Network Task
// Chamada somente da Network Task (já sob xNetMutex)
// ============================================================
bool sendReading(const char* code, float value) {
    StaticJsonDocument<128> doc;
    doc["code"]  = code;
    doc["value"] = value;
    String body;
    serializeJson(doc, body);

    Serial.printf("POST %s  code=%s  value=%.2f\n",
                  API_ENDPOINT, code, value);

    for (int attempt = 0; attempt < 2; attempt++) {
        String bearer = "Bearer ";
        bearer += rtcToken;
        int    httpCode = -1;
        String response = "";

        if (!useGSM) {
            HTTPClient http;
            http.begin(API_ENDPOINT);
            http.addHeader("Content-Type", "application/json");
            http.addHeader("Authorization", bearer);
            httpCode = http.POST(body);
            if (httpCode > 0) response = http.getString();
            http.end();
        } else {
            if (!gsmClient.connect("www.agripeweb.com", 8080)) {
                Serial.println("ERROR: GSM TCP connect failed for send.");
                return false;
            }
            String req = "POST /v1/reads/add HTTP/1.1\r\n"
                         "Host: www.agripeweb.com:8080\r\n"
                         "Content-Type: application/json\r\n"
                         "Authorization: " + bearer + "\r\n"
                         "Content-Length: " + String(body.length()) + "\r\n"
                         "Connection: close\r\n\r\n" + body;
            gsmClient.print(req);
            unsigned long t = millis();
            while (gsmClient.connected() && millis() - t < 10000) {
                while (gsmClient.available()) response += (char)gsmClient.read();
            }
            gsmClient.stop();
            int spaceIdx = response.indexOf(' ');
            if (spaceIdx > 0) {
                httpCode = response.substring(spaceIdx + 1, spaceIdx + 4).toInt();
            }
        }

        Serial.printf("Response code: %d\n", httpCode);

        if (httpCode == 200 || httpCode == 201) {
            Serial.println("Reading sent OK.");
            return true;
        }
        if (httpCode == 401 && attempt == 0) {
            Serial.println("401 Unauthorized — refreshing token...");
            if (!getAuthToken()) return false;
            continue;
        }
        Serial.printf("ERROR: unexpected response code %d\n", httpCode);
        return false;
    }
    return false;
}

// ============================================================
// Task 1 — LoRa RX  (Core 1, Prioridade 10, Stack 3072)
// ============================================================
void taskLoRaRX(void* /*pvParameters*/) {
    Serial.println("[LoRaRX] Task started on Core 1.");

    for (;;) {
        // Bloqueia até a ISR sinalizar via semáforo binário
        xSemaphoreTake(xPktSem, portMAX_DELAY);

        LoraPacket_t pkt;
        bool ok = false;

        // Acessa o rádio sob mutex
        xSemaphoreTake(xRadioMutex, portMAX_DELAY);
        {
            int state = radio.readData(pkt.buf, 11);
            radio.startReceive();   // rearma o mais cedo possível

            if (state == RADIOLIB_ERR_NONE) {
                pkt.rssi = (int)radio.getRSSI();
                ok = true;
            } else {
                Serial.printf("[LoRaRX] readData error, code %d\n", state);
            }
        }
        xSemaphoreGive(xRadioMutex);

        if (!ok) continue;

        // Valida CRC antes de enfileirar
        if (crc8(pkt.buf, 10) != pkt.buf[10]) {
            uint8_t expected = crc8(pkt.buf, 10);
            Serial.printf("[LoRaRX] CRC error (got 0x%02X, expected 0x%02X) — drop\n",
                          pkt.buf[10], expected);
            continue;
        }

        Serial.printf("[LoRaRX] Packet OK, RSSI %d dBm — enqueuing\n", pkt.rssi);

        if (xQueueSend(xLoRaQueue, &pkt, 0) == errQUEUE_FULL) {
            Serial.println("[LoRaRX] WARN: xLoRaQueue full — packet dropped");
        }
    }
}

// ============================================================
// Task 2 — Parser  (Core 0, Prioridade 6, Stack 2048)
// ============================================================
void taskParser(void* /*pvParameters*/) {
    Serial.println("[Parser] Task started on Core 0.");

    // Tabela de deduplicação (circular, tamanho fixo na stack)
    DedupEntry_t dedupTable[DEDUP_TABLE_SIZE] = {};
    uint8_t      dedupIdx = 0;

    LoraPacket_t pkt;

    for (;;) {
        xQueueReceive(xLoRaQueue, &pkt, portMAX_DELAY);

        SensorReading_t reading;
        float temp, hum;
        if (!parsePayload(pkt.buf, 11, reading.eui, temp, hum)) continue;

        reading.temp = temp;
        reading.hum  = hum;

        // Verificar deduplicação por EUI dentro da janela DEDUP_WINDOW_MS
        uint32_t now = (uint32_t)xTaskGetTickCount() * portTICK_PERIOD_MS;
        bool duplicate = false;
        for (int i = 0; i < DEDUP_TABLE_SIZE; i++) {
            if (strncmp(dedupTable[i].eui, reading.eui, 12) == 0) {
                uint32_t elapsed = now - dedupTable[i].last_seen_ticks;
                if (elapsed < DEDUP_WINDOW_MS) {
                    Serial.printf("[Parser] Duplicate EUI %s (%.1f s ago) — drop\n",
                                  reading.eui, elapsed / 1000.0f);
                    duplicate = true;
                }
                // Atualiza timestamp mesmo se duplicado (janela deslizante)
                dedupTable[i].last_seen_ticks = now;
                break;
            }
        }
        if (duplicate) continue;

        // Registrar nova entrada (substituição circular)
        strncpy(dedupTable[dedupIdx].eui, reading.eui, 12);
        dedupTable[dedupIdx].eui[12]          = '\0';
        dedupTable[dedupIdx].last_seen_ticks  = now;
        dedupIdx = (dedupIdx + 1) % DEDUP_TABLE_SIZE;

        Serial.printf("[Parser] EUI: %s  Temp: %.2f C  Hum: %.2f %%\n",
                      reading.eui, reading.temp, reading.hum);

        if (xQueueSend(xSendQueue, &reading, 0) == errQUEUE_FULL) {
            Serial.println("[Parser] WARN: xSendQueue full — reading dropped");
        }
    }
}

// ============================================================
// Task 3 — Network  (Core 0, Prioridade 4, Stack 8192)
// ============================================================
void taskNetwork(void* /*pvParameters*/) {
    Serial.println("[Network] Task started on Core 0.");

    SensorReading_t reading;

    for (;;) {
        xQueueReceive(xSendQueue, &reading, portMAX_DELAY);

        xSemaphoreTake(xNetMutex, portMAX_DELAY);
        {
            // Renovar token se necessário
            if (tokenExpired()) {
                if (!getAuthToken()) {
                    Serial.println("[Network] ERROR: Cannot obtain token — dropping reading.");
                    xSemaphoreGive(xNetMutex);
                    continue;
                }
            }

            // Montar códigos EUI_T e EUI_H
            char codeT[17], codeH[17];
            snprintf(codeT, sizeof(codeT), "%s_T", reading.eui);
            snprintf(codeH, sizeof(codeH), "%s_H", reading.eui);

            // Enviar temperatura com retry + backoff exponencial
            bool sentT = false;
            for (int attempt = 0; attempt < NET_MAX_RETRIES && !sentT; attempt++) {
                if (attempt > 0) {
                    uint32_t waitMs = NET_RETRY_BASE_MS * (1u << (attempt - 1)); // 2s, 4s, 8s
                    Serial.printf("[Network] Retry %d in %lu ms...\n", attempt, waitMs);
                    vTaskDelay(pdMS_TO_TICKS(waitMs));

                    // Tentar reconexão de rede se necessário
                    if (!useGSM && WiFi.status() != WL_CONNECTED) {
                        Serial.println("[Network] WiFi down — reconnecting...");
                        connectWiFi();
                    } else if (useGSM && !modem.isGprsConnected()) {
                        Serial.println("[Network] GSM down — reconnecting...");
                        if (!modem.gprsConnect(APN, GSM_USER, GSM_PASS)) {
                            Serial.println("[Network] GPRS reconnect failed — modem restart...");
                            modem.restart();
                            modem.gprsConnect(APN, GSM_USER, GSM_PASS);
                        }
                    }
                }
                sentT = sendReading(codeT, reading.temp);
            }
            if (!sentT) {
                Serial.printf("[Network] ERROR: Failed to send %s after %d attempts — drop\n",
                              codeT, NET_MAX_RETRIES);
            }

            // Enviar umidade com retry + backoff exponencial
            bool sentH = false;
            for (int attempt = 0; attempt < NET_MAX_RETRIES && !sentH; attempt++) {
                if (attempt > 0) {
                    uint32_t waitMs = NET_RETRY_BASE_MS * (1u << (attempt - 1));
                    Serial.printf("[Network] Retry %d in %lu ms...\n", attempt, waitMs);
                    vTaskDelay(pdMS_TO_TICKS(waitMs));

                    if (!useGSM && WiFi.status() != WL_CONNECTED) {
                        Serial.println("[Network] WiFi down — reconnecting...");
                        connectWiFi();
                    } else if (useGSM && !modem.isGprsConnected()) {
                        Serial.println("[Network] GSM down — reconnecting...");
                        if (!modem.gprsConnect(APN, GSM_USER, GSM_PASS)) {
                            Serial.println("[Network] GPRS reconnect failed — modem restart...");
                            modem.restart();
                            modem.gprsConnect(APN, GSM_USER, GSM_PASS);
                        }
                    }
                }
                sentH = sendReading(codeH, reading.hum);
            }
            if (!sentH) {
                Serial.printf("[Network] ERROR: Failed to send %s after %d attempts — drop\n",
                              codeH, NET_MAX_RETRIES);
            }
        }
        xSemaphoreGive(xNetMutex);
    }
}

// ============================================================
// setup — inicialização sequencial, depois cria as 3 tasks
// ============================================================
void setup() {
    Serial.begin(115200);
    delay(200);
    Serial.println("\n=== AgripeWeb ESP32 LoRa Gateway (FreeRTOS) ===");

    // 1. Inicializar LoRa
    int state = radio.begin(LORA_FREQUENCY, LORA_BANDWIDTH, LORA_SF, LORA_CR);
    if (state != RADIOLIB_ERR_NONE) {
        Serial.printf("ERROR: LoRa init failed, code %d — halting\n", state);
        while (true) delay(1000);
    }
    radio.setSyncWord(LORA_SYNC_WORD);
    Serial.println("LoRa initialized.");

    // 2. Conexão de rede (bloqueante — OK antes das tasks)
    if (connectWiFi()) {
        useGSM = false;
        // 3a. Sincronizar horário via NTP
        configTime(0, 0, "pool.ntp.org", "time.nist.gov");
        Serial.println("Syncing NTP time...");
        int tries = 0;
        while (time(nullptr) < 1000000 && tries++ < 20) delay(500);
        Serial.printf("Time synced: %lu\n", (unsigned long)time(nullptr));
    } else {
        useGSM = connectGSM();
        if (useGSM) {
            // 3b. Obter horário pelo modem (para TTL aproximado do token)
            int yr, mo, da, hr, mi, se;
            float tz;
            modem.getNetworkTime(&yr, &mo, &da, &hr, &mi, &se, &tz);
            Serial.printf("Modem time: %04d-%02d-%02d %02d:%02d:%02d\n",
                          yr, mo, da, hr, mi, se);
        } else {
            Serial.println("ERROR: No network available — halting");
            while (true) delay(1000);
        }
    }

    // 4. Criar primitivas FreeRTOS
    xPktSem     = xSemaphoreCreateBinary();
    xRadioMutex = xSemaphoreCreateMutex();
    xNetMutex   = xSemaphoreCreateMutex();
    xLoRaQueue  = xQueueCreate(10,  sizeof(LoraPacket_t));
    xSendQueue  = xQueueCreate(20,  sizeof(SensorReading_t));

    if (!xPktSem || !xRadioMutex || !xNetMutex || !xLoRaQueue || !xSendQueue) {
        Serial.println("ERROR: FreeRTOS primitives creation failed — halting");
        while (true) delay(1000);
    }
    Serial.println("FreeRTOS primitives created.");

    // 5. Configurar ISR e iniciar recepção LoRa
    radio.setDio0Action(onDio0Rise, RISING);
    state = radio.startReceive();
    if (state != RADIOLIB_ERR_NONE) {
        Serial.printf("ERROR: startReceive failed, code %d — halting\n", state);
        while (true) delay(1000);
    }
    Serial.println("LoRa RX armed. Waiting for packets...");

    // 6. Criar as 3 tasks
    BaseType_t r1 = xTaskCreatePinnedToCore(
        taskLoRaRX, "LoRaRX",   3072, NULL, 10, &hLoRaTask,   1);
    BaseType_t r2 = xTaskCreatePinnedToCore(
        taskParser,  "Parser",   2048, NULL,  6, &hParserTask, 0);
    BaseType_t r3 = xTaskCreatePinnedToCore(
        taskNetwork, "Network",  8192, NULL,  4, &hNetTask,    0);

    if (r1 != pdPASS || r2 != pdPASS || r3 != pdPASS) {
        Serial.println("ERROR: Task creation failed — halting");
        while (true) delay(1000);
    }
    Serial.println("Tasks started: LoRaRX(C1/P10) Parser(C0/P6) Network(C0/P4)");

    // 7. Deletar a task do Arduino loop (não é mais necessária)
    vTaskDelete(NULL);
}

// ============================================================
// loop — nunca executado (task deletada no setup)
// ============================================================
void loop() {
    // Não utilizado — toda a lógica está nas tasks FreeRTOS
}
