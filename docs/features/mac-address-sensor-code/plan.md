# Implementation Plan: MAC Address Sensor Code Validation
Generated: 2026-04-13

---

## Context

The Sensor entity has a `Code` field that represents the physical identifier of an ESP8266 field device. That code is the device's MAC address. Currently the field is a free-form string with no format enforcement, so invalid values (e.g., `"SENSOR-001"`) can be stored. This feature adds a `MacAddressAttribute` custom validation attribute that enforces the canonical MAC address format `XX:XX:XX:XX:XX:XX` (uppercase hex octets separated by colons), applies it to `CreateSensorRequest.Code` and `EditSensorRequest.Code`, validates in the handler for the edit path, and accompanies the change with a full behavioral test suite.

---

## Approach

No new CQRS operations are needed. The change is purely additive validation on two existing operations:

- **CreateSensor** — `POST v1/sensor/add`: `Code` becomes a required, MAC-format field.
- **EditSensor** — `PUT v1/sensor/update`: `Code` becomes an optional but MAC-format field when provided.

MongoDB collection touched: `sensors` (indirectly — the new constraint prevents invalid data reaching the collection; no schema migration is required since MongoDB is schema-less).

---

## API Endpoints

No new endpoints. The two affected endpoints already exist:

| Method | Route | Request source | Auth | UserId injection |
|--------|-------|----------------|------|-----------------|
| POST | `v1/sensor/add` | `[FromBody]` | Yes (`[Authorize]`) | Controller sets `command.UserId = GetCurrentUserId()` before `mediator.Send()` |
| PUT | `v1/sensor/update` | `[FromBody]` | Yes (`[Authorize]`) | Controller sets `command.UserId = GetCurrentUserId()` before `mediator.Send()` |

Both endpoints already call `if (!ModelState.IsValid) return CustomResponse(ModelState);` before delegating to MediatR, so the new attribute will be enforced automatically at the model-binding layer.

---

## Files to Create

### 1. `StarkAgroAPI/Validators/MacAddressAttribute.cs`
Type: Validator (custom `ValidationAttribute`)

Key members:
```
namespace StarkAgroAPI.Validators

public class MacAddressAttribute : ValidationAttribute
    private static readonly Regex MacRegex = new Regex(
        @"^([0-9A-F]{2}:){5}[0-9A-F]{2}$",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)

    public override bool IsValid(object? value)
        // returns false when value is null or empty
        // calls value.ToString()!.ToUpperInvariant() before matching so the
        // regex stays uppercase-only; the caller must store normalised form
        // NOTE: the attribute only validates format — uniqueness is enforced
        // in the handler (see handler changes below)

    public override string FormatErrorMessage(string name)
        // returns: "{name} must be a valid MAC address in the format XX:XX:XX:XX:XX:XX."
```

Regex explanation: `^([0-9A-F]{2}:){5}[0-9A-F]{2}$` — exactly six pairs of uppercase hex digits separated by five colons. The `IsValid` method must upper-case the input before matching so that both `5c:cf:7f:3a:54:29` and `5C:CF:7F:3A:54:29` are accepted.

Decision point for the implementer: the attribute should normalise (upper-case) the value for comparison purposes only; it must NOT mutate the model property inside `IsValid`. Normalisation of the stored value (convert to upper-case before saving) must happen inside the handlers.

---

### 2. `StarkAgroAPI.Tests/Validators/MacAddressAttributeTests.cs`
Type: Unit test

Required test cases:

| Method | Description |
|--------|-------------|
| `IsValid_ReturnsTrue_ForValidUppercaseMac` | `"5C:CF:7F:3A:54:29"` → true |
| `IsValid_ReturnsTrue_ForValidLowercaseMac` | `"5c:cf:7f:3a:54:29"` → true (case-insensitive via ToUpper normalisation) |
| `IsValid_ReturnsFalse_ForNull` | `null` → false |
| `IsValid_ReturnsFalse_ForEmptyString` | `""` → false |
| `IsValid_ReturnsFalse_ForDashSeparator` | `"5C-CF-7F-3A-54-29"` → false |
| `IsValid_ReturnsFalse_ForTooFewOctets` | `"5C:CF:7F:3A:54"` → false |
| `IsValid_ReturnsFalse_ForTooManyOctets` | `"5C:CF:7F:3A:54:29:FF"` → false |
| `IsValid_ReturnsFalse_ForNonHexChars` | `"ZZ:CF:7F:3A:54:29"` → false |
| `IsValid_ReturnsFalse_ForPlainString` | `"SENSOR-001"` → false |
| `FormatErrorMessage_ContainsFieldNameAndFormat` | message contains "Code" and "XX:XX:XX:XX:XX:XX" |

