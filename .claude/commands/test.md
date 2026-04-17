Run the test suite and report results. Optionally invokes test-writer if coverage gaps are found.

## Usage
- `/test` — runs all unit tests (main project)
- `/test --all` — runs all tests including integration tests (requires SQL Server Docker)
- `/test --coverage` — runs with coverage and checks 80% threshold
- `/test --integration` — runs only integration tests
- `/test $ARGUMENTS` — pass any additional dotnet test arguments

## Steps

1. Run tests based on arguments:
   - Default (unit only): `dotnet test AP.AlixVault.API/Tests/UnitTests/AlixVault.UnitTests/ --logger "console;verbosity=normal"`
   - `--all`: `dotnet test --logger "console;verbosity=normal"` (runs all projects)
   - `--coverage`: `dotnet test AP.AlixVault.API/Tests/UnitTests/AlixVault.UnitTests/ --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"`
   - `--integration`: `dotnet test AP.AlixVault.API/AlixVault.Tests/AlixVault.IntegrationTests/ --logger "console;verbosity=normal"`

2. Parse the output and report:
   - Total: X passed / X failed / X skipped
   - Duration
   - Any failed tests: name + error message (first 5 lines)

3. If tests fail:
   - Show the failing test names and error messages
   - Do NOT automatically fix — report findings and ask the user how to proceed

4. If integration tests fail with connection errors:
   - Check if SQL Server Docker container is running: `docker ps --filter name=alixvault-sqlserver`
   - If not running, inform the user: "SQL Server Docker container is not running. Start it with: docker-compose up -d sqlserver"

5. If `--coverage` was used and coverage < 80%:
   - List the top files with lowest coverage
   - Ask the user if they want to invoke the `test-writer` agent to fill the gaps

## Output format

```
Test Run: X passed / X failed / X skipped (Xms)
Project: UnitTests | IntegrationTests | All

[FAILED] TestClassName.MethodName
  → Expected: ...
  → Actual: ...

Coverage: X% (threshold: 80%)  ← only if --coverage
```
