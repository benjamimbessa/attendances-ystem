using Attendance.Api.Domain;

namespace Attendance.Tests.Domain;

public class ChronicAbsencePolicyTests
{
    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void Chronic_when_absences_reach_school_threshold(int absences, bool expected)
    {
        Assert.Equal(expected, ChronicAbsencePolicy.IsChronic(absences, schoolThreshold: 5));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void Uses_default_threshold_of_10_when_school_has_none(int absences, bool expected)
    {
        Assert.Equal(10, ChronicAbsencePolicy.DefaultThreshold);
        Assert.Equal(expected, ChronicAbsencePolicy.IsChronic(absences, schoolThreshold: null));
    }
}