Follow the exact `[Theory] / [InlineData]` + `[Fact]` pattern from `EmailAttributeTests.cs`.

---

### 3. `StarkAgroAPI.Tests/Domain/Commands/Requests/Sensors/CreateSensorRequestTests_MacValidation.cs`

Type: Behavioral unit test — validates that `MacAddressAttribute` is wired to `CreateSensorRequest.Code` and that `DataAnnotations` validation fires correctly.

Required test cases:

| Method | Scenario |
|--------|----------|
| `Validate_ValidMac_NoValidationErrors` | request with `Code = "5C:CF:7F:3A:54:29"` passes `Validator.TryValidateObject` with zero errors |
| `Validate_InvalidCode_ReturnsValidationError` | request with `Code = "SENSOR-001"` fails with at least one validation error message referencing the MAC format |
| `Validate_NullCode_ReturnsValidationError` | `Code = null` fails because `[Required]` + `[MacAddress]` are both present |

Use `System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, ctx, results, true)` — this is the standard approach in the existing test suite (see `EmailAttributeTests.cs` pattern). Because `CreateSensorRequest` needs a populated `Pivot` to avoid other validation noise, set `Pivot = new Pivot { Id = 1 }` and `Quadrante = 1` in the Arrange phase.

---

### 4. `StarkAgroAPI.Tests/Domain/Commands/Requests/Sensors/EditSensorRequestTests_MacValidation.cs`

Type: Behavioral unit test — mirrors file 3 but for `EditSensorRequest`.

Required test cases:

| Method | Scenario |
|--------|----------|
| `Validate_ValidMac_NoValidationErrors` | `Code = "AA:BB:CC:DD:EE:FF"` passes validation |
| `Validate_InvalidCode_ReturnsValidationError` | `Code = "NOT-A-MAC"` fails |
| `Validate_NullCode_IsAllowed_WhenNotRequired` | If `Code` on `EditSensorRequest` is nullable and `[MacAddress]` is applied without `[Required]`, then `Code = null` should pass — flag this as an open design question (see Risks section). |

---

### 5. `StarkAgroAPI.Tests/Domain/Handlers/Sensors/CreateSensorHandlerTests_MacNormalisation.cs`

Type: Behavioral unit test — verifies that the handler stores the code in normalised (uppercase) form.

Required test cases:

| Method | Scenario |
|--------|----------|
| `Handle_NormalisesCodeToUppercase` | Pass `Code = "5c:cf:7f:3a:54:29"`, capture the `Sensor` argument to `InsertOneAsync` via `It.IsAny<Sensor>()` Moq callback, assert `sensor.Code == "5C:CF:7F:3A:54:29"` |
| `Handle_AlreadyUppercaseCode_StoredUnchanged` | Pass `Code = "5C:CF:7F:3A:54:29"`, assert stored value is identical |

Use the same Moq setup pattern as `CreateSensorHandlerTests.cs` (mock `agpDBContext`, `IMongoCollection<Sensor>`, `ICurrentUserContext`). Use `mockSensors.Setup(...).Callback<Sensor, InsertOneOptions, CancellationToken>((s, _, _) => capturedSensor = s)` to capture the inserted entity.

---

### 6. `StarkAgroAPI.Tests/Domain/Handlers/Sensors/EditSensorHandlerTests_MacNormalisation.cs`

Type: Behavioral unit test — same normalisation check for the edit path.

Required test cases:

| Method | Scenario |
|--------|----------|
| `Handle_NormalisesCodeToUppercase_OnEdit` | Pass `Code = "aa:bb:cc:dd:ee:ff"`, capture the sensor passed to `ReplaceOneAsync`, assert `sensor.Code == "AA:BB:CC:DD:EE:FF"` |
| `Handle_NullCode_IsStoredAsNull` | Pass `Code = null` (nullable on edit), assert the replaced sensor has `Code == null` |

---

## Files to Modify

### 1. `StarkAgroAPI/Domain/Commands/Requests/Sensors/CreateSensorRequest.cs`

