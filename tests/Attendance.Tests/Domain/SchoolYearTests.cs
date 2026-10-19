using Attendance.Api.Domain;

namespace Attendance.Tests.Domain;

public class SchoolYearTests
{
    [Fact]
    public void Aug31_belongs_to_previous_school_year()
    {
        Assert.Equal(new SchoolYear(2024), SchoolYear.For(new DateOnly(2025, 8, 31)));
    }

    [Fact]
    public void Sep1_starts_new_school_year()
    {
        Assert.Equal(new SchoolYear(2025), SchoolYear.For(new DateOnly(2025, 9, 1)));
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 1, 31)]
    public void January_belongs_to_year_that_started_previous_September(int y, int m, int d)
    {
        Assert.Equal(new SchoolYear(2025), SchoolYear.For(new DateOnly(y, m, d)));
    }

    [Fact]
    public void Formats_as_start_dash_end()
    {
        Assert.Equal("2025-2026", new SchoolYear(2025).ToString());
    }

    [Fact]
    public void Spans_Sep1_to_Aug31()
    {
        var year = new SchoolYear(2025);
        Assert.Equal(new DateOnly(2025, 9, 1), year.Start);
        Assert.Equal(new DateOnly(2026, 8, 31), year.End);
    }
}
