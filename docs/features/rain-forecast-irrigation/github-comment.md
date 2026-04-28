## Front-End Integration Guide

A new REST endpoint was added on the backend to enrich the irrigation recommendation with rain-forecast data.

### New REST Endpoint

#### `GET /v1/pivot/getIrrigationTrend`

- **Auth:** yes (Bearer JWT — handled by Angular's existing `AuthGuard`/HTTP interceptor pattern).
- **Query parameters:**
  - `PivotId` *(integer, required)* — the pivot to evaluate.
  - `NumberOfReads` *(integer, optional, default `10`)* — how many recent readings to consider when computing the current quadrant average.
- **`UserId` is server-resolved** from the JWT — never sent by the front-end.

#### Response shape (`IrrigationTrend`)

```ts
export interface DailyForecast {
  date: string;                 // ISO date "YYYY-MM-DD"
  precipitationMm: number;
  probabilityPercent: number | null;
}

export interface WeatherForecast {
  totalPrecipitationMm: number;
  source: string;               // "OpenMeteo" | "GoogleWeatherAI"
  isAvailable: boolean;
  probabilityOfPrecipitation: number | null;
  dailyForecasts: DailyForecast[];
}

export interface IrrigationTrend {
  pivotId: number;
  pivotName: string | null;
  latitude: number | null;
  longitude: number | null;
  limiteInferior: number | null;
  limiteSuperior: number | null;
  currentAverage: number | null;
  needsIrrigation: boolean;
  irrigationPostponed: boolean;
  postponeReason: string | null; // e.g. "8.4 mm de chuva prevista nos próximos 5 dias (OpenMeteo)"
  weatherForecast: WeatherForecast | null;
}
```

#### Angular usage (relative URL)

```ts
// api.service.ts
getIrrigationTrend(pivotId: number, numberOfReads: number = 10): Observable<IrrigationTrend> {
  const params = new HttpParams()
    .set('PivotId', pivotId.toString())
    .set('NumberOfReads', numberOfReads.toString());

  return this.http.get<IrrigationTrend>(`${this.baseUrl}pivot/getIrrigationTrend`, { params });
}
```

```ts
// irrigation-dashboard.component.ts
this.apiService.getIrrigationTrend(this.selectedPivotId, this.numberOfDays).subscribe({
  next: trend => this.irrigationTrend = trend,
  error: () => this.irrigationTrend = null
});
```

```html
<!-- Postpone banner -->
<div class="alert-grid" *ngIf="irrigationTrend?.irrigationPostponed">
  <div class="alert-banner alert-low">
    <span class="alert-icon">🌧️</span>
    <div class="alert-text">
      <strong>Irrigação adiada</strong>
      <span>{{ irrigationTrend?.postponeReason }}</span>
    </div>
  </div>
</div>
```

### Authorization

- The endpoint inherits `[Authorize]` from `PivotController`. Apply the existing `AuthGuard` on any route that consumes it.
- `baseUrl` stays as the relative `/api/v1/` so the dev proxy handles routing — do **not** hardcode the API host.

### Notes / edge cases for the UI

- **Pivot without coordinates:** `latitude`/`longitude` are `null`, `weatherForecast` is `null`, `irrigationPostponed` is `false`. The UI should not show the postpone banner in that case (the `*ngIf` above handles it).
- **Forecast service unavailable:** `weatherForecast.isAvailable` is `false`. The recommendation falls back to humidity-only logic. UI should not display rainfall numbers when `isAvailable` is `false`.
- **Threshold and horizon are server-side configurable** via `appsettings.json → WeatherForecast` (`RainThresholdMm`, `ForecastHorizonDays`). Front-end does not need to know these values.
- **Cache TTL** on the backend is 60 minutes by default per (lat, lon, source, days) — the UI can poll the endpoint as often as it likes without abusing the upstream forecast API.
- **`postponeReason`** is a localized PT-BR string ready to display verbatim (e.g. `"8.4 mm de chuva prevista nos próximos 5 dias (OpenMeteo)"`).

### Validation rules

- `PivotId` must be a positive integer; the backend returns a 400 with a notification message if missing.
- A pivot owned by another user returns a 400 with `"Pivot not found."` (tenant isolation — never leaks foreign pivots).

### Out of scope (follow-up)

A UI to capture/edit pivot `Latitude`/`Longitude` is intentionally not part of this issue. Until that ships, pivots created via the existing UI will have `null` coordinates and the postpone banner will simply not appear.
