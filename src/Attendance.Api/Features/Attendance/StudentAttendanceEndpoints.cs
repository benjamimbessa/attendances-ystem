using Attendance.Api.Data;
using Attendance.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Features.Attendance;

public record StudentAttendanceItem(
    DateOnly Date, string Code, string Description, bool IsAbsent, bool IsExcused, int MinutesLate, string? Notes);

public record ChronicAbsenteeismResult(string SchoolYear, int Absences, int Threshold, bool IsChronic);

public static class StudentAttendanceEndpoints
{
    public static void MapStudentAttendance(this IEndpointRouteBuilder app)
    {
        app.MapGet("/students/{studentId:int}/attendance", GetAttendanceAsync);
        app.MapGet("/students/{studentId:int}/chronic-absenteeism", GetChronicAsync);
    }

    private static SchoolYear Resolve(string? text, TimeProvider time, out bool ok)
    {
        if (text is null)
        {
            ok = true;
            return SchoolYear.For(DateOnly.FromDateTime(time.GetLocalNow().DateTime));
        }
        ok = SchoolYear.TryParse(text, out var year);
        return year;
    }

    private static IResult BadYear() =>
        Results.Problem(detail: "schoolYear must look like 2025-2026.", statusCode: 400, title: "Invalid school year");

    private static async Task<IResult> GetAttendanceAsync(
        int studentId, string? schoolYear, AttendanceDbContext db, TimeProvider time)
    {
        var year = Resolve(schoolYear, time, out var ok);
        if (!ok) return BadYear();
        if (!await db.Students.AnyAsync(s => s.Id == studentId))
            return Results.Problem(statusCode: 404, title: "Student not found");

        var start = year.Start;
        var end = year.End;
        var rows = await db.Records
            .Where(r => r.StudentId == studentId && r.Date >= start && r.Date <= end)
            .OrderByDescending(r => r.Date)
            .Join(db.Codes, r => r.Code, c => c.Value,
                (r, c) => new StudentAttendanceItem(r.Date, r.Code, c.Description, r.IsAbsent, r.IsExcused, r.MinutesLate, r.Notes))
            .ToListAsync();
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetChronicAsync(
        int studentId, string? schoolYear, AttendanceDbContext db, TimeProvider time)
    {
        var year = Resolve(schoolYear, time, out var ok);
        if (!ok) return BadYear();
        var student = await db.Students.FindAsync(studentId);
        if (student is null) return Results.Problem(statusCode: 404, title: "Student not found");

        var school = await db.Schools.FindAsync(student.SchoolId);
        var threshold = school?.AbsenceAlertThreshold ?? ChronicAbsencePolicy.DefaultThreshold;
        var start = year.Start;
        var end = year.End;
        var absences = await db.Records.CountAsync(r =>
            r.StudentId == studentId && r.IsAbsent && r.Date >= start && r.Date <= end);
        return Results.Ok(new ChronicAbsenteeismResult(
            year.ToString(), absences, threshold, ChronicAbsencePolicy.IsChronic(absences, threshold)));
    }
}
