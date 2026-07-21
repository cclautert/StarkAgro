# Graph Report - StarkAgro  (2026-07-20)

## Corpus Check
- 645 files · ~234,568 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 211 nodes · 443 edges · 12 communities (11 shown, 1 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 12 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `beaed46a`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_MonitoredAreaResponse|MonitoredAreaResponse]]
- [[_COMMUNITY_.TryBuild|.TryBuild]]
- [[_COMMUNITY_StarkAgroAPI.Models|StarkAgroAPI.Models]]
- [[_COMMUNITY_CLAUDE|CLAUDE.md]]
- [[_COMMUNITY_MonitoredAreaHandlersTests|MonitoredAreaHandlersTests]]
- [[_COMMUNITY_agpDBContext|agpDBContext]]
- [[_COMMUNITY_NdviController|NdviController]]
- [[_COMMUNITY_NDVI F1 — MonitoredArea + CRUD + geometria (círculo e polígono)|NDVI F1 — MonitoredArea + CRUD + geometria (círculo e polígono)]]
- [[_COMMUNITY_NdviControllerTests|NdviControllerTests]]
- [[_COMMUNITY_MonitoredArea|MonitoredArea]]
- [[_COMMUNITY_Deploy — starkcompany.com.br (VPS 2.25.140.180)|Deploy — starkcompany.com.br (VPS 2.25.140.180)]]
- [[_COMMUNITY_redeploy-starkcompany.sh|redeploy-starkcompany.sh]]

## God Nodes (most connected - your core abstractions)
1. `agpDBContext` - 27 edges
2. `MonitoredAreaResponse` - 21 edges
3. `MonitoredAreaHandlersTests` - 17 edges
4. `NDVI F1 — MonitoredArea + CRUD + geometria (círculo e polígono)` - 13 edges
5. `MonitoredAreaGeometryTests` - 12 edges
6. `GeoCoordinate` - 10 edges
7. `NdviControllerTests` - 9 edges
8. `MonitoredArea` - 9 edges
9. `StarkAgroAPI.Models` - 9 edges
10. `Architecture` - 8 edges

## Surprising Connections (you probably didn't know these)
- `CreateMonitoredAreaRequest` --references--> `GeoCoordinate`  [EXTRACTED]
  StarkAgroAPI/Domain/Commands/Requests/Ndvi/MonitoredAreaRequests.cs → StarkAgroAPI/Models/GeoCoordinate.cs
- `EditMonitoredAreaRequest` --references--> `GeoCoordinate`  [EXTRACTED]
  StarkAgroAPI/Domain/Commands/Requests/Ndvi/MonitoredAreaRequests.cs → StarkAgroAPI/Models/GeoCoordinate.cs
- `MonitoredAreaResponse` --references--> `GeoCoordinate`  [EXTRACTED]
  StarkAgroAPI/Domain/Commands/Responses/Ndvi/MonitoredAreaResponse.cs → StarkAgroAPI/Models/GeoCoordinate.cs
- `ListMonitoredAreasHandler` --references--> `agpDBContext`  [EXTRACTED]
  StarkAgroAPI/Domain/Handlers/Ndvi/MonitoredAreaHandlers.cs → StarkAgroAPI/Models/agpDBContext.cs
- `GetMonitoredAreaHandler` --references--> `agpDBContext`  [EXTRACTED]
  StarkAgroAPI/Domain/Handlers/Ndvi/MonitoredAreaHandlers.cs → StarkAgroAPI/Models/agpDBContext.cs

## Import Cycles
- None detected.

## Communities (12 total, 1 thin omitted)

### Community 0 - "MonitoredAreaResponse"
Cohesion: 0.18
Nodes (22): INotifier, IRequest, IRequestHandler, List, CreateMonitoredAreaRequest, DeleteMonitoredAreaRequest, EditMonitoredAreaRequest, GetMonitoredAreaRequest (+14 more)

### Community 1 - ".TryBuild"
Cohesion: 0.15
Nodes (13): double, InlineData, int, IReadOnlyList, GeoCoordinate, GeoJson2DGeographicCoordinates, GeoJsonPolygon, List (+5 more)