Changes:
- Add `using System.ComponentModel.DataAnnotations;`
- Add `using StarkAgroAPI.Validators;`
- Decorate `Code` with `[Required]` and `[MacAddress]`
- Change the `Code` property type from `string` (non-nullable) to `string?` with `[Required]` annotation, OR keep `string` and mark as required — remain consistent with `User.cs` Email pattern.

Before: `public string Code { get; set; }`
After: `[Required] [MacAddress] public string Code { get; set; }`

Note: the `Code` field is already typed as non-nullable `string`. Adding `[Required]` ensures the model binder rejects a missing or null `Code`. The `[MacAddress]` attribute enforces format. No property type change is strictly necessary.

---

### 2. `StarkAgroAPI/Domain/Commands/Requests/Sensors/EditSensorRequest.cs`

Changes:
- Add `using System.ComponentModel.DataAnnotations;`
- Add `using StarkAgroAPI.Validators;`
- Decorate `Code` with `[MacAddress]` (but NOT `[Required]` — on edit, omitting Code is acceptable; see Risks).

Before: `public string? Code { get; set; }`
After: `[MacAddress] public string? Code { get; set; }`

Important: since `Code` is nullable on the edit request, `MacAddressAttribute.IsValid` must return `true` when `value` is `null` so that omitting the field on a partial update passes validation. If the business rule is "Code is always required even on edit", change the attribute to return `false` on null and add `[Required]`. This is flagged in Risks.

---

### 3. `StarkAgroAPI/Domain/Handlers/Sensors/CreateSensorHandler.cs`

Changes:
- In the `Handle` method, before constructing the `Sensor` entity, normalise the code:
  ```
  var normalisedCode = request.Code?.ToUpperInvariant();
  ```
- Assign `Code = normalisedCode` instead of `Code = request.Code`.

No new constructor dependencies needed.

---

### 4. `StarkAgroAPI/Domain/Handlers/Sensors/EditSensorHandler.cs`

Changes:
- In the `Handle` method, after the null pivot check, normalise code before assigning to the entity:
  ```
  sensor.Code = request.Code?.ToUpperInvariant();
  ```
- Replace the existing `sensor.Code = request.Code;` assignment.

No new constructor dependencies needed.

---

### 5. `StarkAgroAPI.Tests/Domain/Commands/Requests/Sensors/CreateSensorRequestTests.cs`

Changes:
- The existing test `Can_Set_And_Get_Properties` uses `Code = "SENSOR-001"`. After adding `[MacAddress]`, this value is still settable on the POCO (the attribute is only checked during model validation, not on property assignment). The test does NOT use `Validator.TryValidateObject`, so it will continue to pass unchanged.
- However, if the implementer introduces `[Required]` and the test checks `Default_Values_Are_Correct` with `Assert.Null(request.Code)`, this will still pass because `[Required]` is not evaluated in the default-value test.
- No test changes required for this file — but the implementer must verify after running `dotnet test`.

---

### 6. `StarkAgroAPI.Tests/Domain/Handlers/Sensors/CreateSensorHandlerTests.cs`

Changes:
- The existing tests pass `Code = "SENSOR-1"`. After the handler normalises code to upper-case, `"SENSOR-1".ToUpperInvariant()` == `"SENSOR-1"`, so existing assertions `Assert.Equal(123, result.Id)` remain correct.
- No change required, but the implementer must verify.

---

### 7. `StarkAgroAPI.Tests/Domain/Handlers/Sensors/EditSensorHandlerTests.cs`

Changes:
- Existing test `Handle_Updates_Sensor_And_Returns_Response` asserts `Assert.Equal("NEW", sensor.Code)`. After normalisation, `"NEW".ToUpperInvariant()` == `"NEW"`, so this still passes.
- No change required, but the implementer must verify.

---

### 8. `StarkAgroAPI.Tests/Controllers/SensorControllerTests.cs`

Changes:
- Existing tests pass `Code = "S001"` in `CreateSensorRequest`. This is NOT a valid MAC address. The controller test does not invoke the real model validation pipeline (`ModelState` is only manually manipulated), so these tests are isolated from the new attribute.
- The `Add_InvalidModelState_ReturnsBadRequest` test manually adds a model error and checks the response — this remains valid.
- No change required. However, the implementer SHOULD add two new controller-level test cases to `SensorControllerTests.cs` (see behavioral tests below).

