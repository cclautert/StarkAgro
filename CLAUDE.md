# CLAUDE.md

Guide for Claude Code and coding agents in this repository.

| Doc | Purpose |
|-----|---------|
| [README.md](README.md) | Product overview, onboarding (human) |
| [.cursor/rules/agripeweb-standards.mdc](.cursor/rules/agripeweb-standards.mdc) | Mandatory patterns (Cursor, always applied) |
| [docs/agent-behavior.md](docs/agent-behavior.md) | Generic agent behavior (think, simplify, surgical edits) |

## How to work here

- **Scope:** minimum change that satisfies the request; match existing patterns.
- **Uncertainty:** state assumptions or ask — don't guess tenant rules, API contracts, or irrigation logic.
- **Verify:** `dotnet test` for API changes; manual or documented check for UI/IoT.
- **Features:** large backend work → plan in `docs/features/{name}/plan.md` first (see `.claude/skills/agripeweb-feature-planner/`).
- **Behavior details:** [docs/agent-behavior.md](docs/agent-behavior.md).

## Components

| Path | Role |
|------|------|
| `AgripeWebAPI/` | ASP.NET Core 10, MediatR/CQRS, MongoDB, JWT + Google OAuth |
| `AgripeWebUI/` | Angular 19, Material, Chart.js, Leaflet |
| `AgripeWebUI-Mobile/` | React Native (field use) |
| `AgripeWebIOT/` | ESP8266 (Wi-Fi), ESP32 LoRa gateway/slave |
| `docker/`, `.github/workflows/` | Compose, CI, deploy VPS |
| `terraform/aws/` | ECS Fargate, ALB, optional cloud path |

## Pitfalls (break prod or waste time)

| Situation | Rule |
|-----------|------|
| `dotnet build` API on Windows | Kill running `AgripeWebAPI` first (MSB3027/MSB3021 file lock) |
| UI dev server | Only `npm run start` inside `AgripeWebUI/` — proxy is in `angular.json` |
| API URLs from UI | Relative `/api/v1/...` — never hardcode host |
| Pivot without lat/long | Skip weather forecast; show CTA in dashboard |
| New domain handler | Inject `ICurrentUserContext`; filter `_currentUser.UserId`; never trust `request.UserId` |
| MongoDB | No EF Core / SQL / `dotnet ef` — collections via `agpDBContext` |
| Firmware | No real Wi-Fi passwords, tokens, or MACs in committed `.ino` files |
| Deploy | `main` + green CI; secrets only via env / user secrets — placeholders in repo |
| Google login button | Show only when `environment.googleClientId` is set |

## Build and run

### API
```bash
dotnet run --project AgripeWebAPI/AgripeWebAPI.csproj
dotnet build AgripeWebAPI/AgripeWebAPI.csproj   # stop API process first on Windows
```

### UI (from `AgripeWebUI/`)
```bash
npm install
npm run start    # http://localhost:4200 — proxy to API
```

