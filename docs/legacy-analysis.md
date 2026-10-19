# Legacy Attendance Module — Analysis

Sources: `legacy/Forms/frmDailyAttendance.frm` (FRM), `legacy/Database/schema.sql` (SCH), `sp_SaveDailyAttendance.sql` (SAVE), `sp_GetStudentAttendance.sql` (GET). Citations are `file:line`. *Guess* marks inference not proven by the code.

## 1. End-to-end behavior (business rules)
**UI (FRM).** User picks a school (active schools, FRM:47); the grade list and the student grid load (FRM:54-58). The attendance date is always today (FRM:40); there is no date picker. The grid shows Student ID, names, Code, Min Late, Notes. A student with no record shows code `P` and 0 minutes (FRM:90-91). *Guess:* `P` means Present.
**Write path.** Save sends every grid row as XML. A blank code becomes `P` (FRM:109), and non-numeric minutes become 0 via `Val` (FRM:112). For each student, SAVE:
- Looks up IsAbsent/IsExcused from `AttendanceCodes`. An unknown code is stored as present and not excused (SAVE:46-53).
- Upserts one row per student per date. An update does not change TermID (SAVE:54-77).
- Sets TermID from `SchoolTerms` by date range. It is NULL if no term matches (SAVE:36-39).
- Recounts the year's absences into `StudentAttendanceSummary` (SAVE:79-96). Excused absences are counted too: the filter is only `IsAbsent = 1`.
- Raises a `CHRONIC_ABSENCE` alert when absences ≥ the school threshold (default 10), unless an open alert already exists for that year (SAVE:97-115).

After the loop, one row is written to `AttendanceSubmissionLog` (SAVE:120). Audit user = `SYSTEM_USER`, the SQL login, not the end user.
**School year.** It starts Sept 1 and is labelled `YYYY-YYYY`. This is hard-coded in 3 places (SAVE:32, GET:10, GET:30).
**Read path (GET).** Given a student and a year (default: current), GET returns 2 result sets. Set 1 is the daily rows, newest first, with code description and term name. Set 2 is the summary total plus `IsChronicallyAbsent`, computed live against the threshold (GET:40). The form never calls GET; the caller is unknown.
**Print.** The Print button prints a Crystal report for the selected school and today's date (FRM:123-131). It prints saved data, not unsaved grid edits.

## 2. Data model and derived data
- `Students`, `AttendanceCodes` (CodeValue unique; IsAbsent/IsExcused/IsActive flags).
- `StudentAttendance`: one logical row per student+date. IsAbsent/IsExcused are copied from the code when the row is saved, so they are not looked up when read.
- `StudentAttendanceSummary`: TotalAbsences per (StudentID, SchoolYear), with a unique key (SCH:45). SchoolID is stored but is not part of the key.
- `StudentAlerts`: open while ResolvedDate is NULL.
- There are no foreign keys or indexes, and no unique key on (StudentID, AttendDate) (SCH:21-36).

**When derived data changes:** summary and alerts update only inside SAVE, and only for students in that submission. Alerts are only ever inserted. Nothing in scope resolves an alert, updates its message, or removes it when absences later drop. Any other write to `StudentAttendance` leaves the summary stale.

