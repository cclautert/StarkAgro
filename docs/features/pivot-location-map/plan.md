# Pivot Location Map — Implementation Plan

Issue: https://github.com/cclautert/AgripeWeb/issues/17

## Context

Issue #17 asks for a map-based location selector on the Pivot create/edit form, persistence of coordinates and a few derived fields, and surfacing of a 7-day weather forecast on the pivot dashboard.

The Pivot entity already carries nullable `Latitude` / `Longitude` (added in #16 for the rain-forecast feature) and the weather-forecast services (`IWeatherForecastService`, `WeatherForecastOrchestrator`, Open-Meteo + Google Weather AI implementations, in-memory cache) are wired up. What is still missing for #17:

1. Front-end map selector (currently the form only has a `Name` field — no lat/lon UI at all).
2. Three new persisted fields on `Pivot` — `Altitude`, `LocationAddress`, `LocationUpdatedAt`.
3. Range validation (lat / lon / altitude) on POST and PUT.
4. A pivot-scoped weather forecast endpoint surfaced in the dashboard (the existing `/getIrrigationTrend` only returns the forecast when irrigation is needed and rain is detected — not appropriate for a permanent dashboard widget).
5. Tenant isolation on `EditPivotHandler`, which currently filters only by `Id` (this becomes a critical issue as soon as we add new persisted fields, since cross-tenant overwrites of coordinates would silently corrupt other users' data).

## Acceptance Criteria → Implementation Decisions

| AC | Implementation |
|---|---|
| 1. Map button on form | Add **"Selecionar Localização no Mapa"** button to `pivot-form.component.html`; opens `PivotLocationMapComponent` in a `MatDialog`. |
| 2. Click / drag / search / use-my-location with live values | New standalone component using **Leaflet + OpenStreetMap tiles** (no API key required); search via **Nominatim** (OSM, no key); altitude via **Open-Meteo Elevation API** (no key); browser Geolocation API for current location. |
| 3. Confirm fills lat/lon/altitude on form | Modal returns `{ latitude, longitude, altitude, address }`; parent form patches reactive controls. |
| 4. Persist on save | Backend already accepts `Latitude`/`Longitude`. Add `Altitude`, `LocationAddress`, `LocationUpdatedAt` to entity, request, response, and handlers. |
| 5. Edit reopens centred on saved coords | Form passes existing values to map component as inputs; component centres on them and shows marker. |
| 6. Dashboard shows 7-day forecast | New `GET /v1/pivot/forecast?pivotId=X&days=7` + forecast tile in `dashboard.component`. |
| 7. Pivots without coordinates see "configure location" message | Forecast endpoint returns `hasCoordinates=false`; dashboard renders a CTA linking to the pivot edit form. |
| 8. Range validation front + back | Backend: validate in handlers via `INotifier` (Lat -90..90, Lon -180..180, Alt -500..9000). Front-end: Angular `Validators.min/max`. |

## Affected Layers

- **Entity**: `Pivot` (3 new fields)
- **Requests / Responses**: `CreatePivotRequest`, `EditPivotRequest`, `GetPivotResponse`, plus new `GetPivotForecastRequest` / `GetPivotForecastResponse`
- **Handlers**: `CreatePivotHandler`, `EditPivotHandler` (validation + tenant fix), new `GetPivotForecastHandler`
- **Controllers**: `PivotController` (new `GET /forecast` action)
- **Configuration**: `WeatherForecastSettings.PivotDashboardForecastDays` (new, default 7)
- **agpDBContext**: no new collection, no new index — fields are added to existing `pivots` documents, all nullable
- **Angular**: new `pivot-location-map` component (lazy), updated `pivot-form`, updated `pivot.model.ts`, updated `pivot.service.ts`, updated `dashboard.component`
- **MQTT worker**: no changes (worker doesn't touch pivot location)

## New REST Endpoints

| Method | Route | Auth | Request (query) | Response |
|---|---|---|---|---|
| GET | `/v1/pivot/forecast` | `[Authorize]` (Bearer JWT) | `pivotId` (int, required), `days` (int, default 7, range 1-14) | `GetPivotForecastResponse` — see below |

```jsonc
// GetPivotForecastResponse
{
  "pivotId": 123,
  "pivotName": "Pivô Norte",
  "latitude": -29.7,
  "longitude": -53.7,
  "days": 7,
  "hasCoordinates": true,
  "forecast": {
    "totalPrecipitationMm": 12.4,
    "source": "OpenMeteo",
    "isAvailable": true,
    "probabilityOfPrecipitation": 65,
    "dailyForecasts": [
      { "date": "2026-04-29", "precipitationMm": 2.1, "probabilityPercent": 40 }
      // ...
    ]
  },
  "message": null  // populated when hasCoordinates=false or forecast unavailable
}
```

When the pivot has no coordinates: `hasCoordinates: false`, `forecast: null`, `message: "Pivô sem coordenadas. Cadastre a localização para visualizar a previsão do tempo."`.

## Files to Create

- `AgripeWebAPI/Domain/Commands/Requests/Pivots/GetPivotForecastRequest.cs`
- `AgripeWebAPI/Domain/Commands/Responses/Pivots/GetPivotForecastResponse.cs`
- `AgripeWebAPI/Domain/Handlers/Pivots/GetPivotForecastHandler.cs`
- `AgripeWebAPI.Tests/Domain/Handlers/Pivots/GetPivotForecastHandlerTests.cs`
- `AgripeWebAPI.Tests/Domain/Handlers/Pivots/EditPivotHandlerTests.cs` (currently absent — added to cover the tenant fix and new validation)
- `AgripeWebUI/src/app/components/pivot-location-map/pivot-location-map.component.ts`
- `AgripeWebUI/src/app/components/pivot-location-map/pivot-location-map.component.html`
- `AgripeWebUI/src/app/components/pivot-location-map/pivot-location-map.component.css`

## Files to Modify

### Backend
- `AgripeWebAPI/Models/Entities/Pivot.cs` — add `double? Altitude`, `string? LocationAddress`, `DateTime? LocationUpdatedAt`.
- `AgripeWebAPI/Domain/Commands/Requests/Pivots/CreatePivotRequest.cs` — add `Altitude`, `LocationAddress`.
- `AgripeWebAPI/Domain/Commands/Requests/Pivots/EditPivotRequest.cs` — add `Altitude`, `LocationAddress`.
- `AgripeWebAPI/Domain/Commands/Responses/Pivots/GetPivotResponse.cs` — add `Altitude`, `LocationAddress`, `LocationUpdatedAt`.
- `AgripeWebAPI/Domain/Handlers/Pivots/CreatePivotHandler.cs` — validate ranges via `INotifier`, persist new fields, set `LocationUpdatedAt = DateTime.UtcNow` when coordinates supplied. Inject `INotifier` (currently throws — but handler creation is straightforward; the invalid-input path will switch to `_notifier.Handle(...) → return null`).
- `AgripeWebAPI/Domain/Handlers/Pivots/EditPivotHandler.cs` — refactor: inject `ICurrentUserContext` and `INotifier`; filter find by `Id == request.Id && UserId == _currentUser.UserId`; replace `throw KeyNotFoundException` with `_notifier.Handle(...) + return null`; validate ranges; update `LocationUpdatedAt` only when coordinates change.
- `AgripeWebAPI/Controllers/PivotController.cs` — add `GET /forecast` action (`[Authorize]`, sets `command.UserId = GetCurrentUserId()`, returns `CustomResponse(...)`).
- `AgripeWebAPI/Configuration/WeatherForecastSettings.cs` — add `int PivotDashboardForecastDays { get; set; } = 7;`.
- `AgripeWebAPI/appsettings.json` and `appsettings.Development.json` — add `"PivotDashboardForecastDays": 7` under the `WeatherForecast` section.
- `AgripeWebAPI.Tests/Domain/Handlers/Pivots/CreatePivotHandlerTests.cs` — extend with happy-path-with-coords, invalid-lat, invalid-lon, invalid-altitude, no-user-context cases.

### Front-end
- `AgripeWebUI/src/app/models/pivot.model.ts` — add `latitude?`, `longitude?`, `altitude?`, `locationAddress?`, `locationUpdatedAt?`.
- `AgripeWebUI/src/app/services/pivot.service.ts` — add `getForecast(pivotId: number, days = 7): Observable<PivotForecast>`.
- `AgripeWebUI/src/app/components/pivot-form/pivot-form.component.ts` — extend reactive form with lat/lon/altitude/locationAddress controls (validators and `disabled: true` so they're filled only via the map); add `openLocationMap()` opening `MatDialog` with `PivotLocationMapComponent`; on submit forward all fields.
- `AgripeWebUI/src/app/components/pivot-form/pivot-form.component.html` — add the "Selecionar Localização no Mapa" button, lat/lon/altitude/address read-only fields, and validation messages.
- `AgripeWebUI/src/app/components/pivot-form/pivot-form.component.css` — minor styles for the location panel.
- `AgripeWebUI/src/app/components/dashboard/dashboard.component.ts` — add `forecast: PivotForecast | null`; subscribe to `pivotService.getForecast(...)` after `pivoId` is known.
- `AgripeWebUI/src/app/components/dashboard/dashboard.component.html` — add a forecast tile (7-day strip with date / precip / probability) and a "configure location" CTA when `forecast.hasCoordinates === false`.
- `AgripeWebUI/package.json` — add `leaflet` (~1.9) and `@types/leaflet` (dev). Add `leaflet/dist/leaflet.css` import in `pivot-location-map.component.ts` (or `angular.json` if global is preferred — chosen: per-component to keep the bundle lazy).
- `CLAUDE.md` — extend the Pivot entity bullet (new fields), add the forecast endpoint to UI conventions, mention Leaflet.

## MongoDB Changes

Adds three optional fields to the existing `pivots` collection. **No new collections, no new indexes.** Existing documents stay valid because the new fields are nullable in the entity (Mongo serializer maps a missing field to `null`/default).

| Field | Type | Default | Nullable |
|---|---|---|---|
| `Altitude` | `double?` | `null` | yes |
| `LocationAddress` | `string?` | `null` | yes |
| `LocationUpdatedAt` | `DateTime?` | `null` | yes |

The skill's MongoDB-Setup step (Step 3) is therefore a **field addition only** — no `agpDBContext` registration changes, no `Indexes.CreateOne` calls.

## Tenant Isolation Plan

Every new query is scoped by `_currentUser.UserId`:

- **CreatePivotHandler** — already uses `_currentUser.UserId`; unchanged.
- **EditPivotHandler** — **CRITICAL fix**: inject `ICurrentUserContext`, filter `p.Id == request.Id && p.UserId == _currentUser.UserId`. Without this, a malicious caller could overwrite any pivot's coordinates by guessing IDs.
- **GetPivotForecastHandler** — filters by `p.Id == PivotId && p.UserId == request.UserId` (request.UserId is set by the controller from `GetCurrentUserId()`, mirroring `GetIrrigationTrendHandler`).

`request.UserId` is never trusted from the client body — `PivotController` overwrites it with `GetCurrentUserId()` before forwarding (already the pattern for `Add` and `getAll`).

## Risks & Flags

- **R1 (CRITICAL): EditPivotHandler tenant leak.** Pre-existing bug we are inheriting because we are extending the handler. Must fix in this PR.
- **R2 (out of scope): GetPivotHandler also lacks tenant scoping.** Same pattern. Not touched by #17 but should be fixed in a follow-up — flagged for the review report.
- **R3: Map provider.** Issue text mentions Google Maps as primary option but flags cost. Plan uses **Leaflet + OpenStreetMap** (free, MIT, no API key). If the user wants Google Maps, the map component would need a `@angular/google-maps` swap and a key. **Confirm choice before implementation.**
- **R4: Nominatim TOS.** Free reverse-geocoding limit is 1 request/sec. Acceptable for interactive search. We send a `User-Agent: AgripeWeb/1.0` header to comply with their usage policy.
- **R5: SSR safety.** Leaflet requires `window`. Component does dynamic `import('leaflet')` inside `ngAfterViewInit`, gated by `typeof window !== 'undefined'`, so the SSR build succeeds (Angular 19 with `provideClientHydration`).
- **R6: Issue says fields will be NOT NULL after rollout, but AC7 requires graceful behaviour for legacy pivots without coords.** We keep them nullable and let the UI prompt for coordinates on legacy records — matches AC7. The "NOT NULL" line in the issue is treated as a target state, not a hard requirement now.
- **R7: Forecast horizon.** Open-Meteo supports up to 16 days; we cap at 14 in the validation to be safe. Default 7 from settings.
- **R8: Bundle size.** Leaflet adds ~150 KB (compressed). Lazy-loaded via dynamic import — pivot list / dashboard pages do not pay the cost.

## DI Registration

- **Backend**: No new DI lines required. `GetPivotForecastHandler` is auto-registered by the existing assembly scan in `ApiConfig.AddApiConfiguration`. No new services.
- **Front-end**: `MatDialogModule` is already part of Angular Material; we only need to import it where used (`pivot-form.component.ts`).

## Verification

### Build
```bash
# Kill any running API exe first to avoid MSB3027
dotnet build AgripeWebAPI/AgripeWebAPI.csproj
```

### Tests
```bash
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj
```

### Sample HTTP requests
```http
### Create pivot with coordinates
POST /api/v1/pivot/add
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Pivô Norte",
  "latitude": -29.715,
  "longitude": -53.705,
  "altitude": 95.4,
  "locationAddress": "Santa Maria, RS"
}

### Edit pivot — invalid latitude triggers notification
PUT /api/v1/pivot/update
{
  "id": 123,
  "name": "Pivô Norte",
  "latitude": 200,
  "longitude": 0
}
# Expected 400 with "Latitude must be between -90 and 90."

### Get 7-day forecast
GET /api/v1/pivot/forecast?pivotId=123&days=7
# Expected 200 + dailyForecasts[7]

### Get forecast for pivot without coords
GET /api/v1/pivot/forecast?pivotId=42
# Expected hasCoordinates=false, forecast=null, message=...
```

### Manual UI smoke test
1. `cd AgripeWebUI && npm install && npm run start`
2. Navigate to `/pivots/novo`, click **Selecionar Localização no Mapa**.
3. Click on the map → coords appear; drag the marker → coords update; click **Usar minha localização atual** → map centres on browser geolocation; type an address in the search → autocomplete works.
4. Confirm → form fields populated. Save → returns to list.
5. Open the saved pivot for edit → modal opens centred on the saved coordinates with the marker on them.
6. From the pivot list, open the dashboard → forecast tile shows the next 7 days.
7. Edit a different pivot, clear its coordinates (or pick a legacy pivot) → dashboard shows the "configure location" CTA.
