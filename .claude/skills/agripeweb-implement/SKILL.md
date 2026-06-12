---
name: agripeweb-implement
description: |
  Full feature implementation workflow for the AgripeWeb project using a GitHub Issue number.
  Runs the complete sequence: plan → confirm → implement → MongoDB setup → tests → code review → post to GitHub.
  Use this skill whenever the user says "implement issue", "implement feature", "implement task", or provides
  a GitHub Issue number and wants the full development workflow executed on the AgripeWeb project.
  Also trigger when the user says things like "build this feature", "start work on issue #NNN", or "do the
  implementation for NNN" in the context of AgripeWeb.
---

# AgripeWeb — Implement Feature from GitHub Issue

**Stack context:** ASP.NET Core 10 · CQRS/MediatR · MongoDB · xUnit + Moq · Angular 19

Ask the user for the GitHub Issue number if not already provided. Then follow this exact sequence, waiting for each step to complete before starting the next.

---

## Step 1: Plan

Read the issue using the `github` MCP tool:
```
get_issue(owner="cclautert", repo="AgripeWeb", issue_number=NNN)
list_issue_comments(owner="cclautert", repo="AgripeWeb", issue_number=NNN)
```
Extract the Description, Acceptance Criteria, Business Rules, and any comments.

Explore the codebase to understand which handlers, controllers, entities, and collections are affected. Produce a structured implementation plan covering:

- **Context** — what the feature does and why (1–3 sentences)
- **Acceptance Criteria** — each criterion mapped to a concrete implementation decision
- **Affected layers** — handlers, controllers, entities, agpDBContext, Angular UI, MQTT worker
- **New REST endpoints** — method, route, auth policy, request/response shape
- **Files to create** — full path + type for each new file
- **Files to modify** — full path + exact change description
- **MongoDB changes** — new collections, fields, indexes (or "None")
- **Tenant isolation plan** — how every new query filters by `_currentUser.UserId`
- **Risks & flags** — ambiguous criteria, missing indexes, cross-tenant risks
- **DI registration** — new services/handlers and their lifetimes
- **Verification** — build commands, test commands, sample HTTP requests

Save the plan to `docs/features/{kebab-case-name}/plan.md` with header:
```
Issue: https://github.com/cclautert/AgripeWeb/issues/NNN
```

Present the plan to the user and ask:
> "Does this plan look correct? Proceed with implementation? (yes / no / corrections)"

**Do NOT proceed to Step 2 until the user explicitly confirms.**

---

## Step 2: Implement

Read the plan at `docs/features/{name}/plan.md` and implement every file listed.

**AgripeWeb conventions:**
- All business logic lives in MediatR handlers under `Domain/Handlers/` — controllers are thin routers only
- Use `ICurrentUserContext` (injected, never trust client-supplied user IDs) for tenant isolation
- Use `agpDBContext` for MongoDB collection access; call `GetNextIdAsync()` for sequential IDs on new entities
- Report errors via `_notifier.Handle("message")` + return null — never throw HTTP exceptions from handlers
- Controllers call `_mediator.Send()` and `CustomResponse()` — nothing else
- Register new services in `ApiConfig.cs`; new endpoints carry `[Authorize]` unless explicitly public
- Pass and forward `CancellationToken` on all async I/O

Run `dotnet build AgripeWebAPI/AgripeWebAPI.csproj` when complete and fix any compilation errors.

---

## Step 3: MongoDB Setup (skip if plan says "None")

If the plan has a non-empty **MongoDB Changes** section:

1. Create new entity classes in `AgripeWebAPI/Models/Entities/` inheriting from `Entity`
2. Add `IMongoCollection<NewEntity> NewEntities { get; }` to `agpDBContext`
3. Initialize the collection in the constructor: `NewEntities = database.GetCollection<NewEntity>("new_entities");`
4. Create required indexes immediately after: `NewEntities.Indexes.CreateOne(new CreateIndexModel<NewEntity>(Builders<NewEntity>.IndexKeys.Ascending(e => e.UserId)));`
5. For new fields on existing entities, add them with sensible defaults (backward-compatible)

**Do NOT run `dotnet ef` — this project uses MongoDB, not EF Core.**