### Community 2 - "StarkAgroAPI.Models"
Cohesion: 0.11
Nodes (14): StarkAgroAPI.Domain.Commands.Requests.Ndvi, StarkAgroAPI.Tests.Domain.Handlers.Ndvi, StarkAgroAPI.Tests.Models.Entities, StarkAgroAPI.Services.Ndvi, StarkAgroAPI.Models, StarkAgroAPI.Domain.Commands.Responses.Ndvi, StarkAgroAPI.Tests.Controllers, StarkAgroAPI.Tests.Services.Ndvi (+6 more)

### Community 3 - "CLAUDE.md"
Cohesion: 0.08
Nodes (22): API, API — CQRS (MediatR), Architecture, Authentication, Build and run, Components, Docker / CI / cloud, Documentation and skills (+14 more)

### Community 4 - "MonitoredAreaHandlersTests"
Cohesion: 0.36
Nodes (6): Mock, Fact, ICurrentUserContext, List, Task, MonitoredAreaHandlersTests

### Community 5 - "agpDBContext"
Cohesion: 0.10
Nodes (19): AgronomistClient, DiagnosisPlan, IGridFSBucket, IMongoCollection, IrrigationAlert, IrrigationProposal, Pivot, PlantDiagnosis (+11 more)

### Community 6 - "NdviController"
Cohesion: 0.27
Nodes (11): ActionResult, HttpDelete, HttpGet, HttpPost, HttpPut, IMediator, MainController, CancellationToken (+3 more)

### Community 7 - "NDVI F1 — MonitoredArea + CRUD + geometria (círculo e polígono)"
Cohesion: 0.14
Nodes (13): Acceptance Criteria → decisão de implementação, Affected layers, Context, DI registration, Escopo (a confirmar), Files to create, Files to modify, MongoDB changes (+5 more)

### Community 8 - "NdviControllerTests"
Cohesion: 0.53
Nodes (3): Fact, Task, NdviControllerTests

### Community 9 - "MonitoredArea"
Cohesion: 0.25
Nodes (8): Entity, DateTime, GeoJson2DGeographicCoordinates, GeoJsonPolygon, MonitoredArea, MonitoredAreaKind, MonitoredAreaStatus, string

### Community 10 - "Deploy — starkcompany.com.br (VPS 2.25.140.180)"
Cohesion: 0.29
Nodes (6): Arquitetura na VPS, Comandos úteis (na VPS, em `/opt/starkagro`), Deploy — starkcompany.com.br (VPS 2.25.140.180), Gotchas, Redeploy (um comando, da sua máquina com o repo), Segredos — `/opt/starkagro/.env` (não versionado, `chmod 600`)

## Knowledge Gaps
- **43 isolated node(s):** `How to work here`, `Components`, `Pitfalls (break prod or waste time)`, `API`, `UI (from `StarkAgroUI/`)` (+38 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **1 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `agpDBContext` connect `agpDBContext` to `MonitoredAreaResponse`, `MonitoredArea`, `StarkAgroAPI.Models`, `MonitoredAreaHandlersTests`?**
  _High betweenness centrality (0.168) - this node is a cross-community bridge._
- **Why does `MonitoredAreaResponse` connect `MonitoredAreaResponse` to `.TryBuild`, `StarkAgroAPI.Models`, `NdviController`?**
  _High betweenness centrality (0.137) - this node is a cross-community bridge._
- **Why does `GeoCoordinate` connect `.TryBuild` to `MonitoredAreaResponse`, `StarkAgroAPI.Models`, `MonitoredAreaHandlersTests`?**
  _High betweenness centrality (0.070) - this node is a cross-community bridge._
- **What connects `How to work here`, `Components`, `Pitfalls (break prod or waste time)` to the rest of the system?**
  _43 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `StarkAgroAPI.Models` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `CLAUDE.md` be split into smaller, more focused modules?**
  _Cohesion score 0.08333333333333333 - nodes in this community are weakly interconnected._
- **Should `agpDBContext` be split into smaller, more focused modules?**
  _Cohesion score 0.09956709956709957 - nodes in this community are weakly interconnected._