namespace Attendance.Api.Domain;

/// <summary>School year running Sep 1 through Aug 31 (legacy rule), formatted "2025-2026".</summary>
public readonly record struct SchoolYear(int StartYear)
{
    private const int StartMonth = 9;

    public static SchoolYear For(DateOnly date) =>
        new(date.Month >= StartMonth ? date.Year : date.Year - 1);

    public static bool TryParse(string? text, out SchoolYear year)
    {
        year = default;
        var parts = text?.Split('-');
        if (parts is not [var a, var b] || !int.TryParse(a, out var start) || !int.TryParse(b, out var end)
            || end != start + 1 || start is < 1900 or > 9998)
            return false;
        year = new SchoolYear(start);
        return true;
    }

    public DateOnly Start => new(StartYear, StartMonth, 1);

    public DateOnly End => Start.AddYears(1).AddDays(-1);

    public override string ToString() => $"{StartYear}-{StartYear + 1}";
}