Run `dotnet build AgripeWebAPI/AgripeWebAPI.csproj` to verify.

---

## Step 4: Tests

Write unit tests for every handler and controller action added.

**Coverage & deletion rules (non-negotiable):**
- **Minimum 90% line coverage** on all production code added or modified in this issue (measure with `--collect:"XPlat Code Coverage"`; see `agripeweb-test-writer` skill).
- **Do not delete, disable, or remove existing unit tests** without **explicit approval from the user**. Fix or update tests instead.

**Conventions:**
- Framework: xUnit + Moq (no FluentAssertions)
- Mock `agpDBContext` via `Mock<agpDBContext>` + `Mock<IMongoCollection<T>>`
- Mock `ICurrentUserContext` with a fixed `UserId` (e.g., 42) in every handler test
- Mock `GetNextIdAsync` to return a deterministic ID
- For Find operations, set up an `IAsyncCursor<T>` mock
- Test files go in `AgripeWebAPI.Tests/` mirroring the production folder structure
- Naming: `{MethodName}_{Scenario}_{ExpectedResult}`

Required coverage per handler:
- Happy path — valid input returns the expected response DTO
- Validation failures — each invalid input triggers `_notifier.Handle()` and returns null
- Tenant isolation — a different `UserId` yields null/error (not another user's data)
- ID generation — `GetNextIdAsync` is called once before `InsertOneAsync` on new entities

Run `dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj` and fix any failures.

Then run with coverage and verify **≥ 90% line coverage** on touched production files:
```bash
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"
```
Do not proceed to Step 5 if coverage is below 90% or if any unit test was removed without user approval.

---

## Step 5: Code Review

Review all changed production files on this branch against the following checklist:

1. **No business logic in controllers** — only `_mediator.Send()` and `CustomResponse()`
2. **Tenant isolation** — every handler query on user-owned data filters by `_currentUser.UserId` (CRITICAL if missing)
3. **Sequential IDs** — new entities use `GetNextIdAsync()`, not Guid or ObjectId
4. **Error handling** — errors via `INotifier`, never HTTP exceptions from handlers
5. **Authorization** — `[Authorize]` on all new endpoints unless explicitly public
6. **Async/CancellationToken** — all I/O is async; `CancellationToken` forwarded everywhere; no `.Result`/`.Wait()`
7. **MongoDB** — no full-collection scans; filters always included
8. **No hardcoded secrets** — placeholder values only in committed config files
9. **DI registration** — new services registered in `ApiConfig.cs`
10. **Docs** — new endpoints reflected in plan; `CLAUDE.md` updated if new entities/services added

Classify each finding as CRITICAL / WARNING / SUGGESTION.

Present findings to the user. If there are CRITICAL findings, state clearly they must be resolved before creating a PR.

---

## Step 6: Summary

Report:

| Field | Value |
|---|---|
| Issue | #NNN — https://github.com/cclautert/AgripeWeb/issues/NNN |
| Plan saved at | `docs/features/{name}/plan.md` |
| Build passing | yes / no |
| MongoDB changes applied | yes / no / not needed |
| Tests passing | yes / no |
| Review result | X critical / X warnings / X suggestions |
| Ready for PR | yes / no |

---

## Step 7: Post to GitHub (only if tests pass and no CRITICAL findings)

### 7a — Front-end integration guide (only if new REST endpoints were added)

Post via `create_issue_comment(owner="cclautert", repo="AgripeWeb", issue_number=NNN)`:

```
## Front-End Integration Guide

### New REST Endpoints
For each endpoint:
- Method + route (e.g., POST /api/v1/Pivot)
- Auth: yes (Bearer JWT) / no
- Request body fields (name, type, required/optional)
- Response fields the front-end displays
- Example Angular HttpClient snippet using a relative URL (no hardcoded host)

### Authorization
Which Angular route guard applies.

### Notes
Validation rules, error codes, edge cases affecting the UI.
```

### 7b — PR description (present to user — do NOT create PR automatically)

```markdown
## What changed
[2-4 bullet points]

## MongoDB changes
[New collections, fields, or indexes — or "None"]

## New API endpoints
[Method + path for each — or "None"]

## Test coverage
[Summary of tests added]

Closes #NNN
```
