namespace Attendance.Api.Domain;

public class School
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? AbsenceAlertThreshold { get; set; }
}

public class Student
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool Active { get; set; } = true;
}

public class AttendanceCode
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsAbsent { get; set; }
    public bool IsExcused { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AttendanceRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public DateOnly Date { get; set; }
    public string Code { get; set; } = "";
    public bool IsAbsent { get; set; }
    public bool IsExcused { get; set; }
    public int MinutesLate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class StudentAlert
{
    public const string ChronicAbsence = "CHRONIC_ABSENCE";

    public int Id { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public string AlertType { get; set; } = "";
    public string SchoolYear { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? Message { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
