# Attendance API

.NET 8 minimal API (EF Core + SQLite) replacing a legacy VB6 + SQL Server attendance module. The legacy code in `legacy/` is reference only. See `docs/legacy-analysis.md` for the full analysis.

## Run and test
```
dotnet run --project src/Attendance.Api --launch-profile http   # http://localhost:5156
dotnet test --nologo -v q                                        # unit + integration tests
```
Requires the .NET 8 SDK (pinned in `global.json`). The SQLite file `attendance.db` is created and seeded on first start: school 1, students 1-3 active, student 4 inactive, and codes `P`, `A`, `AE`, `T` plus the inactive `X`. Try the flow in `Attendance.http`. Override the database with `ConnectionStrings:Attendance`.

| Endpoint | Purpose |
|---|---|
| `PUT /schools/{id}/attendance/{yyyy-MM-dd}` | Save a day for a batch of students |
| `GET /students/{id}/attendance?schoolYear=2025-2026` | History, newest first (default: current year) |
| `GET /students/{id}/chronic-absenteeism?schoolYear=...` | Absence count, threshold, chronic flag |

## Key decisions and why
- **One project, organized by feature, plus `Domain/`.** The scope doesn't justify multi-project clean architecture.
- **Atomic save, keyed by (StudentId, Date) with a unique index.** This fixes the legacy bug where one student's row was overwritten with another's.
- **Whole batch rejected on any bad input** (unknown or inactive code, ineligible student, future date), as 400 ProblemDetails. Nothing is partly saved.
- **No stored summary table.** Chronic status is computed on read from attendance rows, so it can't go stale.
- **IsAbsent/IsExcused are copied from the code at save time**, so later changes to a code don't rewrite history.
- **`SchoolYear` value object** holds the Sep 1 rule in one place (legacy repeated it three times).
- **`TimeProvider`** supplies "today", so tests control the clock.
- **Alerts:** a `CHRONIC_ABSENCE` alert is created on save when the threshold is reached and the student has no open alert for that year.

## What the legacy didn't tell us
| Question | How we handled it |
|---|---|
| Do excused absences count toward chronic? | Yes, as in the legacy code. Only the total is exposed. |
| Where does the school year start? | Sep 1 for everyone. It is not per school. |
| Unknown or inactive attendance code? | Reject the batch. The legacy saved it as "present" and hid the row on read. |
| Student left out of the batch or sent with a blank code? | The legacy defaulted to `P`. We require an explicit valid code and create no record for omitted students. |
| Which dates can be entered? | Any past date and not the future. The legacy UI only allowed today. |
| What resolves an alert? | **Open.** Alerts are only created. Nothing resolves or reads them yet. |
| Threshold? | A count, default 10, with an optional per-school value. No endpoint sets it yet. |
| Concurrent edits? | The unique index prevents duplicates. Last write wins, and a race on a first insert returns 500 instead of 409. |
| Students who transfer? | **Open.** Absences are counted per student across schools, using the current school's threshold. |
| Tardiness rules? | **Open.** `minutesLate` is stored and not validated against the code. |
| Who saved a record? | **Open.** There is no auth, so only timestamps are recorded. |
| Terms, the submission log, the print report, roster endpoints? | Not carried over. This was never decided. |
