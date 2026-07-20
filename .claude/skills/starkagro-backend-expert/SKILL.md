---
name: starkagro-backend-expert
description: |
  Implements backend features in the StarkAgro API following CQRS/MediatR, MongoDB, and REST
  conventions. Knows the full project structure, patterns, and rules.
  Use this skill whenever the user asks to implement a handler, controller, endpoint, service,
  or any backend code in the StarkAgro project. Also trigger when the user says "write the
  code for", "implement this handler", "add this endpoint", "create this entity", or when
  a feature plan exists at docs/features/{name}/plan.md and needs to be coded.
---

# StarkAgro — Backend Expert

Implement backend features following the project's exact patterns. Read the plan at `docs/features/{name}/plan.md` before writing any code.

## Pre-task checklist

Before writing any code:
1. Read the plan (or explore the codebase if no plan exists)
2. Read the most similar existing handler + controller to understand the exact pattern
3. Identify all files to create/modify (exhaustive list)
4. Confirm: does this feature access user-owned data? If yes, `ICurrentUserContext` is required

## Handler pattern

```csharp
public class CreatePivotHandler : IRequestHandler<CreatePivotRequest, CreatePivotResponse>
{
    private readonly agpDBContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotifier _notifier;

    public CreatePivotHandler(agpDBContext db, ICurrentUserContext currentUser, INotifier notifier)
    {
        _db = db; _currentUser = currentUser; _notifier = notifier;
    }

    public async Task<CreatePivotResponse> Handle(CreatePivotRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            _notifier.Handle("Name is required");
            return null;
        }

        var pivot = new Pivot
        {
            Id = await _db.GetNextIdAsync(nameof(Pivot), cancellationToken),
            Name = request.Name,
            UserId = _currentUser.UserId   // NEVER use request.UserId
        };

        await _db.Pivots.InsertOneAsync(pivot, cancellationToken: cancellationToken);
        return new CreatePivotResponse { Id = pivot.Id, Name = pivot.Name };
    }
}
```

## Controller pattern

```csharp
[Authorize]
[ApiController]
[Route("v1/[controller]")]
public class PivotController : MainController
{
    private readonly IMediator _mediator;

    public PivotController(IMediator mediator, INotifier notifier) : base(notifier)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePivotRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(request, ct);
        return CustomResponse(result);
    }
}
```

## MongoDB query with tenant filter

```csharp
// Always include UserId in every filter
var filter = Builders<Pivot>.Filter.And(
    Builders<Pivot>.Filter.Eq(p => p.Id, request.Id),
    Builders<Pivot>.Filter.Eq(p => p.UserId, _currentUser.UserId)
);
var pivot = await _db.Pivots.Find(filter).FirstOrDefaultAsync(cancellationToken);
if (pivot is null) { _notifier.Handle("Pivot not found"); return null; }
```

## Entity pattern

```csharp
// StarkAgroAPI/Models/Entities/NewEntity.cs
public class NewEntity : Entity   // Entity provides [BsonId] int Id
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

## Key rules (non-negotiable)

| Rule | What to do |
|---|---|
| Tenant isolation | Every handler query on user-owned data must filter by `_currentUser.UserId` |
| Sequential IDs | Call `GetNextIdAsync(nameof(Entity), ct)` before every `InsertOneAsync` |
| Error reporting | Use `_notifier.Handle("msg")` + return null — never throw HTTP exceptions from handlers |
| Controller thinness | Controllers only call `_mediator.Send()` and `CustomResponse()` |
| Authorization | All new endpoints carry `[Authorize]` unless explicitly public |
| CancellationToken | Accept and forward on all async I/O |
| No secrets | Placeholder values only in committed config files |

## GitHub MCP tools reference

| Tool | When to use |
|---|---|
| `get_issue` | Fetch issue details for feature context |
| `list_issue_comments` | Read team decisions and clarifications |
| `create_issue_comment` | Post integration guide after implementing new endpoints |

## Project structure reference

```
StarkAgroAPI/
  Controllers/               -> Thin controllers (inherit MainController)
  Domain/Commands/Requests/  -> MediatR request objects
  Domain/Commands/Responses/ -> Response DTOs
  Domain/Handlers/           -> MediatR handlers (ALL business logic here)
  Models/Entities/           -> MongoDB entities (inherit Entity)
  Models/Interfaces/         -> ICurrentUserContext, IJwtTokenService, INotifier, IPasswordHasher
  Models/agpDBContext.cs     -> MongoDB collections + GetNextIdAsync()
  Services/                  -> JwtTokenService, PasswordHasherService, CurrentUserContext
  Configuration/ApiConfig.cs -> DI registration, rate limiting, CORS, auth, Swagger
  Notifications/             -> INotifier, Notificator
```

## After implementing

Run `dotnet build StarkAgroAPI/StarkAgroAPI.csproj` and fix any compilation errors before reporting done.

## Pre-commit checklist

- [ ] All business logic in handlers, not controllers
- [ ] Every query on user-owned data filters by `_currentUser.UserId`
- [ ] New entities use `GetNextIdAsync()` — no ObjectId or Guid
- [ ] Errors reported via `INotifier` — no HTTP exceptions from handlers
- [ ] New services registered in `ApiConfig.cs`
- [ ] New endpoints carry `[Authorize]` unless explicitly public
- [ ] `CancellationToken` forwarded on all I/O
- [ ] No hardcoded secrets
- [ ] `dotnet build` passes with zero errors
