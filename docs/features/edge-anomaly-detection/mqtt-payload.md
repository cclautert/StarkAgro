# Edge Anomaly Detection — MQTT Payload Contract

## Overview

Firmware running on edge devices (ESP8266 / ESP32) can perform local anomaly detection and flag readings before sending them to the cloud. This document defines the MQTT and HTTP ingest payload contract for those edge-detected anomalies.

## MQTT Payload Schema

Topic: configured via `MqttSettings:Topic` (default `starkagro/reads`)

### Minimal (no edge detection)

```json
{
  "code": "SENSOR-A1",
  "value": 42.5
}
```

### With edge anomaly flag

```json
{
  "code": "SENSOR-A1",
  "value": 99.8,
  "isEdgeAnomaly": true,
  "edgeStats": {
    "mean": 45.2,
    "stdDev": 3.1,
    "windowSize": 20
  }
}
```

### Field descriptions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `code` | string | yes | Sensor code (matched case-insensitively against `Sensor.Code`) |
| `value` | number | yes | Raw sensor reading |
| `isEdgeAnomaly` | boolean | no | `true` when the firmware detected an anomaly on-device; defaults to `false` |
| `edgeStats` | object | no | Statistical context from the on-device sliding window |
| `edgeStats.mean` | number | no | Mean of the window used for detection |
| `edgeStats.stdDev` | number | no | Standard deviation of the window |
| `edgeStats.windowSize` | integer | no | Number of samples in the window |

## HTTP Ingest (`POST /v1/reads/Add`)

Same fields apply to the HTTP request body (`CreateReadRequest`):

```json
{
  "code": "SENSOR-A1",
  "value": 99.8,
  "isEdgeAnomaly": true,
  "edgeStats": {
    "mean": 45.2,
    "stdDev": 3.1,
    "windowSize": 20
  }
}
```

## Persistence

When a read is ingested with `isEdgeAnomaly = true`:

- `ReadSensor.IsEdgeAnomaly` is set to `true`
- `ReadSensor.EdgeDetectedAt` is set to `DateTime.UtcNow` (server receive time)

When `isEdgeAnomaly` is absent or `false`:

- `ReadSensor.IsEdgeAnomaly` = `false`
- `ReadSensor.EdgeDetectedAt` = `null`

`EdgeStats` fields are not persisted on `ReadSensor` — they are used for diagnostic context only and may be logged.

## Dashboard rendering

See [STA-188](/STA/issues/STA-188) for the Angular UI task that renders:
- **Field icon** when `isEdgeAnomaly = true` (anomaly detected on-device)
- **Cloud icon** when `isEdgeAnomaly = false` or absent (anomaly detected server-side)

## Related issues

- [STA-166](/STA/issues/STA-166) — Firmware edge schema
- [STA-170](/STA/issues/STA-170) — Backend persistence (this feature)
- [STA-188](/STA/issues/STA-188) — Dashboard icon (cloud vs field)
