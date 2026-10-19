using Attendance.Api.Data;
using Attendance.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Features.Attendance;

public record AttendanceEntry(int StudentId, string? Code, int MinutesLate, string? Notes);

public static class SaveDailyAttendance
{
    public static void MapSaveDailyAttendance(this IEndpointRouteBuilder app) =>
        app.MapPut("/schools/{schoolId:int}/attendance/{date}", HandleAsync);

    private static IResult Invalid(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Invalid attendance batch");

    private static async Task<IResult> HandleAsync(
        int schoolId, string date, AttendanceEntry[]? entries, AttendanceDbContext db, TimeProvider time)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var attendDate))
            return Invalid("Date must be formatted yyyy-MM-dd.");
        if (attendDate > DateOnly.FromDateTime(time.GetLocalNow().DateTime))
            return Invalid("Future dates are not allowed.");
        if (entries is null || entries.Length == 0)
            return Invalid("Batch must contain at least one entry.");

        var school = await db.Schools.FindAsync(schoolId);
        if (school is null) return Results.Problem(statusCode: 404, title: "School not found");

        var dupes = entries.GroupBy(e => e.StudentId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0) return Invalid($"Duplicate students in batch: {string.Join(", ", dupes)}.");

        var ids = entries.Select(e => e.StudentId).ToList();
        var students = await db.Students.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        var badStudents = ids.Where(i => !students.TryGetValue(i, out var s) || !s.Active || s.SchoolId != schoolId).ToList();
        if (badStudents.Count > 0)
            return Invalid($"Students not found, inactive, or not in this school: {string.Join(", ", badStudents)}.");

        var codes = await db.Codes.Where(c => c.IsActive).ToDictionaryAsync(c => c.Value);
        var badCodes = entries.Where(e => e.Code is null || !codes.ContainsKey(e.Code)).Select(e => e.Code ?? "(null)").Distinct().ToList();
        if (badCodes.Count > 0) return Invalid($"Unknown or inactive codes: {string.Join(", ", badCodes)}.");

        if (entries.Any(e => e.MinutesLate < 0)) return Invalid("minutesLate cannot be negative.");
        if (entries.Any(e => e.Notes?.Length > 500)) return Invalid("notes cannot exceed 500 characters.");

        var now = time.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync();

        var existing = await db.Records.Where(r => r.Date == attendDate && ids.Contains(r.StudentId))
            .ToDictionaryAsync(r => r.StudentId);
        foreach (var e in entries)
        {
            var code = codes[e.Code!];
            if (!existing.TryGetValue(e.StudentId, out var rec))
            {
                rec = new AttendanceRecord { StudentId = e.StudentId, SchoolId = schoolId, Date = attendDate, CreatedAt = now };
                db.Records.Add(rec);
            }
            else rec.ModifiedAt = now;
            rec.Code = code.Value;
            rec.IsAbsent = code.IsAbsent;
            rec.IsExcused = code.IsExcused;
            rec.MinutesLate = e.MinutesLate;
            rec.Notes = e.Notes;
        }
        await db.SaveChangesAsync();

        var year = SchoolYear.For(attendDate);
        var yearText = year.ToString();
        var counts = await db.Records
            .Where(r => ids.Contains(r.StudentId) && r.IsAbsent && r.Date >= year.Start && r.Date <= year.End)
            .GroupBy(r => r.StudentId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var open = (await db.Alerts
            .Where(a => ids.Contains(a.StudentId) && a.AlertType == StudentAlert.ChronicAbsence
                        && a.SchoolYear == yearText && a.ResolvedAt == null)
            .Select(a => a.StudentId).ToListAsync()).ToHashSet();
        foreach (var id in ids)
        {
            var n = counts.GetValueOrDefault(id);
            if (!ChronicAbsencePolicy.IsChronic(n, school.AbsenceAlertThreshold) || open.Contains(id)) continue;
            db.Alerts.Add(new StudentAlert
            {
                StudentId = id, SchoolId = schoolId, AlertType = StudentAlert.ChronicAbsence, SchoolYear = yearText,
                CreatedAt = now, Message = $"Student has reached {n} absences this year",
            });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.NoContent();
    }
}
