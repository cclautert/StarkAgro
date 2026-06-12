Issue: https://github.com/cclautert/AgripeWeb/issues/71

# Plan — Sensor.MetricType e Telemetria Agrupada por Dispositivo

## Context

Com a ingestão LoRaWAN, um dispositivo SN50 gera 3 sensores por quadrante (`_H`, `_T`, `_B`). O `GetReadByPivotIdHandler` e o `GetIrrigationTrendHandler` calculam médias de **todos** os sensores do pivot, misturando umidade (%), temperatura (°C) e tensão (V). Este issue adiciona `MetricType` ao `Sensor`, filtra os handlers de irrigação apenas para `Humidity`, e expõe um endpoint de telemetria agrupado por DevEUI.

## Acceptance Criteria → Implementation Decisions

| Criterion | Decision |
|-----------|----------|
| Médias de irrigação não misturam C/V com % | `GetReadByPivotIdHandler` e `GetIrrigationTrendHandler` filtram sensores por `MetricType == Humidity` com OR para campo ausente (legado) |
| Endpoint de telemetria retorna última leitura por métrica por dispositivo | `GET /api/v1/sensor/telemetry?pivotId={id}` via `GetSensorTelemetryHandler` |
| Sensores legados (sem MetricType) continuam como Humidity | Default `MetricType.Humidity = 0`; filtro MongoDB inclui documentos sem o campo |
| Testes de handler cobrem o filtro por MetricType | Testes com mix de sensores Humidity/Temperature/Battery |
| `dotnet test` verde | Full suite passa |