### Tests
```bash
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

Handler tests: **xUnit + Moq**, MongoDB cursor mocking (not EF InMemory). Pattern: Arrange–Act–Assert.

### Docker / CI / cloud
```bash
docker compose -f docker/docker-compose.yml up --build
# API :8080, UI :80, MongoDB :27027
```
- **CI:** `.github/workflows/ci.yml` — build, test, Angular prod build
- **Deploy:** `.github/workflows/deploy.yml` → VPS after `main` — [docs/deploy-hostinger.md](docs/deploy-hostinger.md)
- **Terraform:** `cd terraform/aws && terraform init && terraform plan -out=tfplan`

## Where to implement

| Task | Location |
|------|----------|
| REST endpoint | `Controllers/` → `Domain/Commands/Requests|Responses/` → `Domain/Handlers/` |
| Entity / collection | `Models/Entities/`, register in `agpDBContext` — [agripeweb-mongo-setup skill](.claude/skills/agripeweb-mongo-setup/SKILL.md) |
| Angular route / screen | `AgripeWebUI/src/app/app.routes.ts` + component; auth under `LayoutComponent` |
| Weather / irrigation logic | `Services/Forecast/`, irrigation trend handlers |
| OAuth | API `Auth/external-login`; UI `/login/callback` |
| Firmware / field device | `AgripeWebIOT/` — coordinate API contract with backend |
| Multi-agent (Paperclip) | [docs/agents/README.md](docs/agents/README.md) |

## Feature workflow

1. GitHub issue → acceptance criteria clear  
2. Plan → `docs/features/{name}/plan.md` (optional skill: `agripeweb-feature-planner`)  
3. Implement → handler + tests; UI and IoT in parallel only if contract is defined  
4. Review → tenant isolation, no secrets in diff (`agripeweb-code-reviewer` skill)

## Architecture

### API — CQRS (MediatR)

Controllers delegate to handlers; **no business logic in controllers.**

- `Controllers/` — thin; `MainController` base helpers  
- `Domain/Commands/` — request/response DTOs  
- `Domain/Handlers/` — business logic  
- `Models/Entities/` — `User`, `Pivot`, `Sensor`, `ReadSensor` extend `Entity` (`[BsonId] int Id`)  
- `Pivot` — nullable `Latitude`, `Longitude`, `Altitude`, `LocationAddress`, `LocationUpdatedAt` (map selector)  
- `Services/` — JWT, passwords, `CurrentUserContext`; `Services/Forecast/` — `WeatherForecastOrchestrator`, Open-Meteo, Google Weather AI  
- `Configuration/` — DI, JWT/OAuth, Swagger, CORS, `MongoDbSettings`, `WeatherForecastSettings`  
- `Validators/`, `Notifications/` (`INotifier` / `Notificator`)

### MongoDB

- `agpDBContext` → `IMongoCollection<T>`  
- Collections: `users`, `pivots`, `sensors`, `read_sensors`, `counters`  
- Sequential `int` IDs: `counters` + `GetNextIdAsync`  
- Config: `appsettings.*.json` → section `MongoDb`  
- Seed default user on startup if `users` is empty (`ApiConfig`)

### Multi-tenant isolation

- JWT claim `"id"` → `ICurrentUserContext.UserId` (`CurrentUserContext` + `IHttpContextAccessor`)  
- Every domain handler: inject `ICurrentUserContext`, filter/persist with `_currentUser.UserId`  
- **Never** use client-supplied `request.UserId` for isolation  
- Examples: `CreatePivotHandler`, `CreateReadHandler`, `GetListSensorHandler`

### Authentication

- JWT Bearer (8 h) + Google OAuth → `Auth/external-login` exchanges code for API JWT  
- UI stores token in `localStorage` (`AuthGuard` with `typeof window` guard for SSR)

### UI — Angular 19

- **Routes:** single source `app.routes.ts` + `provideRouter` in `app.config.ts` — **not** `RouterModule.forRoot` in `AppModule`  
- Login outside layout; authenticated routes are children of `LayoutComponent`  
- **Routes:** `/login`, `/login/callback`, `/home`, `/irrigation-dashboard`, `/config`, `/pivots`, `/pivots/novo`, `/pivots/editar/:id`, `/sensores`, `/sensores/novo`, `/sensores/editar/:id`, `/dashboard/:pivoId/:quadrante`, `/dashboard/:pivoId/:quadrante/config`, `/user`  
- `ApiService` → `/api/v1/*`  
- `PivotLocationMapComponent` — dynamic `import('leaflet')`; Nominatim + Open-Meteo elevation  
- `LayoutComponent` standalone — `showLayout` from URL + `NavigationEnd` in `ngOnInit`  
- `AppModule` imports `RouterModule` without `.forRoot()` for `routerLink` only

### Security and infra

- CORS: `localhost:4200` (dev), `agripeweb.com` (prod)  
- Rate limit: 100 req / 10 s  
- Passwords: BCrypt; CSRF anti-forgery configured  
- Docker: multi-stage API + UI (Nginx)  
- AWS (Terraform): ECS Fargate, ALB `/api/*` → API, default → UI, ECR, CloudWatch (256 CPU / 512 MB tasks)

## Key conventions

- Secrets in repo: placeholders only (`CHANGE_ME`, `YOUR_GOOGLE_CLIENT_ID`)  
- Stable .NET / NuGet versions — no previews without explicit approval  
- UI OAuth button only if `environment.googleClientId` is configured  
- IoT login/read endpoints must stay aligned with API routes used in `.ino` files

## Documentation and skills

- [docs/contratacao-time.md](docs/contratacao-time.md) — team roles  
- [docs/agents/README.md](docs/agents/README.md) — Paperclip SOUL/HEARTBEAT  
- `.claude/skills/agripeweb-backend-expert` — handlers/endpoints  
- `.claude/skills/agripeweb-implement` — full issue workflow  
- `.claude/skills/agripeweb-test-writer` — xUnit + Mongo mocks  
- `.claude/skills/agripeweb-code-reviewer` — pre-merge review
