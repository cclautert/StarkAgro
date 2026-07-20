Issue: https://github.com/cclautert/StarkAgro/issues/69

# Plan — IoT Device Read without JWT + Device Timestamp

## Context

`MqttWorkerService` currently calls `CreateReadHandler`, which requires an authenticated user via `ICurrentUserContext`. In the worker, `WorkerUserContext.UserId` returns `null`, so MQTT readings are never persisted. Additionally, `Date` is always `DateTime.UtcNow`, ignoring the `time` field from device uplinks. This issue creates a dedicated handler for device ingestion that resolves the user from the sensor's `Code` (which embeds the device `DevEUI`) and accepts an optional timestamp.

## Acceptance Criteria → Implementation Decisions

| Criterion | Decision |
|-----------|----------|
| Worker persists legacy `{code, value}` without JWT | `CreateDeviceReadHandler` does not inject `ICurrentUserContext` |
| `ReadSensor.Date` uses uplink `time` when provided | `CreateDeviceReadRequest.ReadAt` is `DateTime?`; handler: `request.ReadAt ?? DateTime.UtcNow` |
| Tenant isolation: `UserId` from `sensor.UserId` only | Handler looks up sensor by `Code` (case-insensitive); takes `sensor.UserId` — never from request |
| Uplink with unregistered `DevEUI` rejected/logged | If sensor not found → `_logger.LogWarning(...)` → return `null`; worker skips anomaly dispatch |
| `dotnet test` green | Tests in `StarkAgroAPI.Tests` and `StarkAgroWorker.Tests` |

## Affected Layers

- **New handler:** `StarkAgroAPI/Domain/Handlers/Reads/CreateDeviceReadHandler.cs`
- **New request DTO:** `StarkAgroAPI/Domain/Commands/Requests/Reads/CreateDeviceReadRequest.cs`
- **Modified worker:** `StarkAgroWorker/Services/MqttWorkerService.cs`
- **No controller** — device reads are not exposed via HTTP endpoint; handler is invoked only by the worker via MediatR

## New REST Endpoints

None — device ingestion is MQTT-only.

## Files to Create

| Path | Type |
|------|------|
| `StarkAgroAPI/Domain/Commands/Requests/Reads/CreateDeviceReadRequest.cs` | MediatR request DTO |
| `StarkAgroAPI/Domain/Handlers/Reads/CreateDeviceReadHandler.cs` | MediatR handler |
| `StarkAgroAPI.Tests/Domain/Handlers/Reads/CreateDeviceReadHandlerTests.cs` | xUnit tests |

## Files to Modify

| Path | Change |
|------|--------|
| `StarkAgroWorker/Services/MqttWorkerService.cs` | Replace `CreateReadRequest` → `CreateDeviceReadRequest`; propagate `message.ReadAt`; skip anomaly dispatch when handler returns null |

## MongoDB Changes

None — no new collections, fields, or indexes required.

## Tenant Isolation Plan

- Handler resolves `UserId` exclusively from `sensor.UserId` after looking up the sensor by `Code`.
- No `ICurrentUserContext` is injected — device reads have no JWT context.
- A `Code` that matches no sensor in the database is rejected and logged; nothing is persisted.
- `UserId` from the MQTT payload is never trusted.

## Handler Logic (CreateDeviceReadHandler)

```
1. Find sensor by Code (case-insensitive exact match)
2. If sensor == null → LogWarning → return null (worker handles gracefully)
3. Allocate Id via GetNextIdAsync(nameof(ReadSensor))
4. Persist ReadSensor { SensorId, UserId = sensor.UserId, Value, Date = request.ReadAt ?? UtcNow }
5. Return CreateReadResponse { Id, SensorId, UserId }
```

## MqttWorkerService Changes

- After deserialising message, use `CreateDeviceReadRequest` (not `CreateReadRequest`).
- Pass `message.ReadAt` (new optional field in the message DTO).
- Guard anomaly dispatch: `if (createResponse != null && createResponse.Id > 0)`.

## DI Registration

None — MediatR discovers handlers via assembly scan already configured in `ApiConfig.cs`.

## Risks & Flags

- `CreateReadHandler` throws `UnauthorizedAccessException` / `KeyNotFoundException`; `CreateDeviceReadHandler` returns null instead (worker context, no HTTP response to write errors to).
- `MqttReadMessage` in `MqttWorkerService` is a private sealed class — add `ReadAt` field there.
- The `time` field in the MQTT payload is ISO 8601 with offset; parse with `DateTime.TryParse(..., DateTimeStyles.AdjustToUniversal)` and store as UTC.

## Test Coverage Plan

### CreateDeviceReadHandlerTests (StarkAgroAPI.Tests)

| Test | Scenario |
|------|----------|
| `Handle_PersistsRead_WithSensorUserId` | Happy path — sensor found, read inserted with `sensor.UserId` |
| `Handle_UsesProvidedReadAt_WhenSupplied` | `ReadAt` set → `Date == request.ReadAt` |
| `Handle_DefaultsToUtcNow_WhenReadAtNull` | `ReadAt` null → `Date` close to `UtcNow` |
| `Handle_ReturnsNull_WhenSensorNotFound` | Sensor not in DB → returns null, no insert |
| `Handle_CallsGetNextIdAsync_BeforeInsert` | Verifies `GetNextIdAsync` called once |

### MqttWorkerServiceTests (StarkAgroWorker.Tests)

Existing tests continue to pass; add:

| Test | Scenario |
|------|----------|
| `ProcessMessage_SendsCreateDeviceReadRequest` | Verifies mediator.Send called with `CreateDeviceReadRequest` |
| `ProcessMessage_SkipsAnomalyDispatch_WhenHandlerReturnsNull` | Response null → `DetectSensorAnomalyRequest` not sent |

## Verification

```bash
dotnet build StarkAgroAPI/StarkAgroAPI.csproj
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj
dotnet test StarkAgroWorker.Tests/StarkAgroWorker.Tests.csproj
```
