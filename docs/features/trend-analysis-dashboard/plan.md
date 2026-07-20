# Implementation Plan: Trend Analysis Dashboard (#7)
Generated: 2026-04-16

---

## Context

A React mockup (`trend-analysis-mockup.jsx`) was committed to the repository root showing a
fully-designed trend analysis view for a single sensor quadrant. The mockup demonstrates:
linear regression trend lines, 3-day moving averages, 5-day forward projections with expanding
confidence margins, metric cards (latest reading, period average, min/max, projection, alert
count), and a compliance/variability analysis panel. Work item 7 implements this feature end-to-end:
the API must expose the statistics needed (per-sensor aggregated readings already exist), the Angular
`DashboardComponent` must render the new chart overlays and metric cards matching the mockup, and the
worker pipeline (MQTT) is unaffected.

---

## Work Item Summary

**Title:** Trend Analysis Dashboard (#7)

**Description:** Add a trend analysis view to the existing per-quadrant sensor dashboard. The view
must show historical sensor readings alongside a linear regression trend line, a 3-day moving average
overlay, a 5-day forward projection band, and a set of KPI cards (latest reading, average, min, max,
projected value in 5 days, alert count). A compact analysis panel below the chart must display the
trend direction badge (rising / stable / falling), the regression slope, the projection point, and
compliance and variability percentages.

**Acceptance Criteria (inferred from mockup):**

1. The existing `GET v1/reads/GetAllBySensorId` endpoint returns enough data for the UI to compute
   all statistics client-side — no new API endpoint is required if the existing response includes
   all reads for the requested window.
2. The `DashboardComponent` chart gains three optional overlays: linear regression trend line
   (yellow dashed), 3-day moving average (violet), and a 5-day projection band (orange fill +
   central dashed line). Each overlay has an independent toggle checkbox.
3. Six metric cards appear above the chart: Ultima leitura, Media do periodo, Minimo, Maximo,
   Projecao em 5 dias, Alertas no periodo.
4. A trend direction badge (arrow + label + slope per day) is displayed inline with the chart header.
5. An analysis panel below the chart shows four tiles: Direcao da Tendencia, Projecao (5 dias),
   Conformidade, Variabilidade — each with the correct colour coding.
6. All existing dashboard behaviour (pivot + quadrant route params, 60-second auto-refresh, day
   selector buttons, sensor selector dropdown, limit lines, colour zones) is preserved unchanged.

---

## API Scope

No new endpoints are required. The current `GET v1/reads/GetAllBySensorId` endpoint already returns
all readings for a sensor within a window of N days. The Angular dashboard already calls this
endpoint via `ApiService.getAllReadsBySensorId`. All statistical computations (regression, moving
average, projection) will be implemented entirely in the Angular service layer and component —
the backend is unmodified.

**Assumption to flag:** The mockup uses daily-granularity data for its chart (`DD/MM` labels). The
current `GetAllReadBySensorIdResponse` returns individual readings with full `DateTime` timestamps,
not daily aggregates. The UI must group readings by day before computing statistics.

---

## Files to Create

### 1. `StarkAgroUI/src/app/services/trend-analysis.service.ts`

Type: Angular service (pure computation, no HTTP calls)

Purpose: Centralises all statistical logic extracted from the mockup so the component stays thin
and the algorithms are independently testable.

Key members:

```
export interface TrendPoint {
  date: string;      // 'DD/MM'
  value: number;     // last reading of the day
  movingAvg?: number;
  trend?: number;
}

export interface ProjectionPoint {
  date: string;      // '+1d' … '+5d'
  projMin: number;
  projMax: number;
  projMid: number;
}

export interface TrendStats {
  slope: number;
  intercept: number;
  avg: number;
  min: number;
  max: number;
  last: number;
  proj5: number;
  alertCount: number;
  compliancePct: number;
  variability: number;
}

@Injectable({ providedIn: 'root' })
export class TrendAnalysisService {
  computeDailyData(reads: Read[], limiteInferior: number, limiteSuperior: number):
    { points: TrendPoint[], projection: ProjectionPoint[], stats: TrendStats }
}
```

Internal helpers (private methods):
- `linearRegression(data: {value:number}[]): { slope: number, intercept: number }`
- `movingAverage(data: TrendPoint[], window: number): TrendPoint[]`
- `buildProjection(slope, intercept, n, projDays): ProjectionPoint[]`
- `toDayLabel(date: Date): string` — `'DD/MM'` format

The service takes raw `Read[]` (from `GetAllReadBySensorIdResponse`), groups by UTC day, takes the
last reading per day, applies moving average and regression, and returns the fully computed dataset.

### 2. `StarkAgroUI/src/app/services/trend-analysis.service.spec.ts`

Type: Angular unit test (Jasmine/Karma)

Required test cases:

| Test | Description |
|---|---|
| `linearRegression with flat data returns slope ~0` | Feed 5 equal values, expect slope near zero |
| `linearRegression with strictly increasing data returns positive slope` | Values 1,2,3,4,5 → slope ~1 |
| `movingAverage window=3 averages correctly` | Spot-check position 2 = avg of positions 0-2 |
| `movingAverage window=1 returns original values` | No smoothing |
| `buildProjection returns projectionDays entries` | projDays=5 → 5 entries |
| `buildProjection margin grows with projection distance` | entry[4].projMax − entry[4].projMid > entry[0].projMax − entry[0].projMid |
| `computeDailyData groups readings by day` | Two readings on the same day → one data point, takes last |
| `computeDailyData computes correct alertCount` | Feed values where 2 are outside limits, expect alertCount=2 |
| `computeDailyData computes compliancePct` | 8 of 10 readings in range → 80% |

Follow the Angular default Jasmine `TestBed` pattern used across the project (`*.spec.ts` next to
the service file).

---

## Files to Modify

### 1. `StarkAgroUI/src/app/components/dashboard/dashboard.component.ts`

Changes:
- Inject `TrendAnalysisService` via `inject()` (already using `inject()` pattern in sensor-form).
- Add component-level state:
  ```typescript
  showTrend = true;
  showMA = true;
  showProjection = true;
  trendStats: TrendStats | null = null;
  projectionPoints: ProjectionPoint[] = [];
  ```
- Modify `loadReads()`: after receiving the reads array from `getAllReadsBySensorId`, call
  `this.trendAnalysisService.computeDailyData(reads, this.limiteInferior!, this.limiteSuperior!)`
  and unpack the result into `trendStats`, `projectionPoints`, and new chart datasets.
- Replace the raw `reads.map(r => r.value)` dataset assignment with the computed `TrendPoint[]`
  merged with `ProjectionPoint[]` for the unified chart.
- Add Chart.js dataset entries for trend line, moving average, projection min/max area, and
  projection central line — mirroring the mockup's `ComposedChart` configuration translated to
  `ChartConfiguration<'line'>` with appropriate annotations.
- Add `setDays()` method already exists; keep it.
- Add `onToggleTrend()`, `onToggleMA()`, `onToggleProjection()` handlers that update the
  corresponding dataset `hidden` flag and call `this.chart?.update()`.

Note: Chart.js does not have a native `ReferenceArea` equivalent. The colour zones (red/green/blue
background bands) are already implemented as `fill: 'start'` / `fill: 'end'` datasets with
`backgroundColor` in the current component. The projection band can be added as two additional
datasets with `fill: '+1'` (fill between them) and a low `borderWidth: 0` for the invisible floor.
Alternatively use the `chartjs-plugin-annotation` package for reference lines and areas — see Risk
R1 below.

### 2. `StarkAgroUI/src/app/components/dashboard/dashboard.component.html`

Changes:
- Add the toggle checkboxes row above the chart (Tendencia, Media Movel, Projecao).
- Add the `TrendBadge` element inline with the chart title: an `<ng-container>` with `*ngIf` and a
  styled `<span>` showing the arrow, label, and slope value from `trendStats`.
- Add the six `MetricCard` tiles above the chart using `*ngIf="trendStats"`:
  - Ultima leitura, Media do periodo, Minimo, Maximo, Projecao em 5 dias, Alertas no periodo.
- Add the analysis panel below the chart (four tiles: Direcao da Tendencia, Projecao 5 dias,
  Conformidade, Variabilidade) — all bound to `trendStats` properties.
- All new elements follow the dark-theme CSS already established in `dashboard.component.css`.

### 3. `StarkAgroUI/src/app/components/dashboard/dashboard.component.css`

Changes:
- Add CSS classes from the mockup's inline styles, converted to classes:
  `.metric-cards-grid`, `.metric-card`, `.trend-badge`, `.trend-badge-rising`,
  `.trend-badge-falling`, `.trend-badge-stable`, `.overlay-toggles`, `.analysis-panel`,
  `.analysis-tile`, `.analysis-tile-value`.
- Maintain the existing dark theme variables (`#0f172a`, `#1e293b`, `#334155`).
- Add responsive breakpoints for the metric card grid (`flex-wrap: wrap`, `min-width: 130px`).

### 4. `StarkAgroUI/src/app/models/read.model.ts`

No changes required — existing `Read` interface `{ sensorId, value, date }` is sufficient. The
`TrendAnalysisService` consumes this directly.

---

## Schema / MongoDB Changes

None. The feature is entirely client-side computation on top of existing `read_sensors` data. No
new collections, fields, or indexes are needed.

---

## Key Implementation Decisions

### Decision 1 — Client-side vs server-side computation

All regression, moving average, and projection logic is implemented in Angular. The backend already
returns all individual readings for a sensor within a day window. This avoids adding new API
endpoints and keeps the backend unchanged.

Trade-off: if the sensor produces many readings per day (e.g., every minute), the payload size
could become large for long windows. At the current polling frequency (the IoT firmware sleeps 60
seconds for ESP8266 and 3 hours for LoRa slaves), this is not a concern for windows up to 30 days.
Flag this as a scaling note (see Risk R2).

### Decision 2 — Chart.js dataset structure for projection band

The projection band (orange fill between `projMin` and `projMax`) is implemented as two transparent
datasets that share the same X-axis labels as the history datasets (using `null` for the historical
range). Chart.js `fill: '+1'` fills from one dataset down to the next. The historical datasets use
`null` for the projection range, and the projection datasets use `null` for the historical range,
with `spanGaps: false` so the gap is not connected.

### Decision 3 — Moving average at the boundary

The first `window - 1` data points get a partial moving average (average of available points, not
a full window). This matches the mockup's `Math.max(0, i - window + 1)` slice behaviour.

### Decision 4 — Confidence margin

The projection confidence margin grows linearly: `Math.min(p * 2.5, 15)` where `p` is the number
of days ahead. This matches the mockup exactly. Document this formula in a JSDoc comment on
`buildProjection`.

### Decision 5 — Day grouping

When multiple readings exist for the same calendar day (UTC), take the last one by timestamp order.
This matches the mockup's `byDay.get(lbl)!.push(...)` → `vals[vals.length - 1]` pattern.

---

## Authorization

No authorization changes. The existing `DashboardComponent` is accessible only to logged-in users
via `AuthGuard` on the `/dashboard/:pivoId/:quadrante` route. The API endpoint
`GET v1/reads/GetAllBySensorId` is already behind `[Authorize]`. No changes are needed.

---

## DI Registration

`TrendAnalysisService` is decorated with `@Injectable({ providedIn: 'root' })`, so Angular
registers it automatically in the root injector. No changes to `app.config.ts` or any module
file are required.

---

## Risks and Flags

### R1 — Chart.js projection area rendering

Chart.js `fill` between two datasets can be finicky when mixing null-gapped data. The implementer
must verify that `spanGaps: false` on both projection datasets prevents the fill from extending
into the historical range. If the fill behaviour is unreliable, consider adding
`chartjs-plugin-annotation` (already available in the ng2-charts ecosystem) and using
`AnnotationPlugin` to draw the orange band as a box annotation instead.

Alternative: use two `fill: false` line datasets (projMin and projMax) and a third with
`fill: { target: '-1', above: 'rgba(251,146,60,0.15)' }` for the band. Test in the browser
before committing to one approach.

### R2 — Payload size for large day windows

If the user selects 30 days and the sensor posts every 60 seconds, the API returns up to 43,200
readings. The current `GetListReadBySensorIdHandler` already filters by `startDate = AddDays(-N)`,
so the payload is bounded. However it returns ALL individual readings, not daily aggregates.
At 43,200 readings, each ~50 bytes JSON, that is ~2 MB per request — acceptable for a web app on
WiFi but worth noting. Flag for a future `aggregate-by-day` query parameter if performance becomes
an issue.

### R3 — `DashboardComponent` is not standalone

`DashboardComponent` currently has `standalone: false` and belongs to an Angular module. The
`TrendAnalysisService` is registered with `providedIn: 'root'` so it is available everywhere. No
import changes are needed in the module. Verify this in `app.module.ts` before adding any new
imports.

### R4 — Chart.js dataset index stability

The existing `loadReads()` method accesses `lineChartData.datasets[0]` through `[3]` by index.
Adding new datasets (trend, MA, projMin, projMax, projMid) must append to the array in a stable
order. The implementer must update every index reference in the component (`datasets[1].data`,
etc.) to named references or rebuild the datasets array entirely in `loadReads()`. Rebuilding is
the safer approach — assign a completely new `datasets` array on each data load instead of mutating
by index.

### R5 — `numberOfReads` parameter semantics

The existing component uses `numberOfReads` as the `NumberOfReads` query parameter to
`GetAllBySensorId`. Looking at `GetListReadBySensorIdHandler`, this parameter is used as
`AddDays(-request.NumberOfReads)` — i.e., it is the number of days back, not the number of
readings. The selector buttons (7, 14, 30) already use this correctly. The trend service must
align with the same semantics: `N` days of data, daily-grouped.

### R6 — `limiteInferior` and `limiteSuperior` may be null on first load

The dashboard fetches pivot data before sensor data. On the first load, `limiteInferior` and
`limiteSuperior` are `null` until the API response arrives. The `TrendAnalysisService.computeDailyData`
call inside `loadReads()` must only be invoked after both limits are set. The existing code already
subscribes inside the `getReadsByPivotId` callback, so this is handled, but the implementer must
confirm the call order is preserved when restructuring `loadReads()`.

---

## Test Cases to Cover

### TrendAnalysisService unit tests (new file)

| Test Case | Expected Behaviour |
|---|---|
| `linearRegression` on 5 identical values | slope = 0, intercept = value |
| `linearRegression` on [1,2,3,4,5] | slope ≈ 1, intercept ≈ 1 |
| `movingAverage` window=3 at index 2 | avg of indices 0,1,2 |
| `movingAverage` window=1 | all values equal to original |
| `buildProjection` with projDays=5 | returns array of length 5 |
| Projection margin grows with distance | entry[4].projMax − mid > entry[0].projMax − mid |
| `computeDailyData` with two readings same day | one output point, value = last reading |
| `computeDailyData` alert count | 2 readings outside [40,80] on 10 readings → alertCount=2 |
| `computeDailyData` compliancePct | (10-2)/10 = 80% |
| `computeDailyData` variability | max - min of input values |
| `computeDailyData` proj5 clamps to [0,100] | intercept + slope*14 clamped correctly |

### DashboardComponent integration (manual)

| Scenario | Verification |
|---|---|
| Load `/dashboard/1/TopRight` | Six metric cards appear, all populated |
| Toggle "Tendencia" checkbox off | Yellow dashed line disappears from chart |
| Toggle "Media Movel" checkbox off | Violet line disappears |
| Toggle "Projecao" checkbox off | Orange projection band and central line disappear |
| Select 30-day window | Chart re-renders with 30 days of data and updated statistics |
| Sensor with no readings | Metric cards show `'-'` or `'Sem dados'`, no JS errors |
| Sensor with 1 reading | Moving average equals the single value, slope = 0 |
| Auto-refresh after 60 seconds | Statistics recalculate without page reload |

---

## Implementation Order

1. Create `TrendAnalysisService` with its statistical helpers.
2. Write `TrendAnalysisService` unit tests and confirm they pass (`ng test`).
3. Modify `DashboardComponent` TypeScript — inject service, update `loadReads()`, add overlay state.
4. Modify `DashboardComponent` HTML — add toggles, metric cards, analysis panel.
5. Modify `DashboardComponent` CSS — add dark-theme utility classes.
6. Run the Angular dev server (`npm run start` inside `StarkAgroUI/`) and verify visually against
   the mockup in `trend-analysis-mockup.jsx`.
7. Run the full test suite (`ng test`).

---

## Verification

### Angular unit tests

```bash
cd StarkAgroUI
npm run test -- --include=**/trend-analysis.service.spec.ts
```

### Full test suite

```bash
cd StarkAgroUI
npm run test
```

### Manual end-to-end

Start the full stack:
```bash
docker compose -f docker/docker-compose.yml up --build
```

Navigate to `http://localhost:80/dashboard/1/TopRight` (or the correct pivot/quadrant ID).

Verify:
- Six metric cards with real values from the database are visible above the chart.
- The trend line (yellow dashed), moving average (violet), and projection band (orange) overlay the
  historical data correctly.
- The trend direction badge reflects the actual slope of the data.
- The analysis panel below the chart shows the four tiles.
- Toggling each checkbox hides/shows the corresponding overlay without errors.
- Changing the day selector (7, 14, 30) recalculates all statistics.

---

## Reference Files the Implementer Must Read Before Coding

- `trend-analysis-mockup.jsx` — the exact visual specification; all formulas and colour codes are
  taken from this file.
- `StarkAgroUI/src/app/components/dashboard/dashboard.component.ts` — existing chart setup,
  dataset structure (indices 0-3), `loadReads()` shape, `setDays()`, auto-refresh, route params.
- `StarkAgroUI/src/app/components/dashboard/dashboard.component.html` — current template to extend.
- `StarkAgroUI/src/app/components/dashboard/dashboard.component.css` — dark theme variables.
- `StarkAgroUI/src/app/models/read.model.ts` — `Read` interface consumed by the service.
- `StarkAgroUI/src/app/components/irrigation-dashboard/irrigation-dashboard.component.ts` — shows
  the multi-quadrant chart pattern; useful reference for `ComposedChart`-style multi-dataset builds
  in ng2-charts.
- `StarkAgroUI/src/app/services/sensor.service.ts` — `getAllByPivotId` for context on how the
  existing sensor data flow works.
- `StarkAgroUI/src/app/services/api.service.ts` — `getAllReadsBySensorId` call signature.
- `StarkAgroAPI/Domain/Handlers/Reads/GetListReadBySensorIdHandler.cs` — confirms the `NumberOfReads`
  parameter is days, not a record count.
