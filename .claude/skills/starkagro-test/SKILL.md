---
name: starkagro-test
description: |
  Runs the StarkAgro test suite and reports results. Supports running API tests, worker tests,
  all tests, coverage reports, and single-test filtering.
  Use this skill whenever the user says "run tests", "run the tests", "check tests", "are tests
  passing", "test coverage", or wants to verify the test suite in the StarkAgro project.
  Also trigger when the user says "run /test" or provides dotnet test arguments.
---

# StarkAgro — Test Runner

Run the test suite and report results.

## Usage modes

| Argument | Behavior |
|---|---|
| *(none)* | API unit tests only |
| `--worker` | Worker service tests only |
| `--all` | All test projects |
| `--coverage` | API tests + coverage report |
| `--filter MethodName` | Single test by name fragment |
| *(any other value)* | Passed directly to `dotnet test` |

## Commands

```bash
# Default — API tests
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --logger "console;verbosity=normal"

# Worker tests
dotnet test StarkAgroWorker.Tests/StarkAgroWorker.Tests.csproj --logger "console;verbosity=normal"

# All projects
dotnet test --logger "console;verbosity=normal"

# With coverage
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"

# Single test
dotnet test StarkAgroAPI.Tests/StarkAgroAPI.Tests.csproj --filter "FullyQualifiedName~MethodName" --logger "console;verbosity=normal"
```

## Reporting

Parse the output and report:
- Total: X passed / X failed / X skipped
- Duration
- Failed tests: name + first 5 lines of error output

If tests fail with MongoDB connection errors, check if MongoDB is running:
```bash
docker ps --filter name=mongo
```
If not running: "MongoDB container is not running. Start it with: `docker compose -f docker/docker-compose.yml up -d mongo`"

If `--coverage` was used, parse the coverage summary and report line coverage per file.

**Coverage gate:** the project requires **≥ 90% line coverage** on production code under test. If any file is below 90%, list it as **FAIL** (not just a suggestion) and invoke or recommend the `starkagro-test-writer` skill to close the gap.

**Test deletion:** if the diff removes unit test files or test methods, flag as **BLOCKED** unless the user has explicitly approved the removal in this conversation.

## Output format

```
Test Run: X passed / X failed / X skipped (Xms)
Project: StarkAgroAPI.Tests | StarkAgroWorker.Tests | All

[FAILED] TestClassName.MethodName
  -> Expected: ...
  -> Actual: ...

Coverage: X%  <- only if --coverage
```

$ARGUMENTS
