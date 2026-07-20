# Sensor Anomaly Detection

Issue: https://github.com/cclautert/StarkAgro/issues/25

## Context

After each MQTT reading is persisted by the Worker, a statistical check compares the new value against the rolling mean ± 2.5 standard deviations of the last 50 non-anomalous readings for the same sensor. Readings outside that range are flagged as anomalies: the `ReadSensor.IsAnomaly` field is set to `true`, a `SensorAnomaly` document is saved to MongoDB, and the irrigation trend handler excludes flagged readings from its average calculation.

## Acceptance Criteria

| Criterion | Implementation |
|---|---|
| Anomaly detected < 1s after reading arrival | Synchronous MediatR send in MqttWorkerService immediately after CreateRead |
| Uses rolling 50-reading window | DetectSensorAnomalyHandler fetches last 50 non-anomalous reads |
| Mean ± 2.5 stddev threshold | Computed server-side in handler |
| Skips detection with < 10 samples | Early return in handler |
| Anomaly saved in dedicated collection | `sensor_anomalies` MongoDB collection |
| Anomalous reads excluded from irrigation trend | `!r.IsAnomaly` filter in GetIrrigationTrendHandler |
| GET endpoint lists anomalies per pivot | `GET /api/v1/pivot/{id}/anomalies` with paging |
| Dashboard shows badge of unacknowledged anomalies | `anomaly-badge` in irrigation-dashboard component |

## Affected Layers

- **Worker**: `MqttWorkerService` — send `DetectSensorAnomalyRequest` after `CreateReadRequest`
- **Handlers**: `DetectSensorAnomalyHandler`, `GetPivotAnomaliesHandler`
- **Entities**: `SensorAnomaly` (new), `ReadSensor` (add `IsAnomaly`)
- **DB Context**: `agpDBContext` — add `SensorAnomalies` collection with compound index
- **Controller**: `PivotController` — `GET {pivotId}/anomalies`
- **Angular**: `pivot.service.ts`, `irrigation-dashboard.component.*`

## New REST Endpoints

| Method | Route | Auth | Response |
|---|---|---|---|
| GET | `/api/v1/pivot/{pivotId}/anomalies` | Bearer JWT | `SensorAnomalyResponse[]` |

Query params: `pageSize` (default 20), `pageIndex` (default 0).

## Files Created

- `StarkAgroAPI/Models/Entities/SensorAnomaly.cs`
- `StarkAgroAPI/Domain/Commands/Requests/Anomalies/DetectSensorAnomalyRequest.cs`
- `StarkAgroAPI/Domain/Commands/Requests/Anomalies/GetPivotAnomaliesRequest.cs`
- `StarkAgroAPI/Domain/Commands/Responses/Anomalies/SensorAnomalyResponse.cs`
- `StarkAgroAPI/Domain/Handlers/Anomalies/DetectSensorAnomalyHandler.cs`
- `StarkAgroAPI/Domain/Handlers/Anomalies/GetPivotAnomaliesHandler.cs`

## Files Modified

- `StarkAgroAPI/Models/Entities/ReadSensor.cs` — add `bool IsAnomaly`
- `StarkAgroAPI/Models/agpDBContext.cs` — add `SensorAnomalies` collection + 2 indexes
- `StarkAgroAPI/Domain/Commands/Responses/Reads/CreateReadResponse.cs` — add `SensorId`, `UserId`
- `StarkAgroAPI/Domain/Handlers/Reads/CreateReadHandler.cs` — populate new response fields
- `StarkAgroAPI/Domain/Handlers/Pivots/GetIrrigationTrendHandler.cs` — filter `!IsAnomaly`
- `StarkAgroAPI/Controllers/PivotController.cs` — add `GET {pivotId}/anomalies`
- `StarkAgroWorker/Services/MqttWorkerService.cs` — send `DetectSensorAnomalyRequest` after create
- `StarkAgroUI/src/app/services/pivot.service.ts` — add `getAnomalies()`
- `StarkAgroUI/src/app/components/irrigation-dashboard/irrigation-dashboard.component.*` — anomaly badge

## MongoDB Changes

- **New collection**: `sensor_anomalies`
- **Compound index**: `{ userId: 1, sensorId: 1, date: -1 }` — supports pivot anomaly listing
- **Sparse index**: `{ acknowledged: 1 }` — supports future unread filtering
- **New field on read_sensors**: `isAnomaly: bool` (default `false`, backward-compatible)

## Tenant Isolation

`GetPivotAnomaliesHandler` filters `SensorAnomalies` by `UserId == _currentUser.UserId` and verifies the pivot belongs to the user before resolving sensor IDs. `DetectSensorAnomalyHandler` receives `UserId` from `CreateReadHandler` (sourced from `sensor.UserId`) — never from the client.
