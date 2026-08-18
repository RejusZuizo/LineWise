using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Xunit;

namespace Linewise.Tests.Rostering;

/// <summary>
/// The one answer to "is this person actually going to be here", shared by the grid, the
/// counts and the printed sheet so the three cannot drift apart.
/// </summary>
public sealed class AttendanceTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);
    private static readonly Guid Ada = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(AvailabilityStatus.Off, true)]
    [InlineData(AvailabilityStatus.Holiday, true)]
    [InlineData(AvailabilityStatus.Working, false)]
    [InlineData(AvailabilityStatus.Overtime, false)]
    public void A_status_decides_whether_somebody_is_absent(AvailabilityStatus status, bool expected)
    {
        var attendance = new Attendance([Record(status)]);

        Assert.Equal(expected, attendance.IsAbsent(Ada, Monday));
    }

    /// <summary>
    /// The rule that differs from the engine's, and the one worth stating twice. The engine
    /// asks "may I place this person", where silence has to mean no. This asks "was the
    /// roster's decision overturned", where silence means nothing happened.
    /// </summary>
    [Fact]
    public void Silence_is_not_absence()
    {
        var attendance = new Attendance([]);

        // Reading it the other way would grey out every name in a week whose sheet was
        // never imported, and print a wall sheet with nobody on it.
        Assert.False(attendance.IsAbsent(Ada, Monday));
    }

    [Fact]
    public void An_absence_belongs_to_one_day()
    {
        var attendance = new Attendance([Record(AvailabilityStatus.Off)]);

        Assert.True(attendance.IsAbsent(Ada, Monday));
        Assert.False(attendance.IsAbsent(Ada, Monday.AddDays(1)));
    }

    [Fact]
    public void Nobody_is_absent_when_there_is_no_availability_to_hand()
    {
        Assert.False(Attendance.Everybody.IsAbsent(Ada, Monday));
    }

    [Fact]
    public void An_assignment_is_read_by_its_person_and_its_date()
    {
        var attendance = new Attendance([Record(AvailabilityStatus.Holiday)]);

        var assignment = new Assignment
        {
            Date = Monday,
            ShiftId = Guid.Empty,
            LineId = Guid.Empty,
            EmployeeId = Ada,
            Role = AssignmentRole.Worker,
        };

        Assert.True(attendance.IsAbsent(assignment));
    }

    private static Availability Record(AvailabilityStatus status) => new()
    {
        EmployeeId = Ada,
        Date = Monday,
        Status = status,
    };
}
