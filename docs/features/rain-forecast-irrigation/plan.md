# Plan — Rain Forecast for Irrigation Trend Analysis (Issue #16)

**Issue:** [#16 — Melhoria Previsão de Chuva](https://github.com/cclautert/AgripeWeb/issues/16)
**Branch (suggested):** `feature/rain-forecast-irrigation`

---

## Context

The existing irrigation trend analysis runs entirely on the Angular frontend (`trend-analysis.service.ts`) using sensor humidity readings against fixed thresholds (`limiteInferior`, `limiteSuperior`). It does **not** consider future rainfall, so it can recommend irrigation even when significant rain is already forecast.

This feature adds a new backend pipeline that:
1. Reuses the existing read aggregation to determine if irrigation is currently warranted (latest quadrant average below `limiteInferior`).
2. Calls a pluggable weather-forecast service for the pivot's location.
3. If forecast precipitation over the next *N* days meets a configurable threshold, postpones the recommendation and explains why.

The frontend dashboard then surfaces the postpone signal alongside the current trend chart.

---

## Acceptance Criteria → Implementation Mapping

| Issue criterion | Implementation |
|---|---|
| Trend analysis considers AI-generated forecast | New `IWeatherForecastService` injected into `GetIrrigationTrendHandler`; primary impl can be `GoogleWeatherAI`, `OpenMeteo`, or any other registered source |
| Recommendations adjust based on forecast rain | Handler sets `IrrigationPostponed=true` and `PostponeReason` when total precipitation in the horizon ≥ threshold |
| Configurable horizon (default 5 d), threshold (default 5 mm), source name | `WeatherForecastSettings` bound from `WeatherForecast` config section |
| Logs which source was used per analysis | `ILogger<>` info log in handler with `forecastSource`, `pivotId`, `userId` (no PII) |
| Fallback on primary failure, log the event | Orchestrator service wraps primary call in `try/catch`; on failure logs warning + invokes fallback; on both failing returns `WeatherForecast { IsAvailable=false }` and handler keeps original recommendation |

---

## Architecture Decisions / Deviations from Issue

The issue body proposes Google Weather API (GraphCast/GenCast) as the **primary** source. To keep the MVP runnable without secrets, I'm flipping the default:

- **Primary (default):** `OpenMeteo` — public, no API key, mature `/v1/forecast?precipitation_sum` endpoint.
- **Secondary (optional, opt-in via settings):** `GoogleWeatherAI` — placeholder implementation that requires `GoogleWeatherApiKey`. Disabled out-of-the-box.
- The orchestrator follows the order configured in `PrimarySource` / `FallbackSource`. Either implementation can be swapped in by changing config alone.

This keeps the spirit of the issue (pluggable AI-capable forecast) while shipping a working default.

The issue also assumes a backend `GetIrrigationTrendRequest` already exists. **It does not** — this feature creates it.

The issue assumes the `Pivot` entity has latitude/longitude. **It does not** — this feature adds them as nullable fields. Pivots without coordinates will skip the forecast step (recommendation falls back to humidity-only logic).

---

## Affected Layers

- **Backend (AgripeWebAPI):** new request/response DTOs, new handler, new service abstraction + 2 implementations, new configuration POCO, controller endpoint, DI wiring, NuGet refs.
- **Backend Entity:** `Pivot` gets nullable `Latitude` / `Longitude`.
- **MongoDB:** existing `pivots` collection — backward-compatible field additions only. No new collections, no new indexes (queries unchanged).
- **Angular (AgripeWebUI):** new `IrrigationTrend` API call + minimal UI banner on the irrigation dashboard. Trend math stays on the frontend; the new endpoint contributes only the rain-postpone signal.
- **MQTT worker (`AgripeWebWorker`):** unaffected.

---

## New REST Endpoint

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `GET` | `/v1/pivot/getIrrigationTrend` | `[Authorize]` (Bearer JWT) | `GetIrrigationTrendRequest { PivotId, NumberOfReads = 10 }` (UserId is server-resolved) | `IrrigationTrendResponse` (schema below) |

### `IrrigationTrendResponse` shape

```jsonc
{
  "pivotId": 1,
  "pivotName": "Pivô A",
  "latitude": -27.5954,
  "longitude": -48.5480,
  "limiteInferior": 25,
  "limiteSuperior": 75,
  "currentAverage": 22.5,                  // latest avg across all sensors
  "needsIrrigation": true,                 // currentAverage < limiteInferior
  "irrigationPostponed": true,
  "postponeReason": "Previsão de 8.4 mm de chuva nos próximos 5 dias (OpenMeteo)",
  "weatherForecast": {
    "totalPrecipitationMm": 8.4,
    "source": "OpenMeteo",
    "isAvailable": true,
    "probabilityOfPrecipitation": null,
    "dailyForecasts": [
      { "date": "2026-04-28", "precipitationMm": 2.1, "probabilityPercent": 60 },
      { "date": "2026-04-29", "precipitationMm": 6.3, "probabilityPercent": 80 },
      { "date": "2026-04-30", "precipitationMm": 0.0, "probabilityPercent": 10 }
    ]
  }
}
```

When pivot has no lat/lon, response includes `weatherForecast: null`, `irrigationPostponed: false`, `postponeReason: null`.

---

## Files to Create

| Path | Type |
|---|---|
| `AgripeWebAPI/Configuration/WeatherForecastSettings.cs` | Config POCO |
| `AgripeWebAPI/Models/WeatherForecast.cs` | Records: `WeatherForecast`, `DailyForecast` |
| `AgripeWebAPI/Models/Interfaces/IWeatherForecastService.cs` | Interface |
| `AgripeWebAPI/Services/Forecast/WeatherForecastOrchestrator.cs` | Orchestrator (chooses primary, falls back) |
| `AgripeWebAPI/Services/Forecast/OpenMeteoForecastService.cs` | Primary impl (HTTP, no key) |
| `AgripeWebAPI/Services/Forecast/GoogleWeatherAIForecastService.cs` | Optional impl (HTTP, requires key) |
| `AgripeWebAPI/Domain/Commands/Requests/Pivots/GetIrrigationTrendRequest.cs` | MediatR request |
| `AgripeWebAPI/Domain/Commands/Responses/Pivots/IrrigationTrendResponse.cs` | Response DTO |
| `AgripeWebAPI/Domain/Handlers/Pivots/GetIrrigationTrendHandler.cs` | MediatR handler |
| `AgripeWebAPI.Tests/Services/Forecast/WeatherForecastOrchestratorTests.cs` | Unit tests |
| `AgripeWebAPI.Tests/Services/Forecast/OpenMeteoForecastServiceTests.cs` | Unit tests with mocked `HttpMessageHandler` |
| `AgripeWebAPI.Tests/Domain/Handlers/Pivots/GetIrrigationTrendHandlerTests.cs` | Handler tests |
| `docs/features/rain-forecast-irrigation/plan.md` | This plan (already saved) |

## Files to Modify

| Path | Change |
|---|---|
| `AgripeWebAPI/Models/Entities/Pivot.cs` | Add `decimal? Latitude`, `decimal? Longitude` (nullable, default `null`) |
| `AgripeWebAPI/Domain/Commands/Requests/Pivots/CreatePivotRequest.cs` | Add `Latitude`, `Longitude` |
| `AgripeWebAPI/Domain/Commands/Requests/Pivots/EditPivotRequest.cs` | Add `Latitude`, `Longitude` |
| `AgripeWebAPI/Domain/Handlers/Pivots/CreatePivotHandler.cs` | Persist new fields |
| `AgripeWebAPI/Domain/Handlers/Pivots/EditPivotHandler.cs` | Persist new fields |
| `AgripeWebAPI/Domain/Commands/Responses/Pivots/GetPivotResponse.cs` | Surface `Latitude`, `Longitude` |
| `AgripeWebAPI/Domain/Handlers/Pivots/GetPivotHandler.cs` | Project new fields |
| `AgripeWebAPI/Controllers/PivotController.cs` | Add `GET /getIrrigationTrend` endpoint |
| `AgripeWebAPI/Configuration/ApiConfig.cs` | Register `WeatherForecastSettings`, `IMemoryCache`, `IWeatherForecastService`, named `HttpClient` instances |
| `AgripeWebAPI/AgripeWebAPI.csproj` | Add NuGet refs: `Microsoft.Extensions.Caching.Memory`, `Microsoft.Extensions.Http.Polly` |
| `AgripeWebAPI/appsettings.json` | Add `"WeatherForecast"` section with safe defaults |
| `AgripeWebAPI/appsettings.Development.template.json` | Same section with placeholder API key |
| `AgripeWebAPI/appsettings.Production.template.json` | Same |
| `AgripeWebUI/src/app/services/api.service.ts` | Add `getIrrigationTrend(pivotId, numberOfReads)` |
| `AgripeWebUI/src/app/models/irrigation-trend.model.ts` | New TS interface mirroring response |
| `AgripeWebUI/src/app/components/irrigation-dashboard/irrigation-dashboard.component.ts` | Call new endpoint; expose `irrigationPostponed`, `postponeReason` |
| `AgripeWebUI/src/app/components/irrigation-dashboard/irrigation-dashboard.component.html` | Banner: when `irrigationPostponed`, render postpone notice |
| `CLAUDE.md` | Document new entity fields, services, and endpoint |

---

## MongoDB Changes

- **No new collections, no new indexes.**
- Backward-compatible additions to `pivots` documents: `Latitude`, `Longitude` (BSON `decimal128`, nullable). Existing documents without these fields deserialize as `null` — no migration needed.

---

## Tenant Isolation Plan

- `PivotController.GetIrrigationTrend` sets `command.UserId = GetCurrentUserId()` before dispatch (matches existing pattern in `GetByPivotId` / `GetAll`).
- `GetIrrigationTrendHandler` re-reads the pivot with `Find(p => p.Id == request.PivotId && p.UserId == request.UserId)` — refuses to operate on pivots not owned by the caller (returns `null` + `_notifier.Handle("Pivot not found")`).
- Sensor + read queries inside the handler reuse the same `s.UserId == request.UserId` filter as `GetReadByPivotIdHandler`.
- Lat/lon coming from the persisted pivot (server-side) are trusted; the request body does not carry coordinates.

---

## DI Registration (in `ApiConfig.cs`)

```csharp
services.Configure<WeatherForecastSettings>(configuration.GetSection("WeatherForecast"));
services.AddMemoryCache();

services.AddHttpClient<OpenMeteoForecastService>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(8);
})
.AddTransientHttpErrorPolicy(p =>
    p.WaitAndRetryAsync(2, retry => TimeSpan.FromMilliseconds(200 * retry)));

services.AddHttpClient<GoogleWeatherAIForecastService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
})
.AddTransientHttpErrorPolicy(p =>
    p.WaitAndRetryAsync(2, retry => TimeSpan.FromMilliseconds(200 * retry)));

services.AddScoped<OpenMeteoForecastService>();
services.AddScoped<GoogleWeatherAIForecastService>();
services.AddScoped<IWeatherForecastService, WeatherForecastOrchestrator>();
```

All new service lifetimes are **Scoped** to match the existing pattern (handlers are Scoped via MediatR registration).

---

## `appsettings` block (defaults)

```json
"WeatherForecast": {
  "ForecastHorizonDays": 5,
  "RainThresholdMm": 5.0,
  "PrimarySource": "OpenMeteo",
  "FallbackSource": "OpenMeteo",
  "GoogleWeatherApiKey": "CHANGE_ME",
  "CacheDurationMinutes": 60
}
```

`GoogleWeatherApiKey` is `CHANGE_ME` everywhere committed; never holds a real key.

---

## Caching

- Cache key: `forecast:{source}:{round(lat,3)}:{round(lon,3)}:{days}` — coordinates rounded to 3 decimals (~110 m) so nearby pivots share entries.
- TTL: `CacheDurationMinutes` minutes via `IMemoryCache`.
- Cache wrapping happens in the orchestrator, not in the individual implementations.

---

## Risks & Flags

1. **Lat/lon defaults.** Existing pivots have no coordinates and the UI does not yet expose an input. The handler must gracefully degrade (skip forecast) when either coordinate is null. Adding the input UI for lat/lon is deliberately out of scope for this issue — open a follow-up.
2. **Polly version drift.** `Microsoft.Extensions.Http.Polly` is the older transitional package; ensure the version chosen targets .NET 10. If incompatible, fall back to `Microsoft.Extensions.Http.Resilience`.
3. **Open-Meteo response variations.** Some regions return `null` precipitation arrays for far-future days; orchestrator must treat `null` as `0 mm` (logged) rather than throwing.
4. **MongoDB.Driver `decimal` storage.** `decimal?` lat/lon serialize as BSON `decimal128`. Confirm via roundtrip test — alternative is to use `double?`.
5. **Time-zone alignment.** Forecast horizon is calendar days; today + N days using UTC is acceptable for MVP; revisit if growers want local-tz semantics.

---

## Verification

```bash
# Backend build
dotnet build AgripeWebAPI/AgripeWebAPI.csproj

# Unit tests
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj

# Manual API test (after `dotnet run`):
curl -H "Authorization: Bearer <jwt>" \
  "https://localhost:7162/v1/pivot/getIrrigationTrend?pivotId=1&numberOfReads=10"

# UI: from AgripeWebUI/, npm run start, navigate to /pivots, select pivot,
# verify postpone banner renders when forecast >= threshold.
```

### Acceptance smoke checks
- Pivot without lat/lon → `weatherForecast: null`, no postpone, no exception.
- Pivot with lat/lon and Open-Meteo reachable → forecast populated, postpone toggles based on threshold.
- Open-Meteo unreachable → response still 200, `weatherForecast.isAvailable = false`, recommendation falls through.
- Two consecutive requests within `CacheDurationMinutes` → second hits cache (verify via log absence of HTTP call in trace).

---

## Out of Scope (follow-up issues)

- UI for editing pivot lat/lon (today only the API persists them).
- Per-quadrant forecast (lat/lon currently captured at the pivot level).
- GenCast probabilistic ensembles — `ProbabilityOfPrecipitation` field is added to the response shape but no implementation populates it yet.
- Migrating existing trend math from Angular to backend.
