# Plan: RainThresholdMm Configurável por Pivot e Global

Issue: https://github.com/cclautert/StarkAgro/issues/23

---

## Context

`WeatherForecastSettings.RainThresholdMm` (default 5.0 mm) controls whether irrigation is postponed due to rain forecast. Currently it is read-only from `appsettings.json`. This feature makes the threshold configurable per-user (global default) and per-pivot (override), following the same pattern already used by `LimiteInferior`/`LimiteSuperior`.

Precedence hierarchy:
```
pivot.RainThresholdMm ?? user.RainThresholdMm ?? _settings.RainThresholdMm (5.0)
```

---

## Acceptance Criteria

| Criterion | Implementation |
|---|---|
| User can set a global rain threshold | Add `RainThresholdMm` field to `User` entity and `EditUserLimitsRequest`/handler |
| Pivot can override user threshold | Add `RainThresholdMm` field to `Pivot` entity and `EditPivotLimitsRequest`/handler |
| `GetIrrigationTrendHandler` uses hierarchy | Resolve `rainThreshold = pivot.RainThresholdMm ?? user?.RainThresholdMm ?? _settings.RainThresholdMm` |
| `GetUserResponse` exposes the field | Add `RainThresholdMm` to `GetUserResponse` and projection in `GetUserHandler` |
| Angular UI allows editing both levels | Add input to `global-config` (user level) and `pivot-config` (pivot level) components |
| null means "use parent in hierarchy" | All fields are `double?` / `number | null`; no `required` validator on front end |

---

## Affected Layers

- **Entities**: `User`, `Pivot`
- **Requests**: `EditUserLimitsRequest`, `EditPivotLimitsRequest`
- **Responses**: `GetUserResponse`
- **Handlers**: `EditUserLimitsHandler`, `GetUserHandler`, `EditPivotLimitsHandler`, `GetIrrigationTrendHandler`
- **Angular models**: `user.model.ts`, `pivot.model.ts`
- **Angular services**: `user.service.ts`, `pivot.service.ts`
- **Angular components**: `global-config`, `pivot-config`

---

## New REST Endpoints

None — extends existing endpoints:
- `PUT /api/v1/user/updateLimits` — now accepts optional `rainThresholdMm: double?`
- `PUT /api/v1/pivot/updateLimits` — now accepts optional `rainThresholdMm: double?`
- `GET /api/v1/user/getById` — now returns `rainThresholdMm: double?`

---

## Files to Create

None.

---

## Files to Modify

### Backend

| File | Change |
|---|---|
| `StarkAgroAPI/Models/Entities/User.cs` | Add `public double? RainThresholdMm { get; set; }` |
| `StarkAgroAPI/Models/Entities/Pivot.cs` | Add `public double? RainThresholdMm { get; set; }` |
| `StarkAgroAPI/Domain/Commands/Requests/Users/EditUserLimitsRequest.cs` | Add `public double? RainThresholdMm { get; set; }` |
| `StarkAgroAPI/Domain/Handlers/Users/EditUserLimitsHandler.cs` | Add `user.RainThresholdMm = request.RainThresholdMm;` before `ReplaceOneAsync` |
| `StarkAgroAPI/Domain/Commands/Responses/Users/GetUserResponse.cs` | Add `public double? RainThresholdMm { get; set; }` |
| `StarkAgroAPI/Domain/Handlers/Users/GetUserHandler.cs` | Add `RainThresholdMm = x.RainThresholdMm` to the `Project()` expression |
| `StarkAgroAPI/Domain/Commands/Requests/Pivots/EditPivotLimitsRequest.cs` | Add `public double? RainThresholdMm { get; set; }` |
| `StarkAgroAPI/Domain/Handlers/Pivots/EditPivotLimitsHandler.cs` | Add `pivot.RainThresholdMm = request.RainThresholdMm;` before `ReplaceOneAsync` |
| `StarkAgroAPI/Domain/Handlers/Pivots/GetIrrigationTrendHandler.cs` | After fetching `user` (~line 59), compute `var rainThreshold = pivot.RainThresholdMm ?? user?.RainThresholdMm ?? _settings.RainThresholdMm;`; replace `_settings.RainThresholdMm` on line 117 with `rainThreshold` |

### Frontend

| File | Change |
|---|---|
| `StarkAgroUI/src/app/models/user.model.ts` | Add `rainThresholdMm?: number \| null;` |
| `StarkAgroUI/src/app/models/pivot.model.ts` | Add `rainThresholdMm?: number \| null;` |
| `StarkAgroUI/src/app/services/user.service.ts` | Add `rainThresholdMm?: number \| null` param to `updateLimits()`, include in request body |
| `StarkAgroUI/src/app/services/pivot.service.ts` | Add `rainThresholdMm?: number \| null` param to `updateLimits()`, include in request body |
| `StarkAgroUI/src/app/components/global-config/global-config.component.ts` | Add `rainThresholdMm: [null, Validators.min(0)]` to form; load/save the field |
| `StarkAgroUI/src/app/components/global-config/global-config.component.html` | Add input: label "Chuva mínima para adiar irrigação (mm)", placeholder "Padrão global: 5 mm", step 0.1 |
| `StarkAgroUI/src/app/components/pivot-config/pivot-config.component.ts` | Add `rainThresholdMm: [null, Validators.min(0)]` to form; load/save the field |
| `StarkAgroUI/src/app/components/pivot-config/pivot-config.component.html` | Add input: same label/placeholder, step 0.1 |

---

## MongoDB Changes

None — `RainThresholdMm` is a new nullable `double?` field on existing `users` and `pivots` documents. MongoDB returns `null` for missing fields automatically. No new collections or indexes required.

---

## Tenant Isolation Plan

`GetIrrigationTrendHandler` already enforces tenant isolation:
- Pivot fetched with `p.Id == request.PivotId && p.UserId == request.UserId`
- User fetched with `u.Id == request.UserId`

The new `rainThreshold` variable is derived from data that is already scoped to the authenticated user — no additional isolation work needed.

---

## Risks & Flags

- **Pre-existing issue (WARNING)**: `EditPivotLimitsHandler` does not filter by `UserId` when finding the pivot (`Find(p => p.Id == request.Id)`). This is a pre-existing gap, not introduced here. The controller should ensure the pivot belongs to the current user. Out of scope for this issue but worth noting.
- No type mismatch risk: `WeatherForecastSettings.RainThresholdMm` is already `double`, and new fields use `double?`.

---

## DI Registration

No new services. All handlers are already registered via MediatR assembly scanning.

---

## Verification

```bash
# Build
dotnet build StarkAgroAPI/StarkAgroAPI.csproj

# Tests
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj

# Manual — set user threshold
# PUT /api/v1/user/updateLimits  { "limiteInferior": 25, "limiteSuperior": 75, "rainThresholdMm": 10 }

# Manual — set pivot threshold
# PUT /api/v1/pivot/updateLimits  { "id": 1, "limiteInferior": null, "limiteSuperior": null, "rainThresholdMm": 3 }

# Hierarchy check
# pivot null, user 10 → uses 10
# pivot 3, user 10 → uses 3
# pivot null, user null → uses 5.0 from appsettings
```
