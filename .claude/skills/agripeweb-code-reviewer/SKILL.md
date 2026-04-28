---
name: agripeweb-code-reviewer
description: |
  Reviews AgripeWeb code changes against the full project ruleset: tenant isolation, MediatR
  handler patterns, MongoDB usage, authorization, async correctness, and documentation.
  Read-only — never modifies files, only reports findings.
  Use this skill whenever the user says "review this code", "check the code", "is this correct",
  "code review for AgripeWeb", or after implementing any feature and wanting a quality check.
  Also trigger when the user asks "what's wrong with this handler" or "is this safe to merge".
---

# AgripeWeb — Code Reviewer

Find violations — do not fix them. Read code, check against rules, report precisely. Never modify any file.

## Workflow

1. **Build check first** — run `dotnet build AgripeWebAPI/AgripeWebAPI.csproj 2>&1`. Any compilation error is **CRITICAL** — report immediately and stop.
2. Identify changed files:
   ```bash
   git diff $(git merge-base HEAD main) HEAD --name-only   # committed
   git diff HEAD --name-only                                # uncommitted
   ```
3. Read each changed **production** file (skip test files)
4. Run every applicable checklist item
5. Collect and report all findings

## Review checklist

For each failure: **file path + line number + exact issue + rule violated**.

### 1. No business logic in controllers *(CRITICAL if violated)*
- Controllers only call `_mediator.Send()` and `CustomResponse()`
- No direct MongoDB access (`agpDBContext`, `IMongoCollection`) in controllers
- No domain `if/else` logic in controllers

### 2. Tenant isolation *(CRITICAL if any query missing UserId filter)*
For every handler accessing user-owned collections (`pivots`, `sensors`, `read_sensors`):
- Handler injects `ICurrentUserContext`
- Every `Find`/`UpdateOne`/`DeleteOne` filter includes `Builders<T>.Filter.Eq(x => x.UserId, _currentUser.UserId)`
- No use of `request.UserId` or any client-supplied value as a data filter

### 3. Sequential IDs
- New entity ID set via `await _db.GetNextIdAsync(nameof(Entity), cancellationToken)`
- Called **before** `InsertOneAsync`
- No Guid, no 0, no MongoDB ObjectId

### 4. Error handling — INotifier pattern
- Handlers report errors via `_notifier.Handle("message")` + return `null`
- No `throw new HttpRequestException()` or manual `BadRequest()` from handlers
- Controllers call `CustomResponse(result)` — never inspect notifier manually

### 5. Authorization
- Every new `[HttpGet/Post/Put/Delete]` action has `[Authorize]` unless explicitly public
- Public endpoints have a comment explaining why auth is not required

### 6. Async / CancellationToken
- All I/O methods: `async Task` / `async Task<T>`
- `CancellationToken` accepted as parameter and forwarded to all async I/O calls
- No `.Result` or `.Wait()` on tasks
- No `async void` (except event handlers)

### 7. MongoDB usage
- All DB access through `agpDBContext` — no raw `MongoClient` or `IMongoDatabase` in handlers
- No `Find().ToList()` without a filter (full-collection scan)
- New query filtering by a field that is not `Id` or `UserId` — flag missing index as WARNING

### 8. No hardcoded secrets
- No connection strings, JWT keys, BCrypt parameters, or OAuth credentials in code or committed config
- `appsettings.json` uses placeholder values (`CHANGE_ME`, `YOUR_GOOGLE_CLIENT_ID`)

### 9. DI registration
- New services registered in `ApiConfig.cs` with correct lifetime:
  - Scoped: handlers, services
  - Singleton: stateless utilities
  - Transient: validators

### 10. Code standards
- English only — identifiers, comments, string literals
- No emojis in code files
- `CancellationToken` parameter present and not ignored

### 11. Documentation auto-update
- New controller action — API docs updated if `docs/api/` exists
- New entity field — entity table in `CLAUDE.md` updated
- New significant feature — `docs/features/{name}/README.md` exists

Report missing documentation updates as **WARNING**.

## Output format

```
### Summary
Total issues: X  (Critical: X | Warnings: X | Suggestions: X)
Checklist items run: 11

### CRITICAL — must fix before merging
| # | File | Line | Rule | Issue |
|---|---|---|---|---|

### WARNING — should fix
| # | File | Line | Rule | Issue |
|---|---|---|---|---|

### SUGGESTION — consider improving
| # | File | Line | Rule | Issue |
|---|---|---|---|---|

### Passed Checks
[one line per passing item]

Branch ready to merge: yes / no
```

$ARGUMENTS
