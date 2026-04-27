---
name: agripeweb-test-writer
description: |
  Writes xUnit + Moq unit tests for the AgripeWeb API following the exact project patterns:
  handler tests with MongoDB cursor mocking, controller tests, service tests, and validator tests.
  Use this skill whenever the user says "write tests", "add tests", "test this handler",
  "increase test coverage", "write unit tests for", or after implementing any feature in
  the AgripeWeb project and wanting tests written.
  Also trigger when the user says "coverage is low" or "tests are missing" for AgripeWeb code.
---

# AgripeWeb — Test Writer

Write high-quality unit tests following the project's exact patterns. Never modify production code — only test files.

**Framework:** xUnit + Moq — **no FluentAssertions** (use xUnit's built-in `Assert.*` methods).

## Step 1: Read before writing

1. Read the production file(s) to test — understand every public method and its behavior
2. Read 1–2 existing test files for the same layer from `AgripeWebAPI.Tests/` — match the exact pattern and imports

## Handler test pattern

```csharp
namespace AgripeWebAPI.Tests.Handlers;

public class CreatePivotHandlerTests
{
    private readonly Mock<agpDBContext> _mockDb = new();
    private readonly Mock<IMongoCollection<Pivot>> _mockPivots = new();
    private readonly Mock<ICurrentUserContext> _mockUser = new();
    private readonly Mock<INotifier> _mockNotifier = new();
    private readonly CreatePivotHandler _sut;

    public CreatePivotHandlerTests()
    {
        _mockDb.Setup(db => db.Pivots).Returns(_mockPivots.Object);
        _mockUser.Setup(u => u.UserId).Returns(42);
        _mockDb.Setup(db => db.GetNextIdAsync(nameof(Pivot), It.IsAny<CancellationToken>()))
               .ReturnsAsync(1);
        _sut = new CreatePivotHandler(_mockDb.Object, _mockUser.Object, _mockNotifier.Object);
    }

    [Fact]
    public async Task Handle_WithValidRequest_InsertsPivotAndReturnsResponse()
    {
        // Arrange
        var request = new CreatePivotRequest { Name = "North Field" };
        _mockPivots
            .Setup(c => c.InsertOneAsync(It.IsAny<Pivot>(), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("North Field", result.Name);
        _mockPivots.Verify(
            c => c.InsertOneAsync(
                It.Is<Pivot>(p => p.UserId == 42 && p.Name == "North Field"),
                null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyName_NotifiesErrorAndReturnsNull()
    {
        var result = await _sut.Handle(new CreatePivotRequest { Name = "" }, CancellationToken.None);

        Assert.Null(result);
        _mockNotifier.Verify(n => n.Handle(It.IsAny<string>()), Times.Once);
        _mockPivots.Verify(
            c => c.InsertOneAsync(It.IsAny<Pivot>(), null, It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

## Mocking MongoDB Find (cursor pattern)

```csharp
var pivot = new Pivot { Id = 1, UserId = 42, Name = "North Field" };

var mockCursor = new Mock<IAsyncCursor<Pivot>>();
mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
          .ReturnsAsync(true).ReturnsAsync(false);
mockCursor.Setup(c => c.Current).Returns(new[] { pivot });

_mockPivots
    .Setup(c => c.FindAsync(
        It.IsAny<FilterDefinition<Pivot>>(),
        It.IsAny<FindOptions<Pivot>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(mockCursor.Object);
```

## Tenant isolation test (mandatory for every handler accessing user-owned data)

```csharp
[Fact]
public async Task Handle_Always_FiltersQueryByCurrentUserId()
{
    _mockUser.Setup(u => u.UserId).Returns(99);

    var mockCursor = new Mock<IAsyncCursor<Pivot>>();
    mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
    _mockPivots
        .Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<Pivot>>(),
            It.IsAny<FindOptions<Pivot>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(mockCursor.Object);

    var result = await _sut.Handle(new GetPivotRequest { Id = 1 }, CancellationToken.None);

    Assert.Null(result);
    _mockNotifier.Verify(n => n.Handle(It.IsAny<string>()), Times.Once);
}
```

## Parameterized validation tests

```csharp
[Theory]
[InlineData("")]
[InlineData(" ")]
[InlineData(null)]
public async Task Handle_WithInvalidName_NotifiesError(string? name)
{
    var result = await _sut.Handle(new CreatePivotRequest { Name = name! }, CancellationToken.None);

    Assert.Null(result);
    _mockNotifier.Verify(n => n.Handle(It.IsAny<string>()), Times.Once);
}
```

## Test naming convention

`{MethodName}_{Scenario}_{ExpectedResult}`

Examples:
- `Handle_WithValidRequest_InsertsPivotAndReturnsResponse`
- `Handle_WithEmptyName_NotifiesErrorAndReturnsNull`
- `Handle_WhenPivotNotFound_NotifiesErrorAndReturnsNull`
- `Handle_Always_FiltersQueryByCurrentUserId`
- `Create_WhenHandlerSucceeds_ReturnsOk`

## Required coverage per handler

- Happy path — valid input returns the expected response DTO
- Each validation failure — triggers `_notifier.Handle()` + returns null
- Tenant isolation — different `UserId` yields null/error
- ID generation — `GetNextIdAsync` called once before `InsertOneAsync`
- Update/delete — filter includes both `Id` and `UserId`

## File placement

Test files go in `AgripeWebAPI.Tests/` mirroring the production folder:
- Handler tests -> `AgripeWebAPI.Tests/Handlers/`
- Controller tests -> `AgripeWebAPI.Tests/Controllers/`
- Service tests -> `AgripeWebAPI.Tests/Services/`

## After writing

Run `dotnet test AgripeWebAPI.Tests/AgripeWebAPI.Tests.csproj --logger "console;verbosity=normal"` and fix any failures.

Flag any scenario that requires a production code change to be testable (missing interface, untestable static call).
