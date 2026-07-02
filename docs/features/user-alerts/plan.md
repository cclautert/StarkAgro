# Implementation Plan: Central de notificações — endpoints user/alerts
Issue: (sem issue — diagnóstico em sessão: sininho do UI chama endpoint inexistente, 404 em produção)
Generated: 2026-07-02

## Context
O painel de notificações do UI (`AlertService`) consulta `GET /api/v1/user/alerts` a cada 60 s e chama `POST /api/v1/user/alerts/mark-read`, mas nenhum dos dois existe na API — o painel sempre exibiu "Sem conexão". Os dados já existem nas coleções `irrigation_alerts` (worker) e `sensor_anomalies` (detector de anomalias).

## Acceptance Criteria
1. `GET v1/user/alerts` retorna os alertas do usuário logado no contrato `UserAlert` do UI (`id, title, pivotName, alertType, createdAt, isRead`) → junta `irrigation_alerts` + `sensor_anomalies` dos últimos 30 dias, ordena por data desc, máx. 50 itens.
2. `alertType` mapeado para os valores que o UI conhece: `IrrigationAlert` → `"MoistureLow"`; `SensorAnomaly` → `"AnomalyPersisted"` (ver `alert.model.ts`).
3. `id` string única entre coleções: `"irrigation-{Id}"` / `"anomaly-{Id}"`.
4. `title` em pt-BR, espelhando os textos dos pushes: irrigação `"Umidade projetada {x}% < limite {y}%"`; anomalia `"Sensor {code} — Quadrante {q}: {v}% fora da faixa ({min}%–{max}%)"`.
5. `pivotName` via join na coleção `pivots` (fallback `"Pivô {id}"`).
6. `POST v1/user/alerts/mark-read` grava `AlertsReadAt = UtcNow` no usuário; `isRead = alerta.Date <= AlertsReadAt`.
7. Sem mudanças no Angular — o contrato já está implementado no UI (JSON camelCase padrão do ASP.NET Core).

## Affected Layers
handlers / controllers / entities (campo novo em `User`) — sem mudanças em agpDBContext (coleções já registradas), Angular UI ou worker.

## New REST Endpoints
| Method | Route | Auth | Request fields | Response fields |
|---|---|---|---|---|
| GET | `v1/user/alerts` | `[Authorize]` (herdado do controller) | — | `[{ id, title, pivotName, alertType, createdAt, isRead }]` |
| POST | `v1/user/alerts/mark-read` | `[Authorize]` | `{}` (vazio) | 204 NoContent |

## Files to Create
| Path | Type | Summary |
|---|---|---|
| `AgripeWebAPI/Domain/Commands/Requests/Users/GetUserAlertsRequest.cs` | Request | `IRequest<IList<UserAlertResponse>>`, sem campos |
| `AgripeWebAPI/Domain/Commands/Requests/Users/MarkAlertsReadRequest.cs` | Request | `IRequest<Unit>`, sem campos |
| `AgripeWebAPI/Domain/Commands/Responses/Users/UserAlertResponse.cs` | Response DTO | `Id (string), Title, PivotName, AlertType, CreatedAt (DateTime), IsRead (bool)` |
| `AgripeWebAPI/Domain/Handlers/Users/GetUserAlertsHandler.cs` | Handler | Busca, junta, mapeia, ordena, limita |
| `AgripeWebAPI/Domain/Handlers/Users/MarkAlertsReadHandler.cs` | Handler | `UpdateOne` set `AlertsReadAt` |
| `AgripeWebAPI.Tests/Domain/Handlers/Users/GetUserAlertsHandlerTests.cs` | Testes | cursor mocking padrão |
| `AgripeWebAPI.Tests/Domain/Handlers/Users/MarkAlertsReadHandlerTests.cs` | Testes | cursor mocking padrão |

## Files to Modify
| Path | Change |
|---|---|
| `AgripeWebAPI/Controllers/UserController.cs` | + `[HttpGet] Route("alerts")` e `[HttpPost] Route("alerts/mark-read")` (padrão fino existente) |
| `AgripeWebAPI/Models/Entities/User.cs` | + `public DateTime? AlertsReadAt { get; set; }` |

## MongoDB Changes
Campo novo opcional `AlertsReadAt` (DateTime?) em `users` — nullable, sem migração/índice. Nenhuma coleção nova.

## Tenant Isolation Plan
- `GetUserAlertsHandler`: injeta `ICurrentUserContext`; filtra `irrigation_alerts` e `sensor_anomalies` por `UserId == _currentUser.UserId` (padrão `GetListSensorHandler`); joins em `pivots`/`sensors` restritos aos IDs referenciados pelos alertas do próprio usuário; lê `AlertsReadAt` do próprio usuário.
- `MarkAlertsReadHandler`: injeta `ICurrentUserContext`; `UpdateOne(u => u.Id == _currentUser.UserId, set AlertsReadAt)`. Nenhum `request.UserId` de cliente é usado.

## Risks & Flags
- [WARNING] Serialização: UI espera camelCase — padrão do ASP.NET Core System.Text.Json, nada a fazer; `CreatedAt` DateTime serializa ISO-8601, compatível com `date` pipe do Angular.
- [WARNING] `SensorAnomaly` tem `Acknowledged` — não usar para isRead (semântica distinta, usada pelo detector); leitura é via `AlertsReadAt` do usuário.
- [INFO] Worker referencia o projeto da API — campo novo em `User` é compatível (nullable).

## DI Registration
Nenhuma — handlers MediatR são descobertos pelo assembly scan existente em `ApiConfig`.

## Verification
```bash
dotnet build AgripeWebAPI/AgripeWebAPI.csproj
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj
```
Produção (após deploy): `GET https://agripeweb.com/api/v1/user/alerts` sem token → 401 (hoje: 404); com login no app, sininho lista alertas e badge zera após abrir o painel (mark-read).
