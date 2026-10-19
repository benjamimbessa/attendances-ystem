using System.Net;
using System.Net.Http.Json;
using Attendance.Api.Domain;
using Attendance.Api.Features.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Tests.Api;

public class AttendanceApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public AttendanceApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static AttendanceEntry E(int id, string code, int late = 0, string? notes = null) => new(id, code, late, notes);

    private Task<HttpResponseMessage> Save(string date, params AttendanceEntry[] entries) =>
        _client.PutAsJsonAsync($"/schools/1/attendance/{date}", entries);

    private async Task<List<StudentAttendanceItem>> History(int studentId) =>
        (await _client.GetFromJsonAsync<List<StudentAttendanceItem>>($"/students/{studentId}/attendance"))!;

    private async Task<ChronicAbsenteeismResult> Chronic(int studentId) =>
        (await _client.GetFromJsonAsync<ChronicAbsenteeismResult>($"/students/{studentId}/chronic-absenteeism"))!;

    private Task<int> SetThreshold(int threshold) => _factory.WithDbAsync(async db =>
    {
        (await db.Schools.FindAsync(1))!.AbsenceAlertThreshold = threshold;
        return await db.SaveChangesAsync();
    });

    [Fact]
    public async Task Save_three_students_then_history_returns_rows_with_descriptions()
    {
        var res = await Save("2026-03-16", E(1, "P"), E(2, "A"), E(3, "T", late: 7, notes: "bus"));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var h1 = Assert.Single(await History(1));
        Assert.Equal("Present", h1.Description);
        var h2 = Assert.Single(await History(2));
        Assert.Equal(("Absent", true, false), (h2.Description, h2.IsAbsent, h2.IsExcused));
        var h3 = Assert.Single(await History(3));
        Assert.Equal(("Tardy", 7, "bus"), (h3.Description, h3.MinutesLate, h3.Notes));
    }

    [Fact]
    public async Task Resaving_same_day_updates_instead_of_duplicating()
    {
        await Save("2026-03-16", E(1, "P"));
        await Save("2026-03-16", E(1, "AE"));

        var row = Assert.Single(await History(1));
        Assert.Equal("AE", row.Code);
        Assert.True(row.IsAbsent);
        Assert.True(row.IsExcused);
    }

    [Fact]
    public async Task Student_without_row_gets_new_row_and_existing_row_is_untouched()
    {
        await Save("2026-03-16", E(1, "A", notes: "original"));
        var before = await _factory.WithDbAsync(db => db.Records.AsNoTracking().SingleAsync(r => r.StudentId == 1));

        await Save("2026-03-16", E(2, "P"));

        var a = await _factory.WithDbAsync(db => db.Records.AsNoTracking().SingleAsync(r => r.StudentId == 1));
        Assert.Equal(("A", "original", true), (a.Code, a.Notes, a.IsAbsent));
        Assert.Equal(before.Id, a.Id);
        Assert.Null(a.ModifiedAt);
        var b = Assert.Single(await History(2));
        Assert.Equal("P", b.Code);
        Assert.Equal(2, await _factory.WithDbAsync(db => db.Records.CountAsync()));
    }

    [Theory]
    [InlineData("ZZ")]
    [InlineData("X")]
    public async Task Unknown_or_inactive_code_rejects_whole_batch(string badCode)
    {
        var res = await Save("2026-03-16", E(1, "P"), E(2, badCode));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Records.CountAsync()));
    }

    [Fact]
    public async Task Reaching_threshold_is_chronic_and_keeps_exactly_one_open_alert()
    {
        await SetThreshold(3);
        await Save("2026-03-09", E(1, "A"));
        await Save("2026-03-10", E(1, "AE"));
        Assert.False((await Chronic(1)).IsChronic);
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Alerts.CountAsync()));

        await Save("2026-03-11", E(1, "A"));
        var at = await Chronic(1);
        Assert.True(at.IsChronic);
        Assert.Equal((3, 3, "2025-2026"), (at.Absences, at.Threshold, at.SchoolYear));

        await Save("2026-03-12", E(1, "A"));
        await Save("2026-03-12", E(1, "A"));
        Assert.Equal(4, (await Chronic(1)).Absences);
        var alerts = await _factory.WithDbAsync(db => db.Alerts.AsNoTracking().ToListAsync());
        var alert = Assert.Single(alerts);
        Assert.Equal(StudentAlert.ChronicAbsence, alert.AlertType);
        Assert.Equal("2025-2026", alert.SchoolYear);
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public async Task Default_threshold_is_ten()
    {
        Assert.Equal(10, (await Chronic(1)).Threshold);
    }

    [Fact]
    public async Task Absences_from_previous_school_year_do_not_count()
    {
        await SetThreshold(3);
        await Save("2025-08-29", E(1, "A"));
        await Save("2025-08-28", E(1, "A"));
        await Save("2025-08-27", E(1, "A"));
        await Save("2025-09-01", E(1, "A"));

        var current = await Chronic(1);
        Assert.Equal(1, current.Absences);
        Assert.False(current.IsChronic);
        Assert.Single(await History(1));

        var previous = await _client.GetFromJsonAsync<ChronicAbsenteeismResult>("/students/1/chronic-absenteeism?schoolYear=2024-2025");
        Assert.Equal(3, previous!.Absences);
        Assert.True(previous.IsChronic);
    }

    [Fact]
    public async Task Future_date_and_ineligible_student_are_rejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Save("2026-03-17", E(1, "P"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save("2026-03-16", E(4, "P"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save("2026-03-16", E(99, "P"))).StatusCode);
    }
}
