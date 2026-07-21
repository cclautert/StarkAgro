Issue: https://github.com/cclautert/StarkAgro/issues/6

# NDVI F1 — MonitoredArea + CRUD + geometria (círculo e polígono)

## Context

Base do épico NDVI Sentinel-2: cadastrar as **áreas** (talhões) que o agricultor quer monitorar. Hoje só há geometria de ponto (`Pivot`). Aceita **círculo** (centro+raio) e **polígono livre**, ambos guardados como `GeoJsonPolygon` (o front aproxima o círculo a um polígono antes de enviar). Esta fase entrega CRUD + geometria — **sem** buscar NDVI. Branch nova a partir de `main` (épico independente das Revendas).

Confirmado: MongoDB.Driver 3.6.0 tem `MongoDB.Driver.GeoJsonObjectModel` (`GeoJsonPolygon<GeoJson2DGeographicCoordinates>`, `Geo2DSphere`).

## Acceptance Criteria → decisão de implementação

| Critério | Implementação |
|---|---|
| Usuário cria/edita/lista/apaga áreas isoladas por tenant | Handlers CQRS em `Domain/Handlers/Ndvi/`, todos filtram `x.UserId == _currentUser.UserId`; `NdviController` `[Authorize]` |
| Círculo e polígono persistidos com `Geometry` GeoJSON válida + índice `2dsphere` | `MonitoredArea.Geometry` = `GeoJsonPolygon`; índice `Geo2DSphere` no `agpDBContext`. O front sempre manda um **anel** (lista de pontos); círculo guarda também os campos de round-trip |
| Teste garante ordem `[lng,lat]` e rejeição de polígono inválido | `MonitoredAreaGeometry` (fábrica pura + validação) constrói o polígono com `GeoJson2DGeographicCoordinates(lng, lat)`; testes asseguram a ordem e a rejeição |
| `dotnet test` verde | Testes xUnit por handler + pela fábrica de geometria |

## Affected layers

Entidade + constantes, `agpDBContext` (coleção + índices incl. `2dsphere`), fábrica/validação de geometria, handlers, DTOs, `NdviController`. Sem worker (fica p/ #7). **UI adiada** (ver escopo).

## New REST endpoints (`NdviController`, `[Authorize]`, `Route("v1/areas")`)

| Método | Rota | Request | Response |
|---|---|---|---|
| GET | `/areas` | — | `List<MonitoredAreaResponse>` |
| POST | `/areas` | `CreateMonitoredAreaRequest` | `MonitoredAreaResponse` (201) |
| GET | `/areas/{id}` | — | `MonitoredAreaResponse` |
| PUT | `/areas/{id}` | `EditMonitoredAreaRequest` | `MonitoredAreaResponse` |
| DELETE | `/areas/{id}` | — | 204 |

## Files to create

- `Models/Entities/MonitoredArea.cs` (coleção `monitored_areas`) — `Entity`: `int UserId`, `Name`, `string? Crop`, `string AreaKind`, campos de círculo (`double? CenterLat/CenterLng/RadiusM/Altitude`, `string? LocationAddress`), `GeoJsonPolygon<GeoJson2DGeographicCoordinates> Geometry`, e as flags dormentes do worker (`bool MonitoringEnabled`, `DateTime? NextFetchAt/LastFetchAt`, `string? LastAcquisitionDate`, `string Status`, `DateTime? ProcessingStartedAt`, `string? WorkerId`, `int RetryCount`, `DateTime? NextAttemptAt`, `string? FailureReason`, `DateTime CreatedAt/UpdatedAt`). + `MonitoredAreaKind` (`Circle`/`Polygon`) e `MonitoredAreaStatus` (`Idle`/`Queued`/`Fetching`/`Failed`).
- `Services/Ndvi/MonitoredAreaGeometry.cs` — fábrica **pura e testável**: `bool TryBuild(IReadOnlyList<GeoCoordinate> ring, out GeoJsonPolygon<...> polygon, out string? error)` e `List<GeoCoordinate> ToRing(GeoJsonPolygon<...>)`. Validação: ≥3 pontos distintos, `lat∈[-90,90]`/`lng∈[-180,180]`, auto-fecha o anel, `≤ MaxVertices (500)`, bbox `≤ MaxSpanDegrees (0.5°)` (rejeita polígono gigante) e auto-interseção básica das arestas.
- `Domain/Commands/Requests/Ndvi/*.cs` — `GeoCoordinate {double Lat; double Lng;}`, `Create/Edit/Get/Delete/ListMonitoredAreaRequest`.
- `Domain/Commands/Responses/Ndvi/MonitoredAreaResponse.cs`.
- `Domain/Handlers/Ndvi/MonitoredAreaHandlers.cs` — `Create/Edit/Delete/List/GetMonitoredAreaHandler` + mapper.
- `Controllers/NdviController.cs`.
- Testes espelhando os acima (+ teste dedicado da fábrica de geometria).

## Files to modify

- `Models/agpDBContext.cs` — coleção `MonitoredAreas` (`monitored_areas`) + índices `{UserId}`, `{MonitoringEnabled, NextFetchAt}`, `{Status, NextAttemptAt}`, e `Geo2DSphere` em `Geometry`.
- `CLAUDE.md` — nova coleção + a armadilha do `[lng,lat]`.

## MongoDB changes

Coleção nova `monitored_areas`. IDs via `GetNextIdAsync(nameof(MonitoredArea))`. Índices acima (incl. `2dsphere`). Geometria guardada como GeoJSON pelo serializer do driver.

## Tenant isolation plan

Toda query filtra `x.UserId == _currentUser.UserId`; `UserId` vem do `ICurrentUserContext`, nunca do request. Create grava `UserId = _currentUser.UserId`. Edit/Get/Delete casam `Id` **e** `UserId` (um usuário não toca área de outro).

## Risks & flags

- **Ordem `[lng,lat]`**: fonte clássica de bug — `GeoJson2DGeographicCoordinates(lng, lat)`. Teste dedicado assegura que o polígono construído tem `coordinates[0].Longitude == ring[0].Lng`.
- **Serialização GeoJSON**: o driver serializa `GeoJsonPolygon` como GeoJSON válido (2dsphere funciona); os testes mockam o Mongo, então cobrem a fábrica/validação, não o I/O.
- **Auto-interseção**: checagem básica de cruzamento de arestas (O(n²), n pequeno) — não é validação topológica completa; documentado.
- **Polígono gigante**: bloqueado por `MaxSpanDegrees`/`MaxVertices` (guarda o custo de PU nas fases seguintes).

## DI registration

Handlers via scan do MediatR. `MonitoredAreaGeometry` é estático/puro (sem DI). Sem serviço novo a registrar nesta fase.

## Verification

- `dotnet build StarkAgro.sln` + `dotnet test StarkAgro.sln` (solution inteira).
- Cobertura ≥90% nos arquivos novos.
- HTTP: `POST /v1/areas {name, areaKind:"Polygon", ring:[{lat,lng}...]}` → 201; `GET /v1/areas` só devolve as áreas do tenant; polígono inválido → 400 com notificação.

## Escopo (a confirmar)

**Backend + API apenas** nesta fase; as telas Angular (`areas`, `areas/nova`, `areas/editar/:id` com círculo reusando `pivot-location-map` e polígono via `leaflet-geoman`) ficam para um follow-up — mesma abordagem das fases de Revenda. Confirmar.
