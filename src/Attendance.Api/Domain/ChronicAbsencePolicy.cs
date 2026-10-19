namespace Attendance.Api.Domain;

/// <summary>Chronic = absences (excused + unexcused) in the school year >= school threshold.</summary>
public static class ChronicAbsencePolicy
{
    public const int DefaultThreshold = 10;

    public static bool IsChronic(int absences, int? schoolThreshold) =>
        absences >= (schoolThreshold ?? DefaultThreshold);
}