**Nota de rota:** O controller existente usa `[Route("v1/sensor")]` (singular). O endpoint de telemetria será `GET /api/v1/sensor/telemetry` para manter consistência; a issue sugeria "sensors" mas o controller usa "sensor". A UI (#72) consumirá a rota correta.

## Affected Layers

- **Entity:** `Sensor` — novo campo `MetricType`
- **Enum novo:** `MetricType` (Humidity=0, Temperature=1, Battery=2)
- **Handlers modificados:** `GetReadByPivotIdHandler`, `GetIrrigationTrendHandler`
- **Handler novo:** `GetSensorTelemetryHandler`
- **Request/Response novos:** `GetSensorTelemetryRequest`, `SensorTelemetryResponse`
- **Requests modificados:** `CreateSensorRequest`, `EditSensorRequest`
- **Handlers modificados:** `CreateSensorHandler`, `EditSensorHandler`
- **Controller modificado:** `SensorController` — novo endpoint `telemetry`

## New REST Endpoints

### GET /api/v1/sensor/telemetry

| Campo | Valor |
|-------|-------|
| Método | GET |
| Rota | `/api/v1/sensor/telemetry` |
| Auth | `[Authorize]` — Bearer JWT |
| Query params | `pivotId` (int, obrigatório) |
| Response | `IList<SensorTelemetryResponse>` |

**Response item:**
```json
{
  "quadrante": 1,
  "deviceEui": "A84041691D5F1794",
  "humidity": 75.0,
  "temperature": 22.7,
  "batteryVoltage": 3.582,
  "batteryPercent": 97.0,
  "readAt": "2026-06-11T23:29:02Z"
}
```

## Files to Create

| Path | Type |
|------|------|
| `AgripeWebAPI/Models/Entities/MetricType.cs` | Enum |
| `AgripeWebAPI/Domain/Commands/Requests/Sensors/GetSensorTelemetryRequest.cs` | MediatR request |
| `AgripeWebAPI/Domain/Commands/Responses/Sensors/SensorTelemetryResponse.cs` | Response DTO |
| `AgripeWebAPI/Domain/Handlers/Sensors/GetSensorTelemetryHandler.cs` | Handler |
| `AgripeWebAPI.Tests/Domain/Handlers/Sensors/GetSensorTelemetryHandlerTests.cs` | xUnit tests |

## Files to Modify

| Path | Change |
|------|--------|
| `AgripeWebAPI/Models/Entities/Sensor.cs` | Adicionar `public MetricType MetricType { get; set; } = MetricType.Humidity;` |
| `AgripeWebAPI/Domain/Commands/Requests/Sensors/CreateSensorRequest.cs` | Adicionar `MetricType MetricType { get; set; }` |
| `AgripeWebAPI/Domain/Commands/Requests/Sensors/EditSensorRequest.cs` | Adicionar `MetricType MetricType { get; set; }` |
| `AgripeWebAPI/Domain/Handlers/Sensors/CreateSensorHandler.cs` | Persistir `MetricType = request.MetricType` |
| `AgripeWebAPI/Domain/Handlers/Sensors/EditSensorHandler.cs` | Atualizar `sensor.MetricType = request.MetricType` |
| `AgripeWebAPI/Domain/Handlers/Reads/GetReadByPivotIdHandler.cs` | Filtrar sensores por Humidity (OR campo ausente) |
| `AgripeWebAPI/Domain/Handlers/Pivots/GetIrrigationTrendHandler.cs` | Filtrar sensores por Humidity (OR campo ausente) |
| `AgripeWebAPI/Controllers/SensorController.cs` | Adicionar endpoint `[Route("telemetry")][HttpGet]` |

## MongoDB Changes

- **Campo novo em `Sensor`:** `MetricType` (int, default 0 = Humidity)
- Nenhuma nova coleção; nenhum índice novo necessário
- Backward-compatible: documentos existentes sem o campo deserializam como `Humidity (0)`

**Atenção — filtro MongoDB para Humidity:**

O filtro `s.MetricType == MetricType.Humidity` não casa com documentos que não têm o campo `MetricType`. Usar filtro composto:
```csharp
Builders<Sensor>.Filter.Or(
    Builders<Sensor>.Filter.Eq(s => s.MetricType, MetricType.Humidity),
    Builders<Sensor>.Filter.Exists(nameof(Sensor.MetricType), false)
)
```

## Tenant Isolation Plan

- `GetSensorTelemetryHandler`: filtra sensores por `UserId == _currentUser.UserId` (obrigatório)
- `GetReadByPivotIdHandler` e `GetIrrigationTrendHandler`: já filtram por `UserId` — apenas adicionar filtro de MetricType
- `CreateSensorHandler` / `EditSensorHandler`: `UserId` já vem de `_currentUser.UserId`

## Telemetry Handler Algorithm

```
1. Buscar todos os sensores do pivot (UserId + PivotId)
2. Filtrar sensores cujo Code termina em _H, _T ou _B
3. Agrupar por prefixo do DevEUI (Code sem os últimos 2 chars)
4. Para cada grupo:
   a. _H sensor → buscar última leitura → humidity
   b. _T sensor → buscar última leitura → temperature
   c. _B sensor → buscar última leitura → batteryVoltage; calcular batteryPercent
   d. readAt = max(readAt de todas as leituras não-nulas)
   e. quadrante = sensor._H.Quadrante (ou o do sensor disponível)
5. Retornar lista ordenada por quadrante
```

**BatV → % (hardcoded):**
```csharp
const decimal BatMin = 3.0m;
const decimal BatMax = 3.6m;
decimal percent = (batV - BatMin) / (BatMax - BatMin) * 100m;
batteryPercent = Math.Clamp(Math.Round(percent, 1), 0m, 100m);
```

## DI Registration

Nenhuma — MediatR descobre handlers automaticamente.

## Risks & Flags

- O filtro MongoDB para legado deve usar `Filter.Exists(field, false)` — campo ausente no documento ≠ campo com valor 0
- `SensorSummary` em `GetReadByPivotIdHandler` usa projeção `.Project(...)` — mudar para busca completa para ter acesso a `MetricType`, ou adicionar `MetricType` à projeção

## Test Coverage Plan

### GetSensorTelemetryHandlerTests

| Test | Cenário |
|------|---------|
| `Handle_ReturnsGroupedByDevEui` | 3 sensores _H/_T/_B → 1 item agrupado por DevEUI |
| `Handle_HumidityValue_Correct` | Valor humidity = última leitura _H |
| `Handle_BatteryPercent_Computed` | 3.6V → 100%, 3.0V → 0%, 3.3V → 50% |
| `Handle_ReadAt_IsMaxTimestamp` | readAt = timestamp mais recente entre as 3 leituras |
| `Handle_LegacySensors_Omitted` | Sensores sem sufixo _H/_T/_B não aparecem |
| `Handle_TenantIsolation_FiltersUserId` | Sensor de outro userId não retorna |
| `Handle_PartialMetrics_ReturnsAvailable` | Apenas _H disponível → humidity preenchido, temp/bat null |
| `Handle_NullReadings_ReturnsNullMetrics` | Sensor sem leitura → métrica null |

### Handlers de irrigação (testes existentes atualizados)

- Verificar que sensores Temperature e Battery são excluídos da média
- Verificar que sensores legados (sem MetricType) continuam incluídos

## Verification

```bash
dotnet build AgripeWebAPI/AgripeWebAPI.csproj
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj

# Sample request (após login)
GET /api/v1/sensor/telemetry?pivotId=1
Authorization: Bearer <token>
```
