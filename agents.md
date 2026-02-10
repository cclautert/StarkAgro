# AgripeWeb – General guidelines for AI agents and developers

This document gives high-level guidance for working on the AgripeWeb codebase. For project-specific rules, see `.cursor/rules/agripeweb-standards.mdc`.

---

## Project structure

- **AgripeWebAPI** – .NET 10 Web API; MediatR handlers; JWT + OAuth (Google); MongoDB as the only database.
- **AgripeWebUI** – Angular SPA (standalone + NgModule hybrid); Material UI; proxy to API in dev.
- **AgripeWebIOT** / **Hardware** – IoT/firmware (e.g. Arduino) when present.

---

## Running and building

- **API**: `dotnet run --project AgripeWebAPI/AgripeWebAPI.csproj`. Stop any running API process before `dotnet build` to avoid file-lock errors (MSB3027/MSB3021).
- **UI**: From `AgripeWebUI` only, run `npm run start`. Do not pass `ng serve` with proxy path; proxy is in `angular.json`. App at `http://localhost:4200`.
- **API base URL in dev**: UI calls the API via relative URLs (e.g. `/api/v1/...`) so the dev proxy and CORS stay correct.

---

## Data and configuration

- **Database**: MongoDB only. Connection and database name are in the `MongoDb` section of `appsettings.*.json`. No EF Core / SQL Server.
- **Secrets**: Do not commit real connection strings, JWT secrets, or OAuth client secrets. Use placeholders in versioned config; keep real values in user secrets, env vars, or secure config.
- **Auth**: API issues JWT after login (email/password or OAuth). UI stores the token (e.g. in `localStorage`) and sends it on authorized requests. Any code that reads `localStorage` must guard for SSR (e.g. check `typeof window !== 'undefined' && typeof window.localStorage !== 'undefined'`).

---

## Frontend (Angular)

- **Routing**: Single source of truth is `app.routes.ts` with `provideRouter(routes)` in `app.config.ts`. Do not register routes again with `RouterModule.forRoot` in `AppModule`.
- **Layout**: Authenticated routes are children of a route that uses `LayoutComponent` (sidebar with Home, Sensors, Pivots, User, LogOut). Login and callback routes are outside this layout.
- **LogOut**: Implemented in the left menu below User: clears the stored token and navigates to `/login`. Use the same SSR-safe check before touching `localStorage`.
- **Components**: `LayoutComponent` is standalone. Prefer standalone where it fits; use `AppModule` for shared declarations and providers as needed.

---

## Backend (API)

- **Handlers**: Use MediatR; inject `agpDBContext` (MongoDB collections + `GetNextIdAsync` for integer IDs). No EF `DbContext` or SQL.
- **Entities**: Id is `int`; MongoDB mapping uses `[BsonId]`. Collections: `users`, `pivots`, `sensors`, `read_sensors`; sequence in `counters`.
- **OAuth**: External login goes through an API endpoint (e.g. `Auth/external-login`) that exchanges the provider code for the API JWT. UI only starts the provider flow and handles the callback with that code.

---

## Code style and safety

- Prefer stable .NET and package versions; avoid previews unless required.
- When changing routing, auth, or layout in the UI, ensure the sidebar and LogOut still work and that guarded routes redirect unauthenticated users to login.
- When adding or changing API handlers, keep using the existing MongoDB context and ID strategy; do not reintroduce EF or SQL.
- Run the API and UI after meaningful changes to confirm they start and that login/logout and main flows work.

---

## References

- **Cursor rules**: `.cursor/rules/agripeweb-standards.mdc` (always-applied project standards).
- **OAuth setup**: `AgripeWebAPI/OAUTH_SETUP.md` for Google OAuth configuration.