Additional test cases to add inline to `SensorControllerTests.cs`:

| Method | Scenario |
|--------|----------|
| `Add_WithValidMacCode_ReturnsSuccess` | Sets `Code = "5C:CF:7F:3A:54:29"`, no model errors, mediator returns response — asserts success |
| `Update_WithInvalidMacCode_ModelStateError_ReturnsBadRequest` | Manually adds MAC format model error, asserts `BadRequestObjectResult` |

These test that the controller wiring (ModelState → CustomResponse) works with MAC-specific error messages.

---

## MongoDB Operations

No new MongoDB operations are introduced. The existing `InsertOneAsync` in `CreateSensorHandler` and `ReplaceOneAsync` in `EditSensorHandler` handle persistence unchanged. The only MongoDB-visible effect is that the `Code` field will now always contain uppercase colon-separated hex (or null on edit when omitted).

Index recommendation: if sensor lookups by code are frequent (e.g., an ESP8266 device registering itself by MAC), consider a unique sparse index on `sensors.Code`. This is not required for the current issue but should be noted.

Collection: `sensors`
No filter or update shape changes.

---

## Validation & Error Handling

### Validation layers

| Layer | Mechanism | Fires when |
|-------|-----------|-----------|
| Model binding (controller) | `[Required]` + `[MacAddress]` data annotations on `CreateSensorRequest.Code` | `POST v1/sensor/add` is called with missing or invalid Code |
| Model binding (controller) | `[MacAddress]` on `EditSensorRequest.Code` | `PUT v1/sensor/update` is called with a non-null, non-MAC Code |
| Handler (business rule) | Upper-case normalisation before insert/update | Always, after model is valid |

### Error message

`MacAddressAttribute.FormatErrorMessage` must return a message such as:
`"Code must be a valid MAC address in the format XX:XX:XX:XX:XX:XX."`

This message flows through `NotifyErrorModelInvalid` → `INotifier.Handle` → `CustomResponse()` → HTTP 400 with `{ "errors": ["Code must be a valid MAC address..."] }`.

### No handler-level INotifier usage needed

Because the MAC format check is a structural (not business-rule) validation, it belongs entirely in the attribute layer. The handlers do NOT need to inject `INotifier`. Business-rule examples that would require `INotifier` in the handler would be: "Code already in use by another sensor" (uniqueness). That check is out of scope for this issue.

---

## Risks & Flags

### R1 — EditSensorRequest.Code nullability design ambiguity (flag)

The issue does not specify whether `Code` is mandatory on edit. Currently `EditSensorRequest.Code` is `string?`. Two interpretations:

- **Partial update**: Code is optional on edit; omitting it keeps the existing stored value. The handler would need to skip updating `sensor.Code` when `request.Code == null`. The current handler always overwrites `sensor.Code`, so sending `null` would erase the stored MAC.
- **Full update**: Code is always required on edit. Add `[Required]` to `EditSensorRequest.Code` and change it to `string`.

The current handler implementation (`sensor.Code = request.Code;`) overwrites unconditionally, so the safest approach matching the existing behaviour is to add `[MacAddress]` without `[Required]` and document that clients must re-send the current Code on every edit. The implementer must confirm this with the product owner before merging.

### R2 — User isolation gap in EditSensorHandler (pre-existing, flag)

`EditSensorHandler` finds the sensor by `Id` only — it does NOT filter by the current user's `UserId`. This means a user who knows another user's sensor `Id` can overwrite it. This is a pre-existing security gap, not introduced by this feature, but should be flagged for a follow-up issue. The fix would be to inject `ICurrentUserContext` into `EditSensorHandler` and add `&& Eq(x => x.UserId, userId)` to the Find filter.

### R3 — CreateSensorRequest.Code was non-nullable before this change

Currently `public string Code { get; set; }` (no `?`). Adding `[Required]` is redundant for non-nullable reference types in .NET with NRTs enabled, but is still useful for model binding (ensures the JSON body explicitly includes the field). The implementer should verify the project's `<Nullable>enable</Nullable>` setting in the `.csproj` before deciding whether to keep `[Required]` explicit.

### R4 — Existing tests use `Code = "SENSOR-001"` (safe, but misleading)

