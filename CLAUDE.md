# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AgripeWeb is an agricultural IoT monitoring platform with three main components:
- **AgripeWebAPI** — ASP.NET Core 10.0 REST API (C#, MongoDB, MediatR/CQRS)
- **AgripeWebUI** — Angular 19 SPA (TypeScript, Angular Material, Chart.js)
- **AgripeWebIOT** — ESP8266 Arduino firmware for field sensors

## Build & Run Commands

### API
```bash
dotnet run --project AgripeWebAPI/AgripeWebAPI.csproj
# Before building, kill any running AgripeWebAPI process — the exe gets locked (MSB3027/MSB3021)
dotnet build AgripeWebAPI/AgripeWebAPI.csproj
```

### UI (run from AgripeWebUI/)
```bash
cd AgripeWebUI
npm install
npm run start          # serves at http://localhost:4200, proxy to API configured
```

### Tests
```bash
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj
# Single test:
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

### Docker (full stack)
```bash
docker compose -f docker/docker-compose.yml up --build
# API: localhost:8080, UI: localhost:80, MongoDB: localhost:27027
```

### Terraform (AWS)
```bash
cd terraform/aws
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

## Architecture

### API — CQRS with MediatR
Controllers receive requests and delegate to MediatR handlers. No business logic in controllers.

- **Controllers/** — Thin controllers (`MainController` is the base class with helper methods)
- **Domain/Commands/Requests/** — Command/query request objects (e.g., `CreatePivotRequest`)
- **Domain/Commands/Responses/** — Response DTOs
- **Domain/Handlers/** — MediatR handlers with business logic
- **Models/Entities/** — MongoDB entities (`User`, `Pivot`, `Sensor`, `ReadSensor`), all inherit from `Entity` (has `[BsonId] int Id`)
- **Models/Interfaces/** — `ICurrentUserContext`, `IJwtTokenService`, `INotifier`, `IPasswordHasher`
- **Services/** — `JwtTokenService`, `PasswordHasherService`, `CurrentUserContext`
- **Configuration/** — DI registration, JWT/OAuth/Swagger/CORS setup, `MongoDbSettings`
- **Validators/** — Custom validation attributes (`EmailAttribute`, `PasswordStrengthAttribute`)
- **Notifications/** — Error notification pattern via `INotifier`/`Notificator`

### Database — MongoDB
- Context class: `agpDBContext` exposes `IMongoCollection<T>` for each entity
- Collections: `users`, `pivots`, `sensors`, `read_sensors`, `counters`
- IDs are sequential integers via `counters` collection (`GetNextIdAsync`)
- Config section in appsettings: `MongoDb` with `ConnectionString` and `DatabaseName`

### Multi-Tenant Isolation
- Implemented via `UserId` from JWT claim `"id"`, resolved by `ICurrentUserContext`
- All domain handlers must inject `ICurrentUserContext` and filter by `_currentUser.UserId`
- Never trust `request.UserId` from the client for tenant isolation

### Authentication
- JWT Bearer tokens (8-hour expiration) + Google OAuth 2.0
- OAuth flow goes through backend endpoint `Auth/external-login` which exchanges the code for a JWT
- Frontend stores JWT in localStorage

### UI — Angular 19 Standalone Components
- **Routing**: `app.routes.ts` is the single source of routes (not `AppRoutingModule`). Login routes at top level; authenticated routes are children of `LayoutComponent` (provides sidebar nav)
- **Services**: `ApiService` calls `/api/v1/*` relative URLs (proxy handles dev routing)
- **Guards**: `AuthGuard` checks localStorage (with `typeof window` guard for SSR safety)
- **Key routes**: `/login`, `/login/callback`, `/home`, `/pivots`, `/sensores`, `/dashboard/:pivoId/:quadrante`, `/user`

### Security
- CORS: dev allows `localhost:4200`, prod allows `agripeweb.com`
- Rate limiting: 100 requests per 10 seconds
- Passwords: BCrypt with salt
- CSRF: AntiForgery tokens configured

### Infrastructure
- **Docker**: Multi-stage builds — API (dotnet SDK → runtime), UI (Node → Nginx Alpine)
- **AWS via Terraform**: ECS Fargate, ALB (path-based routing: `/api/*` → API, default → UI), ECR, CloudWatch
- **ECS tasks**: 256 CPU, 512 MB memory

## Key Conventions

- UI calls API via relative URLs (`/api/v1/...`) — never hardcode the API host
- Use placeholder values for secrets in committed files (`CHANGE_ME`, `YOUR_GOOGLE_CLIENT_ID`)
- Prefer stable .NET/NuGet versions (no previews) compatible with the installed SDK
- Test framework: xUnit + Moq + InMemory provider, using Arrange-Act-Assert pattern
- `LayoutComponent` is standalone; set `showLayout` from URL in `ngOnInit` using `NavigationEnd`
- `AppModule` imports `RouterModule` (without `.forRoot()`) — routing is configured via `provideRouter(routes)` in `app.config.ts`
