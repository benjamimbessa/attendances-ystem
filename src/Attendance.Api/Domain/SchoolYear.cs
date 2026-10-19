namespace Attendance.Api.Domain;

/// <summary>School year running Sep 1 through Aug 31 (legacy rule), formatted "2025-2026".</summary>
public readonly record struct SchoolYear(int StartYear)
{
    private const int StartMonth = 9;

    public static SchoolYear For(DateOnly date) =>
        new(date.Month >= StartMonth ? date.Year : date.Year - 1);

    public DateOnly Start => new(StartYear, StartMonth, 1);

    public DateOnly End => Start.AddYears(1).AddDays(-1);

    public override string ToString() => $"{StartYear}-{StartYear + 1}";
}