After this change, `"SENSOR-001"` is no longer a valid Code value. The existing unit tests that use it do NOT invoke the model validation pipeline, so they will continue to pass. However they become misleading examples. The implementer should update the `Code` values in unit test `Arrange` phases to use a real MAC address like `"5C:CF:7F:3A:54:29"` for consistency. This is a cleanup task, not a correctness requirement.

### R5 — Case normalisation responsibility

The attribute validates case-insensitively (by converting to upper-case before matching). The handler normalises to upper-case before storing. This means clients may send lowercase MACs and they will be accepted and stored as uppercase. Confirm this is the desired UX. If only uppercase should be accepted at the API level, remove the ToUpperInvariant() call from `IsValid` and keep the regex strictly uppercase.

---

## DI Registration

No new services are introduced. `MacAddressAttribute` is a `ValidationAttribute` subclass; it is instantiated by the .NET model binding infrastructure automatically. No entry in `Configuration/DependencyInjectionConfig.cs` is required.

---

## Verification

### Step 1 — Run the full existing test suite first (baseline)

```bash
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --logger "console;verbosity=normal"
```

All tests must be green before applying changes.

### Step 2 — Apply changes, then run tests again

```bash
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --logger "console;verbosity=normal"
```

Expected result: all pre-existing tests still pass; new test files add green tests.

### Step 3 — Run only the validator tests in isolation

```bash
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --filter "FullyQualifiedName~MacAddressAttributeTests"
```

### Step 4 — Run only the sensor handler tests

```bash
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --filter "FullyQualifiedName~Sensors"
```

### Step 5 — Manual API call (requires running stack)

Create sensor with valid MAC:
```
POST http://localhost:5000/v1/sensor/add
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Sensor Norte",
  "code": "5c:cf:7f:3a:54:29",
  "quadrante": 1,
  "pivot": { "id": 1 }
}
```
Expected: HTTP 200, `{ "id": <new_id> }`. Stored `Code` in MongoDB must be `"5C:CF:7F:3A:54:29"` (uppercase).

Create sensor with invalid code:
```
POST http://localhost:5000/v1/sensor/add
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Sensor Norte",
  "code": "SENSOR-001",
  "quadrante": 1,
  "pivot": { "id": 1 }
}
```
Expected: HTTP 400, `{ "errors": ["Code must be a valid MAC address in the format XX:XX:XX:XX:XX:XX."] }`.

---

## Implementation Order

The implementer must follow this order to avoid broken intermediary states:

1. Create `MacAddressAttribute.cs`
2. Run `dotnet test` — all existing tests still pass (no changes to requests yet)
3. Create `MacAddressAttributeTests.cs`, run tests — new tests pass
4. Modify `CreateSensorRequest.cs` and `EditSensorRequest.cs` (add attributes)
5. Modify `CreateSensorHandler.cs` and `EditSensorHandler.cs` (add normalisation)
6. Run `dotnet test` — confirm all existing tests still pass (they will, for the reasons explained in R4)
7. Create the behavioral test files (files 3–6 above) and `SensorControllerTests.cs` additions
8. Run `dotnet test` — all new and old tests green
9. If any test fails, diagnose before proceeding

---

## Reference Files the Implementer Must Read Before Coding

- `StarkAgroAPI/Validators/EmailAttribute.cs` — exact pattern for a custom `ValidationAttribute`
- `StarkAgroAPI/Domain/Commands/Requests/Sensors/CreateSensorRequest.cs` — property to decorate
- `StarkAgroAPI/Domain/Commands/Requests/Sensors/EditSensorRequest.cs` — property to decorate
- `StarkAgroAPI/Domain/Handlers/Sensors/CreateSensorHandler.cs` — normalisation insertion point
- `StarkAgroAPI/Domain/Handlers/Sensors/EditSensorHandler.cs` — normalisation insertion point
- `StarkAgroAPI.Tests/Validators/EmailAttributeTests.cs` — test structure to mirror
- `StarkAgroAPI.Tests/Domain/Handlers/Sensors/CreateSensorHandlerTests.cs` — handler test pattern
- `StarkAgroAPI.Tests/Domain/Handlers/Sensors/EditSensorHandlerTests.cs` — handler test pattern
- `StarkAgroAPI.Tests/Helpers/MongoMockHelper.cs` — Moq helpers available for handler tests
- `StarkAgroAPI.Tests/Mocks/MockNotifier.cs` — available mock (not needed for this feature but part of the controller test setup)
