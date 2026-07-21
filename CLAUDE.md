# CLAUDE.md

Guide for Claude Code and coding agents in this repository.

| Doc | Purpose |
|-----|---------|
| [README.md](README.md) | Product overview, onboarding (human) |
| [.cursor/rules/starkagro-standards.mdc](.cursor/rules/starkagro-standards.mdc) | Mandatory patterns (Cursor, always applied) |
| [docs/agent-behavior.md](docs/agent-behavior.md) | Generic agent behavior (think, simplify, surgical edits) |

## How to work here

- **Scope:** minimum change that satisfies the request; match existing patterns.
- **Uncertainty:** state assumptions or ask — don't guess tenant rules, API contracts, or irrigation logic.
- **Verify:** `dotnet test` for API changes; manual or documented check for UI.
- **Features:** large backend work → plan in `docs/features/{name}/plan.md` first (see `.claude/skills/starkagro-feature-planner/`).
- **Code queries and changes:** use graphify by default — query the graph before exploring code, update the graph after changing code (see [## graphify](#graphify)).
- **Memory:** every durable fact saved locally must also be saved to Mnemosine (see [## Mnemosine](#mnemosine-long-term-memory)).
- **Behavior details:** [docs/agent-behavior.md](docs/agent-behavior.md).

## Components

| Path | Role |
|------|------|
| `StarkAgroAPI/` | ASP.NET Core 10, MediatR/CQRS, MongoDB, JWT + Google OAuth |
| `StarkAgroUI/` | Angular 19, Material, Chart.js, Leaflet |
| `docker/`, `.github/workflows/` | Compose, CI, deploy VPS |
| `terraform/aws/` | ECS Fargate, ALB, optional cloud path |

## Pitfalls (break prod or waste time)

| Situation | Rule |
|-----------|------|
| `dotnet build` API on Windows | Kill running `StarkAgroAPI` first (MSB3027/MSB3021 file lock) |
| UI dev server | Only `npm run start` inside `StarkAgroUI/` — proxy is in `angular.json` |
| API URLs from UI | Relative `/api/v1/...` — never hardcode host |
| Pivot without lat/long | Skip weather forecast; show CTA in dashboard |
| New domain handler | Inject `ICurrentUserContext`; filter `_currentUser.UserId`; never trust `request.UserId` |
| Any query or write by email | Never compare `Email` with `==`. Read with `EmailNormalizer.ByEmail(...)`, write with `EmailNormalizer.Normalize(...)`. Mongo is case-sensitive, email is not — `==` breaks login for anyone stored with a capital letter |
| MongoDB | No EF Core / SQL / `dotnet ef` — collections via `agpDBContext` |
| Upload de imagem | Nginx precisa de `client_max_body_size` em `StarkAgroUI/nginx.conf` **e** `docker/nginx/nginx.conf` — o default de 1 MB rejeita foto de celular com 413 |
| Testes com Mongo mock | Não use `.AnyAsync()` em handler: ele projeta para `BsonDocument` e o `MongoMockHelper` não cobre — use `.FirstOrDefaultAsync()` |
| Processamento em background | Lógica rodada pelo worker é **serviço puro**, não handler MediatR — `WorkerUserContext.UserId` é `null` e o assembly scan exporia o handler |
| Idempotência de leitura LoRaWAN | A `IdempotencyKey` (`CreateLoRaWanReadHandler`) inclui o **timestamp** do uplink: `{DevEUI}:{fcnt}:{time.Ticks}`. Nunca use só `{DevEUI}:{fcnt}` — o `fcnt` **zera no rejoin/reboot** do device e colidiria com leituras antigas, **perdendo dado real**. Duplicata (reentrega QoS 1 do broker) é tratada como **no-op** (read-before-write + `catch` do `DuplicateKey` como backstop de corrida), nunca como erro |
| Chaves de IA | Ficam no Mongo (`platform_ai_settings`, tela `/admin/ia`), **não** em `appsettings`. `CropHealthEnabled` é o kill-switch do custo por foto |
| Custo de IA por laudo | Guardado em **centavos inteiros** (`PlatformAiSettings.CropHealthCostCents`, config; `PlantDiagnosis.AiCostCents`, congelado no processamento) — dinheiro em `double` acumula erro. O processador grava o custo em **todos** os desfechos pagos (completo/recusado/só-classificador); `DiagnosisCostService` soma o mês e a tela `/admin/ia` mostra o gasto |
| Cobrança / planos | Preços **nunca** cravados em código: `DiagnosisPlan` (coleção `diagnosis_plans`, tela `/admin/planos`) tem mensalidade + laudos inclusos + preço do excedente, tudo em **centavos inteiros**. `User.DiagnosisPlanId` associa o produtor. `DiagnosisBillingService` calcula a fatura (mensalidade + excedente) — só **mostra**, não cobra (gateway de pagamento é etapa futura). Painel do agrônomo em `/agronomo/faturamento` (`GET /v1/agronomist/billing`) usa esse serviço por cliente **Active** — nunca lê pivôs/sensores, só faturamento derivado dos laudos. **Cota (bloqueio) e plano (cobrança) são coisas separadas**: o excedente é cobrado, não bloqueado. **Revenda paga tudo (modelo pool)**: `RevendaBillingService` soma o consumo de todos os clientes `Active` da revenda e aplica o **único** plano da revenda (`Revenda.DiagnosisPlanId`) à cota agregada — `excedente = max(0, total − inclusos)`. Ao aceitar um vínculo `Client`, o produtor tem `DiagnosisPlanId` **zerado** (sai da cobrança individual — anti-dupla-cobrança). Endpoints: gestor `GET /v1/revenda/billing`, admin `GET /v1/admin/revendas/{id}/billing` |
| Laudo gerado por LLM | O disclaimer legal é garantido em código (`EnsureDisclaimer`), nunca só pelo prompt — truncamento ou modelo teimoso o removeria |
| Geometria de área NDVI | A geometria da `MonitoredArea` (coleção `monitored_areas`) é um `GeoJsonPolygon` — ordem **`[lng, lat]`** (`GeoJson2DGeographicCoordinates(lng, lat)`), fonte clássica de bug. O REST usa `GeoCoordinate {lat, lng}` **nomeado**; a conversão vive **só** em `Services/Ndvi/MonitoredAreaGeometry` (fábrica pura, testada). O front sempre manda um **anel** de pontos (o círculo é aproximado a polígono antes de enviar). Índice `2dsphere` em `Geometry`. Polígono é validado (fechamento, ranges, ≤500 vértices, bbox ≤0.5°, auto-interseção básica) — bloquear polígono gigante guarda o custo de PU nas fases de fetch |
| Busca de NDVI (CDSE) | Credenciais CDSE (Copernicus) ficam no `platform_ai_settings` (`CdseClientId/Secret`), **não** em `appsettings`; `Sentinel2Enabled` é o kill-switch (o worker `NdviProcessor` não busca nada desligado). Token OAuth2 em cache (`CdseTokenProvider`/`IMemoryCache`). `NdviReading` (coleção `ndvi_readings`) tem **índice único `{AreaId, AcquisitionDate}`**: refetch da mesma passagem é **no-op** (dedup por `LastAcquisitionDate` + `catch DuplicateKey`). **Nuvem é terminal** para a passagem (grava `CloudRejected`, sem retry-storm) — só falha real (HTTP/parse) entra no retry/backoff. O `NdviFetchService` é serviço puro (não handler), tenant vem do documento da área |
| Governança de custo NDVI | O gasto do mês (soma de `NdviReading.NdviCostCents`) é calculado por `NdviCostService` (espelho puro de `DiagnosisCostService`) e exposto em `/admin/ia` (`AdminAiSettingsResponse.CurrentMonthNdviCostCents`). Dois tetos em `platform_ai_settings`, **centavos inteiros, 0 = ilimitado**: `NdviMonthlyBudgetCents` (batido, o `NdviProcessor` **para de enfileirar** e faz `LogWarning` do represamento — nunca trunca em silêncio; freio de custo, não erro) e `NdviMaxAreasPerUser` (enforced em `CreateMonitoredAreaHandler`, guarda o custo de PU na origem). Custo é **proxy** por `NdviCostCents`, não o PU real da CDSE — calibrar pelo dashboard. Kill-switch `Sentinel2Enabled` continua cortando tudo antes do teto |
| Overlay NDVI (PNG) | O PNG colorizado (Process API, `CdseProcessService`) vive num bucket GridFS **separado** `ndvi_overlays` (`agpDBContext.NdviOverlays` / `INdviOverlayStore`) — **nunca** no de laudos `diagnosis_images`. Gerado **só** para a passagem mais nova **não-nublada** e é **acessório**: falha da Process API é engolida (`OverlayImageFileId` fica null, tendência não quebra). Servido privado por `GET /v1/areas/{id}/overlay/{readingId}` via `GetNdviOverlayImageHandler` com **dupla checagem de posse** (área do dono → reading da área) antes de tocar o GridFS. O front alinha o `L.imageOverlay` pelo **bbox** `[minLng,minLat,maxLng,maxLat]` que vem em cada ponto do `NdviTrendResponse` |
| Kindwise crop.health | `datetime` exige offset (`+00:00`; `Z` e `ToString("o")` dão 400); **não** envie `similar_images: false`; a resposta de sucesso é **201** |
| PDF (QuestPDF) | Licença **Community** declarada em `ApiConfig` — gratuita só até US$ 1 mi de receita anual. Não dá para asserir texto nos bytes do PDF (fonte subsetada): teste o conteúdo via `DiagnosisPdfService.FooterLines`/`StatusLabel` |
| Firmware / dispositivos em campo | O código do firmware **não vive mais neste repo**, mas os dispositivos continuam chamando `Auth/LogIn` e `reads/add`. Mudar payload ou rota dessas chamadas **quebra sensor em campo** — trate como contrato público |
| Deploy | `main` + green CI; secrets only via env / user secrets — placeholders in repo. **Auto-deploy é opt-in**: o job pós-CI (`deploy.yml`) só roda se a var de repo `AGRIPEWEB_AUTO_DEPLOY == 'true'` **e** os secrets da VPS (`VPS_HOST`/`VPS_USER`/`VPS_SSH_KEY`/`VPS_DEPLOY_PATH`) estiverem setados — senão o merge não dispara deploy (evita falha "missing server host"). Deploy manual: `gh workflow run Deploy` ou `scripts/redeploy-starkcompany.sh` |
| Google login button | Show only when `environment.googleClientId` is set |

## Build and run

### API
```bash
dotnet run --project StarkAgroAPI/StarkAgroAPI.csproj
dotnet build StarkAgroAPI/StarkAgroAPI.csproj   # stop API process first on Windows
```

### UI (from `StarkAgroUI/`)
```bash
npm install
npm run start    # http://localhost:4200 — proxy to API
```

### Tests
```bash
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
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
| Entity / collection | `Models/Entities/`, register in `agpDBContext` — [starkagro-mongo-setup skill](.claude/skills/starkagro-mongo-setup/SKILL.md) |
| Angular route / screen | `StarkAgroUI/src/app/app.routes.ts` + component; auth under `LayoutComponent` |
| Weather / irrigation logic | `Services/Forecast/`, irrigation trend handlers |
| Laudo fitossanitário (foto + IA) | `Controllers/PlantDiagnosisController.cs`, `Domain/Handlers/Diagnosis/`, `Services/Diagnosis/`, `StarkAgroWorker/Services/PlantDiagnosisProcessor.cs` — plano em [docs/features/laudo-fitossanitario-ia/plan.md](docs/features/laudo-fitossanitario-ia/plan.md) |
| OAuth | API `Auth/external-login`; UI `/login/callback` |
| Multi-agent (Paperclip) | [docs/agents/README.md](docs/agents/README.md) |

## Feature workflow

1. GitHub issue → acceptance criteria clear  
2. Plan → `docs/features/{name}/plan.md` (optional skill: `starkagro-feature-planner`)  
3. Implement → handler + tests; UI in parallel only if contract is defined  
4. Review → tenant isolation, no secrets in diff (`starkagro-code-reviewer` skill)

## Architecture

### API — CQRS (MediatR)

Controllers delegate to handlers; **no business logic in controllers.**

- `Controllers/` — thin; `MainController` base helpers  
- `Domain/Commands/` — request/response DTOs  
- `Domain/Handlers/` — business logic  
- `Models/Entities/` — `User`, `Pivot`, `Sensor`, `ReadSensor`, `PlantDiagnosis` extend `Entity` (`[BsonId] int Id`)  
- `Pivot` — nullable `Latitude`, `Longitude`, `Altitude`, `LocationAddress`, `LocationUpdatedAt` (map selector)  
- `PlantDiagnosis` — laudo fitossanitário; status `Uploaded → Processing → AiCompleted|PendingReview|Rejected|Failed`; foto no GridFS (`ImageFileId`); `AuditTrail` append-only  
- `Services/` — JWT, passwords, `CurrentUserContext`; `Services/Forecast/` — `WeatherForecastOrchestrator`, Open-Meteo, Google Weather AI; `Services/Diagnosis/` — `IDiagnosisImageStore` (GridFS), `ImageContentValidator`, `PlantDiagnosisProcessingService` (classificador → contexto → LLM); `Services/CropHealth/` — `KindwiseCropHealthService` (crop.health)  
- `IAIInsightsService.CompleteAsync(systemPrompt, userMessage, apiKey, model, ct, maxTokens?)` — canal genérico de texto; **passe `maxTokens` para textos longos**: o padrão (1024) corta um laudo pelo meio  
- `Configuration/` — DI, JWT/OAuth, Swagger, CORS, `MongoDbSettings`, `WeatherForecastSettings`  
- `Validators/`, `Notifications/` (`INotifier` / `Notificator`)

### MongoDB

- `agpDBContext` → `IMongoCollection<T>`  
- Collections: `users`, `pivots`, `sensors`, `read_sensors`, `plant_diagnoses`, `agronomist_clients`, `diagnosis_plans`, `revendas`, `revenda_memberships`, `monitored_areas`, `ndvi_readings`, `counters`  
- `agronomist_clients` tem **índice único parcial** em `{ClientUserId}` filtrando `Status: "Active"` — o banco garante *um agrônomo ativo por produtor*  
- `revenda_memberships` (vínculo revenda↔membro, papéis `Manager`/`Agronomist`/`Client`) espelha `agronomist_clients`, com **índice único parcial** em `{MemberUserId}` filtrando `Status: "Active" AND MemberRole: "Client"` — *um produtor ativo por revenda*. Revenda/membership são geridos pelo **admin** (não são dados por-tenant): CRUD em `AdminController` (`v1/admin/revendas`), sem filtro por `_currentUser.UserId`, igual a `diagnosis_plans`. O **gestor** (papel `ResellerManager`, `RevendaController` em `v1/revenda`) resolve QUAL revenda ele gere via `IRevendaMembershipService.GetManagedRevendaIdAsync(caller)` — **nunca** pelo request; convida/lista/revoga sempre por essa revenda. Aceite do convite fica no lado do membro (`v1/user/revenda-invites`) e denormaliza `User.RevendaId` (cache; fonte da verdade é a membership)  
- Binários: buckets GridFS `diagnosis_images` (`agpDBContext.DiagnosisImages`) — fotos dos laudos; `ndvi_overlays` (`agpDBContext.NdviOverlays`) — PNGs de overlay NDVI; **nunca** base64 no documento  
- Sequential `int` IDs: `counters` + `GetNextIdAsync`  
- Config: `appsettings.*.json` → section `MongoDb`  
- Seed default user on startup if `users` is empty (`ApiConfig`)

### Multi-tenant isolation

- JWT claim `"id"` → `ICurrentUserContext.UserId` (`CurrentUserContext` + `IHttpContextAccessor`)  
- Every domain handler: inject `ICurrentUserContext`, filter/persist with `_currentUser.UserId`  
- **Never** use client-supplied `request.UserId` for isolation  
- Examples: `CreatePivotHandler`, `CreateReadHandler`, `GetListSensorHandler`

### Papéis (Roles)

- `User.Roles` (`List<string>`, constantes em `UserRole`: `Admin`, `Agronomist`, `ResellerManager`) é a **fonte da verdade** — substituiu os antigos booleans `IsAdmin`/`IsAgronomist`. `User.IsAdmin`/`IsAgronomist`/`IsResellerManager` são **computados `[BsonIgnore]`** sobre `Roles`; use `SetRole(role, bool)` para escrever.
- JWT (`JwtTokenService`) emite as claims booleanas derivadas (`isAdmin`/`isAgronomist`/`isResellerManager`, para a UI/guards) **e** uma claim `role` por papel. `ICurrentUserContext` expõe `IsAdmin`/`IsAgronomist`/`IsResellerManager` + `HasRole(role)`. Policies: `"Agronomist"`, `"ResellerManager"`.
- Documentos gravados no formato antigo são convertidos no boot por `MigrateUserRolesAsync` (`ApiConfig`, idempotente; lógica pura em `UserRoleMigration.DeriveRoles`). Papéis são atribuídos pelo admin em `AdminEditUserHandler` (`SetRole`).

**Única exceção de leitura cross-tenant — o agrônomo** (papel `Agronomist`, claim `isAgronomist`, policy `"Agronomist"`):
- Ele lê laudos cujo `UserId` não é o dele. A regra vive **em um lugar só**, `IDiagnosisAccessService`: lê o laudo `d` **sse** `d.UserId == u` **OU** (`d.AgronomistId == u` **E** existe vínculo `Active` entre eles). A segunda condição é o que faz a **revogação ter efeito imediato**.
- Ele **não** lê `pivots`/`sensors`/`read_sensors` do cliente — todo o contexto vem do `ContextSnapshot` congelado dentro do laudo. Não crie endpoints que furem isso.
- **Admin não tem acesso a laudo** (é ato profissional).

### Authentication

- JWT Bearer (8 h) + Google OAuth → `Auth/external-login` exchanges code for API JWT  
- UI stores token in `localStorage` (`AuthGuard` with `typeof window` guard for SSR)

### UI — Angular 19

- **Routes:** single source `app.routes.ts` + `provideRouter` in `app.config.ts` — **not** `RouterModule.forRoot` in `AppModule`  
- Login outside layout; authenticated routes are children of `LayoutComponent`  
- **Routes:** `/login`, `/login/callback`, `/home`, `/irrigation-dashboard`, `/config`, `/pivots`, `/pivots/novo`, `/pivots/editar/:id`, `/sensores`, `/sensores/novo`, `/sensores/editar/:id`, `/diagnosticos`, `/diagnosticos/novo`, `/diagnosticos/:id`, `/areas`, `/areas/novo`, `/areas/editar/:id`, `/areas/:id` (detalhe: tendência NDVI + overlay), `/agronomo/fila`, `/agronomo/laudo/:id`, `/agronomo/clientes`, `/agronomo/faturamento` (`AgronomistGuard`), `/revenda/convites` (`AuthGuard` — aceite do membro), `/revenda/membros`, `/revenda/faturamento` (`ResellerGuard`), `/admin/revendas` (`AdminGuard`), `/dashboard/:pivoId/:quadrante`, `/dashboard/:pivoId/:quadrante/config`, `/user`  
- Papel do gestor de revenda na UI: `login`/`auth-callback` gravam `localStorage['isResellerManager']` do token; `ResellerGuard` e o menu (`layout`) leem essa flag (espelha `isAgronomist`). Serviço `RevendaService` (gestor + membro); revenda admin no `AdminService`  
- Convite de revenda pendente aparece no **sino de notificações** (alerta `RevendaInvite`, sintetizado em `GetUserAlertsHandler` a partir de `revenda_memberships` Pending — igual ao `AgronomistInvite`); clicar leva a `/revenda/convites`. O convite nunca fica "lido" — some ao aceitar/recusar  
- `ApiService` → `/api/v1/*`  
- Imagem protegida (laudo/overlay NDVI): buscar como **blob** via `HttpClient` (`responseType: 'blob'` → `createObjectURL`) — `<img src>`/`L.imageOverlay` não enviam `Authorization`
- Áreas NDVI (`AreaService`, `area-list`/`area-form`/`area-detail`): detalhe usa **ng2-charts** (`BaseChartDirective`) para a tendência + **Leaflet** (`import('leaflet')` dinâmico, guard `isPlatformBrowser`) com `L.imageOverlay` do PNG. **Bbox vem `[minLng,minLat,maxLng,maxLat]`; Leaflet quer `[[minLat,minLng],[maxLat,maxLng]]`** — trocar a ordem. Círculo é aproximado a anel client-side (`circleToRing`, correção de longitude por `cos(lat)`) antes de enviar. **Polígono**: desenho livre com **leaflet-geoman** (`@geoman-io/leaflet-geoman-free`, `map.pm.addControls` só drawPolygon/edit/remove, import dinâmico + guard SSR), captura o anel via `getLatLngs()[0]`; edição carrega o anel existente editável. `leaflet` e `@geoman-io/leaflet-geoman-free` em `allowedCommonJsDependencies` + CSS do geoman no `angular.json`  
- `PivotLocationMapComponent` — dynamic `import('leaflet')`; Nominatim + Open-Meteo elevation  
- Mapas Leaflet (área NDVI, desenho de polígono, seletor de local): camadas base vêm de `utils/leaflet-basemaps.ts` (`applyDefaultMarkerIcon` + `addBaseLayers`) — abre em **satélite** (Esri World Imagery, sem chave; `maxNativeZoom: 18` porque a Esri nem sempre tem tile útil em z19 no interior) com seletor Satélite/Ruas em `topright` (o geoman ocupa `topleft`). O helper recebe o `L` por parâmetro: importar Leaflet ali puxaria a lib para o bundle inicial e quebraria o SSR  
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
- IoT login/read endpoints are a public contract with devices already in the field — the firmware source is no longer in this repo, so a breaking change here cannot be caught by CI

## Documentation and skills

- [docs/contratacao-time.md](docs/contratacao-time.md) — team roles  
- [docs/agents/README.md](docs/agents/README.md) — Paperclip SOUL/HEARTBEAT  
- `.claude/skills/starkagro-backend-expert` — handlers/endpoints  
- `.claude/skills/starkagro-implement` — full issue workflow  
- `.claude/skills/starkagro-test-writer` — xUnit + Mongo mocks  
- `.claude/skills/starkagro-code-reviewer` — pre-merge review

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- **Default for code queries:** for any codebase question, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- **Default after code changes:** every code change must update BOTH this CLAUDE.md (when the change affects documented architecture, routes, entities, or conventions) AND the graph — run `graphify update .` (AST-only, no API cost) or `/graphify . --update` (also re-extracts changed docs/images).
- A git post-commit hook is installed (`graphify hook status`) that auto-updates the graph for committed code files; doc/image changes still need a manual `/graphify . --update`.
- If `graphify` is not on PATH, use the saved interpreter: `& (Get-Content graphify-out\.graphify_python) -m graphify <args>` (PowerShell).

## Mnemosine (long-term memory)

Remote memory MCP server `mnemosine-remote` (https://mnemosine.cloud/mcp), automatically scoped to this project. Auth token lives in the local MCP config — never in this repo.

Rules:
- **Recall first:** before stating that something wasn't agreed, decided, or configured, run `recall` on the topic.
- **Mirror local memory:** every durable piece of information saved locally — Claude auto-memory files, graphify work-memory Q&As (`graphify save-result`), decisions and conventions — must ALSO be persisted to Mnemosine via `remember`, in the same turn, without asking.
- Memories are project-scoped by default; use `all_projects=true` only when global context is genuinely needed.
- If the Mnemosine API is unavailable, tell the user and continue — never fabricate memories.
