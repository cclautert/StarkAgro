---
name: agripeweb-feature-planner
description: |
  Creates a detailed implementation plan for an AgripeWeb feature by fetching the GitHub Issue
  and exploring the codebase. Saves the plan to docs/features/{name}/plan.md.
  Use this skill whenever the user says "plan this feature", "create a plan for issue #NNN",
  "what needs to change for issue NNN", "explore the codebase for feature NNN", or wants to
  understand the implementation scope before writing any code in the AgripeWeb project.
  Always use this skill BEFORE starting any backend implementation.
---

# AgripeWeb — Feature Planner

Produce a complete, accurate implementation plan. Never write code — only plan.

## Step 1: Fetch the GitHub Issue

Use the `github` MCP tool:
```
get_issue(owner="cclautert", repo="AgripeWeb", issue_number=NNN)
list_issue_comments(owner="cclautert", repo="AgripeWeb", issue_number=NNN)
```

Extract:
- **Description** — functional scope and intent
- **Acceptance Criteria** — treat each as a hard requirement
- **Business Rules** — constraints to enforce in handlers
- **Comments** — team decisions and clarifications

### GitHub MCP tools reference

| Tool | When to use |
|---|---|
| `get_issue` | Fetch issue title, body, labels, assignees |
| `list_issue_comments` | Fetch all comments (team decisions, clarifications) |
| `create_issue_comment` | Post plan summary back to issue |
| `list_pull_requests` | Check if a PR already exists for this issue |

## Step 2: Explore the codebase

Use Glob and Grep to find relevant files, then read them. Look for:
1. The most similar existing handler + controller pair — understand the exact pattern to follow
2. The relevant entity/entities in `AgripeWebAPI/Models/Entities/` — current fields and collection name
3. Existing request/response DTOs in `Domain/Commands/` for naming consistency
4. Whether the feature needs a new MongoDB collection or just new fields/indexes
5. Whether the Angular UI needs changes (new route, service call, component)

## Step 3: Produce and save the plan

Write a structured plan to `docs/features/{kebab-case-name}/plan.md` with this exact structure:

```markdown
# Implementation Plan: {Issue Title} (#{NNN})
Issue: https://github.com/cclautert/AgripeWeb/issues/NNN
Generated: {YYYY-MM-DD}

## Context
Why this feature is needed (1–3 sentences).

## Acceptance Criteria
1. [criterion] → [implementation decision]
2. ...

## Affected Layers
[handlers / controllers / entities / agpDBContext / Angular UI / MQTT worker]

## New REST Endpoints
| Method | Route | Auth | Request fields | Response fields |
|---|---|---|---|---|

## Files to Create
| Path | Type | Summary |
|---|---|---|

## Files to Modify
| Path | Change |
|---|---|

## MongoDB Changes
[New collections, fields, indexes — or "None"]

## Tenant Isolation Plan
[For each new query: how it filters by _currentUser.UserId]
[Or: "No user-owned data accessed — explain why"]

## Risks & Flags
- [CRITICAL] ...
- [WARNING] ...

## DI Registration
| Interface | Implementation | Lifetime | File |
|---|---|---|---|

## Verification
dotnet build AgripeWebAPI/AgripeWebAPI.csproj
dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj
[Sample HTTP request snippets]
```

After saving, output the file path and present the plan to the user for confirmation.

## AgripeWeb Architecture Quick Reference

**Entities and collections:**
- `User` → `users` | `Pivot` → `pivots` | `Sensor` → `sensors` | `ReadSensor` → `read_sensors`
- All inherit `Entity` with `[BsonId] int Id`
- Sequential IDs via `GetNextIdAsync(nameof(Entity), ct)` on `counters` collection

**Key interfaces:**
- `ICurrentUserContext` — resolves `UserId` from JWT claim `"id"` (NEVER trust client-supplied user IDs)
- `INotifier` / `Notificator` — error accumulation; handlers call `.Handle("msg")` and return null
- `IJwtTokenService` — JWT generation/validation
- `IPasswordHasher` — BCrypt hash/verify

**Architecture rules:**
- CQRS/MediatR: every operation is a Request+Handler pair; controllers only call `_mediator.Send()` + `CustomResponse()`
- All user-owned data queries MUST filter by `_currentUser.UserId`
- New entities use `GetNextIdAsync()` — never Guid or ObjectId
- No EF Core — all DB access through `agpDBContext` and `IMongoCollection<T>`
- New endpoints carry `[Authorize]` unless explicitly public
- CORS: dev → `localhost:4200`; prod → `agripeweb.com`
