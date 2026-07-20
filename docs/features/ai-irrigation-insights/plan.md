# AI Irrigation Insights

Issue: https://github.com/cclautert/StarkAgro/issues/26

## Context

`POST /api/v1/pivot/{id}/ai-insights` collects pivot context (sensor readings, limits, weather forecast, unacknowledged anomalies) and calls the Anthropic Messages API to produce a plain-text agronomist recommendation in PT-BR. Responses are cached 30 min per pivot in `IMemoryCache` to avoid redundant API calls.

## Acceptance Criteria

| Criterion | Implementation |
|---|---|
| PT-BR response | System prompt in PT-BR, agronomist persona |
| 30-min cache per pivot | `IMemoryCache` key `ai-insights:{pivotId}` |
| API key never in code | `AISettings.AnthropicApiKey` via `appsettings` / env `AI__AnthropicApiKey` |
| UI shows generation timestamp | `PivotAIInsightsResponse.GeneratedAt` + `FromCache` |
| 503 when Claude unavailable | `CustomResponse(null, HttpStatusCode.ServiceUnavailable)` |
| Unit tests with mock IAIInsightsService | `Mock<IAIInsightsService>` in handler tests |

## Affected Layers

- **New service**: `ClaudeInsightsService` — direct `HttpClient` to Anthropic Messages API (no external SDK)
- **New handler**: `GetPivotAIInsightsHandler`
- **Controller**: `PivotController` — `POST {pivotId}/ai-insights`
- **Config**: `AISettings.cs` registered in `ApiConfig.cs`
- **Angular**: `dashboard.component.*`, `pivot.service.ts`

## New REST Endpoint

| | |
|---|---|
| Method | `POST` |
| Route | `/api/v1/pivot/{pivotId}/ai-insights` |
| Auth | Bearer JWT |
| Body | empty |
| Response 200 | `{ insights, generatedAt, fromCache }` |
| Response 503 | `{ errors: ["Assistente IA indisponível..."] }` |

## Files Created

- `StarkAgroAPI/Configuration/AISettings.cs`
- `StarkAgroAPI/Models/Interfaces/IAIInsightsService.cs`
- `StarkAgroAPI/Services/AIInsights/PivotAIContext.cs`
- `StarkAgroAPI/Services/AIInsights/ClaudeInsightsService.cs`
- `StarkAgroAPI/Domain/Commands/Requests/Pivots/GetPivotAIInsightsRequest.cs`
- `StarkAgroAPI/Domain/Commands/Responses/Pivots/PivotAIInsightsResponse.cs`
- `StarkAgroAPI/Domain/Handlers/Pivots/GetPivotAIInsightsHandler.cs`
- `StarkAgroAPI.Tests/Domain/Handlers/Pivots/GetPivotAIInsightsHandlerTests.cs`

## Files Modified

- `StarkAgroAPI/Configuration/ApiConfig.cs` — `AISettings`, `HttpClient<ClaudeInsightsService>`, `IAIInsightsService`
- `StarkAgroAPI/appsettings.Development.json` — `AI` section with placeholder key
- `StarkAgroAPI/Controllers/PivotController.cs` — `POST {pivotId}/ai-insights`
- `StarkAgroUI/src/app/services/pivot.service.ts` — `getAIInsights()`
- `StarkAgroUI/src/app/components/dashboard/dashboard.component.*` — AI insights panel

## MongoDB Changes

**None** — reads from existing collections only.

## Tenant Isolation

Handler verifies `pivot.UserId == _currentUser.UserId` before collecting any data. `UserId` always comes from JWT via `ICurrentUserContext`, never from the client request body.

## DI Registration

```csharp
services.Configure<AISettings>(configuration.GetSection(AISettings.SectionName));
services.AddHttpClient<IAIInsightsService, ClaudeInsightsService>(client => {
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

## Context Sent to Claude

- Pivot name + limits
- Last 48 readings per sensor (up to 6 sensors) from the past 48h
- 7-day weather forecast if coordinates available
- Up to 10 unacknowledged anomalies
