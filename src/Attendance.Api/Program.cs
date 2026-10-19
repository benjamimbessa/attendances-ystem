using Attendance.Api.Data;
using Attendance.Api.Features.Attendance;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AttendanceDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Attendance") ?? "Data Source=attendance.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.SeedAsync();
}

app.UseExceptionHandler();
app.MapSaveDailyAttendance();
app.MapStudentAttendance();

app.Run();

public partial class Program;
