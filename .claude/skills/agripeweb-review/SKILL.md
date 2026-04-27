---
name: agripeweb-review
description: |
  Code review skill for the AgripeWeb project. Reviews all branch changes before a PR is created,
  checking tenant isolation, MediatR patterns, MongoDB usage, authorization, and more.
  Use this skill whenever the user says "review my code", "review this branch", "check before PR",
  "is this ready to merge", or asks for a code review in the AgripeWeb project context.
  Also trigger when the user provides a PR URL, says "review PR #NNN", or wants a pre-merge quality check.
---

# AgripeWeb — Code Review

Review all code changes on the current branch before creating a PR. This skill is read-only — it reports findings but never modifies files.

## GitHub MCP tools reference

| Tool | When to use |
|---|---|
| `list_pull_requests` | Check open PRs for this branch |
| `get_pull_request` | Fetch PR details and diff |
| `create_pull_request_review` | Post review comments on a PR |

## Step 1: Build check

Run `dotnet build AgripeWebAPI/AgripeWebAPI.csproj 2>&1`. Any compilation error is **CRITICAL** — report immediately and stop.

## Step 2: Identify changed files

```bash
git diff $(git merge-base HEAD main) HEAD --name-only   # committed changes
git diff HEAD --name-only                                # uncommitted changes
```

Read each changed **production** file (skip test files).

## Step 3: Run the full checklist

For each failure record: file path + line number + exact issue + rule violated.

### 1. No business logic in controllers
- Controllers only call `_mediator.Send()` and `CustomResponse()` — nothing else
- No direct MongoDB access in controllers
- No domain `if/else` logic in controllers

### 2. Tenant isolation — most critical check
For every handler accessing user-owned collections (`pivots`, `sensors`, `read_sensors`):
- Handler injects `ICurrentUserContext`
- Every query/update/delete filter includes `Builders<T>.Filter.Eq(x => x.UserId, _currentUser.UserId)`
- No use of `request.UserId` or any client-supplied value as a data filter

### 3. Sequential IDs
For every new entity insertion:
- ID set via `await _db.GetNextIdAsync(nameof(Entity), cancellationToken)`
- `GetNextIdAsync` called before `InsertOneAsync`

### 4. Error handling — INotifier pattern
- Handlers report errors via `_notifier.Handle("message")` + return null
- No `throw new HttpRequestException()` or `BadRequest()` from handlers
- Controllers call `CustomResponse(result)` — never manually check notifier state

### 5. Authorization on endpoints
- Every new controller action has `[Authorize]` unless explicitly public
- Public endpoints documented with a comment explaining why

### 6. Async / CancellationToken
- All I/O methods are `async Task` / `async Task<T>`
- `CancellationToken` forwarded to all async I/O calls
- No `.Result` or `.Wait()` on tasks

### 7. MongoDB usage
- All DB access through `agpDBContext` — no raw `MongoClient` or `IMongoDatabase` in handlers
- No `Find().ToList()` without a filter
- Missing index on high-frequency query field → flag as WARNING

### 8. No hardcoded secrets
- No connection strings, JWT keys, or OAuth credentials in code or committed config

### 9. DI registration
- New services registered in `ApiConfig.cs` with correct lifetime

### 10. Documentation auto-update
- New controller action → API docs updated (if `docs/api/` exists)
- New entity field → entity table in `CLAUDE.md` updated
- New significant feature → `docs/features/{name}/README.md` exists

## Step 4: Report

```
### Summary
Total issues: X (Critical: X | Warnings: X | Suggestions: X)

### CRITICAL — must fix before merging
| # | File | Line | Rule | Issue |

### WARNING — should fix
| # | File | Line | Rule | Issue |

### SUGGESTION — consider improving
| # | File | Line | Rule | Issue |

### Passed Checks
[list each passing item]

Branch ready to merge: yes / no
```

$ARGUMENTS
