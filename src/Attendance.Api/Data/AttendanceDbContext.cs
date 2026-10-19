using Attendance.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Data;

public class AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : DbContext(options)
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<AttendanceCode> Codes => Set<AttendanceCode>();
    public DbSet<AttendanceRecord> Records => Set<AttendanceRecord>();
    public DbSet<StudentAlert> Alerts => Set<StudentAlert>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AttendanceCode>().HasIndex(c => c.Value).IsUnique();
        b.Entity<AttendanceCode>().Property(c => c.Value).HasMaxLength(5);
        b.Entity<AttendanceRecord>().HasIndex(r => new { r.StudentId, r.Date }).IsUnique();
        b.Entity<AttendanceRecord>().Property(r => r.Notes).HasMaxLength(500);
        b.Entity<StudentAlert>().HasIndex(a => new { a.StudentId, a.AlertType, a.SchoolYear });
    }

    public async Task SeedAsync()
    {
        if (await Schools.AnyAsync()) return;
        Schools.Add(new School { Id = 1, Name = "Riverside Elementary" });
        Codes.AddRange(
            new AttendanceCode { Value = "P", Description = "Present" },
            new AttendanceCode { Value = "A", Description = "Absent", IsAbsent = true },
            new AttendanceCode { Value = "AE", Description = "Absent - Excused", IsAbsent = true, IsExcused = true },
            new AttendanceCode { Value = "T", Description = "Tardy" },
            new AttendanceCode { Value = "X", Description = "Retired code", IsActive = false });
        Students.AddRange(
            new Student { Id = 1, SchoolId = 1, FirstName = "Ana", LastName = "Silva" },
            new Student { Id = 2, SchoolId = 1, FirstName = "Bruno", LastName = "Costa" },
            new Student { Id = 3, SchoolId = 1, FirstName = "Carla", LastName = "Souza" },
            new Student { Id = 4, SchoolId = 1, FirstName = "Diego", LastName = "Lima", Active = false });
        await SaveChangesAsync();
    }
}
