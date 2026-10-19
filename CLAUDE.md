# AttendanceSystem

## Goal
Replace the legacy VB6 + SQL Server attendance module with a .NET 8 Web API.
`legacy/` is reference only: do not port or wrap it. Its rules and defects are in `docs/legacy-analysis.md`.

## Stack
- .NET 8 SDK, pinned in `global.json`. Microsoft packages use 8.0.x versions only.
- ASP.NET Core minimal APIs, EF Core with SQLite.
- Tests: xUnit, with `WebApplicationFactory` for integration tests.

## Structure
There is one API project, organized by feature, plus a `Domain` folder. Do not split it into multiple clean-architecture projects; the scope doesn't justify it.
```
global.json
src/Attendance.Api/
  Domain/            entities, value objects (SchoolYear), domain rules
  Features/<Name>/   endpoints, request/response DTOs, handlers per feature
  Data/              DbContext, EF configurations, migrations
  Program.cs
tests/Attendance.Tests/
```

## Commands
- Build: `dotnet build`
- Test: `dotnet test --nologo -v q`
- Run: `dotnet run --project src/Attendance.Api`

## Conventions
- Each feature folder holds its own endpoint mapping (`MapXxx` extension) and DTOs.
- Domain rules live in `Domain`, not in endpoints or EF configurations.
- Return validation and business-rule failures as 400 `ProblemDetails`.
- Never call `DateTime.Now`/`UtcNow`; inject `TimeProvider`. Tests use a fake clock.
- Integration tests go through HTTP via `WebApplicationFactory` against SQLite.

## Decisions
- **School year** starts Sep 1 (legacy rule), isolated in the `SchoolYear` value object.
- **Codes:** if any code in the batch is unknown or inactive, reject the whole batch with 400 ProblemDetails.
- **Save is atomic** (one transaction). Upsert is keyed by (StudentId, Date), backed by a unique index.
- **IsAbsent/IsExcused** are copied from the code when the record is saved (legacy behavior).
- **Chronic absence:** absences (excused + unexcused) in the school year >= school threshold (default 10). It is computed when read, from attendance rows; there is no stored summary table.
- **Alerts:** on save, create a `CHRONIC_ABSENCE` alert if the threshold is reached and the student has no open alert for that year.
- **Validation:** the student must exist, be active, and belong to the school. Future dates are rejected.
- **"Today"** comes from `TimeProvider`, so tests control the clock.

## Working style
- Be concise. After edits, reply in at most 3 lines: what changed and the test result.
- No recaps, and no explanations of code that is readable in the diff.
- Run tests with `dotnet test --nologo -v q`.