## 3. Defects and risks
1. **Wrong row overwritten.** `@ExistingID` is never reset inside the cursor (SAVE:54). Once one student has an existing row, each later student with no row UPDATEs that earlier row instead of inserting a new one. This silently corrupts data.
2. **Absence flags leak between students.** `@IsAbsent`/`@IsExcused` are not reset (SAVE:46). For an unknown code, the `IS NULL` fallback (SAVE:49) works only for the first student; later students inherit the previous student's flags.
3. **Summary recount is broken.** `SchoolYear(@AttendDate)` (SAVE:83) is an unqualified, undefined UDF, so SQL Server raises "not a recognized built-in function" when it runs. Even if it existed, the recount uses the parameter instead of the column, so it would count absences across all years.
4. **SQL injection / broken input.** The grade text (FRM:72) and the whole XML (FRM:116) are concatenated into SQL. A `'` in Notes breaks the call or injects SQL; a `"`, `<` or `&` in Notes breaks the XML.
5. **No transaction or TRY/CATCH** in SAVE. A mid-batch failure leaves a partial save while the form reports an error, or the user retries the whole batch.
6. **Unknown or inactive codes are accepted** (SAVE:46; IsActive is never checked). These rows then disappear on read because of `INNER JOIN AttendanceCodes` (GET:27).
7. **Missing `Schools` row means a NULL threshold** (SAVE:98: ISNULL does not apply when no row is returned), so no alert is raised. GET still shows the default of 10. The two disagree.
8. **No unique key on (StudentID, AttendDate)**, so concurrent saves can create duplicates. Last write wins for the whole grid.
9. **Grade filter is ineffective.** There is no `cboGrade_Click`, and `LoadStudents` runs right after `cboGrade.Clear` (FRM:56-60), so the grade passed is always `''`.
10. The connection string uses `Data Source=<path>\config.ini` (FRM:38). That is not a valid SQLOLEDB server, so the real configuration mechanism is unknown.
11. **Crashes and unguarded input.** `cmdPrint_Click` has no error handler, and Save/Print run with SchoolID 0 if no school is selected (FRM:101, 123).
12. **Text truncation.** `NVARCHAR` XML is cast to `VARCHAR(500)` notes (SAVE:29), so non-Latin characters are lost.
13. **Misleading alert text.** The alert message freezes the count at creation time. `IsChronicallyAbsent` (GET:40) and `StudentAlerts` are two sources of truth.
14. **Weak audit trail.** `SYSTEM_USER` is the database login, so there is no real audit of who saved. *Guess:* the app uses a shared login.

## 4. Referenced but not provided
- Tables: `Schools` (SchoolName, Active, AbsenceAlertThreshold), `SchoolTerms` (TermID, SchoolID, StartDate, EndDate, TermName), `AttendanceSubmissionLog`.
- Function `SchoolYear()`; proc `sp_GetStudentsForAttendance` (its columns and grade/date semantics).
- VB helper `Nz` (not built into VB6); `config.ini`; `Reports\DailyAttendance.rpt` and its `Attendance` table/view; Crystal runtime; ADODB.
- `AttendanceCodes` seed data, including whether `P` exists. The grid-editing code (MSFlexGrid is not editable by itself).
- Whatever resolves alerts, consumes alerts, or calls GET.

## 5. Decisions for the product owner
| # | Topic | Legacy behavior | Recommendation |
|---|---|---|---|
| 1 | Excused absences toward chronic threshold | Counted (IsAbsent only) | Decide explicitly; store and report excused and unexcused counts separately either way |
| 2 | School-year boundary | Hard-coded Sept 1 | Drive from a per-school calendar |
| 3 | Unknown/inactive code | Saved as present, hidden on read | Reject with a validation error |
| 4 | Unmarked student | Defaults to `P` on save | Require explicit marks, or confirm `P` as an intended default |
| 5 | Attendance date | UI: today only; DB: any date | Allow past dates (with audit); reject future dates |
| 6 | Alert lifecycle | Never auto-resolved; new alert after manual resolve | Define who resolves and whether dropping below threshold closes it |
| 7 | Threshold | Absolute count, default 10 | Confirm count vs. rate; make the default configurable |
| 8 | Concurrent edits | Last write wins, whole grid | Optimistic concurrency per record |
| 9 | Student transfers | Summary per student+year, filtered by school on recount | Decide per-school vs. per-student yearly totals |
| 10 | Tardiness | Minutes stored, no rules | Confirm whether tardies accumulate or alert |
| 11 | Audit identity | DB login | Record the authenticated app user |
