using Linewise.Desktop.ViewModels;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>The counts along the bottom of the window, and the state in its title.</summary>
public sealed class RosterSummaryViewModelTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);
    private static readonly Guid Line = Guid.NewGuid();
    private static readonly Guid Ada = Guid.NewGuid();
    private static readonly Guid Bram = Guid.NewGuid();
    private static readonly Guid Cleo = Guid.NewGuid();

    /// <summary>
    /// A person appears once a day. "Eleven placed" has to mean eleven people, not eleven
    /// rows, or the number is meaningless the moment a second shift exists.
    /// </summary>
    [Fact]
    public void People_are_counted_once_per_day_not_once_per_assignment()
    {
        var roster = Roster(
            Assign(Ada, Monday),
            Assign(Ada, Monday),
            Assign(Bram, Monday));

        Assert.Equal(2, Summary(roster).Placed);
    }

    [Fact]
    public void The_same_person_on_two_days_counts_twice()
    {
        var roster = Roster(Assign(Ada, Monday), Assign(Ada, Monday.AddDays(1)));

        Assert.Equal(2, Summary(roster).Placed);
    }

    /// <summary>
    /// Available and standing about. The engine warns for each of these individually; the
    /// number makes it visible without reading the strip.
    /// </summary>
    [Fact]
    public void Available_people_who_were_not_placed_are_counted()
    {
        var roster = Roster(Assign(Ada, Monday));

        var summary = Summary(
            roster,
            Available(Ada, Monday, AvailabilityStatus.Working),
            Available(Bram, Monday, AvailabilityStatus.Working),
            Available(Cleo, Monday, AvailabilityStatus.Overtime));

        Assert.Equal(2, summary.Unplaced);
    }

    [Fact]
    public void Somebody_off_is_not_counted_as_unplaced()
    {
        var summary = Summary(
            Roster(),
            Available(Ada, Monday, AvailabilityStatus.Off),
            Available(Bram, Monday, AvailabilityStatus.Holiday));

        Assert.Equal(0, summary.Unplaced);
        Assert.Equal(2, summary.Off);
    }

    /// <summary>
    /// Holiday and a blank cell are different things to the manager, and the same thing to
    /// this count. Both mean the person is not in.
    /// </summary>
    [Fact]
    public void Holiday_and_off_are_counted_together()
    {
        var summary = Summary(
            Roster(),
            Available(Ada, Monday, AvailabilityStatus.Off),
            Available(Bram, Monday, AvailabilityStatus.Holiday),
            Available(Cleo, Monday, AvailabilityStatus.Working));

        Assert.Equal(2, summary.Off);
        Assert.Equal(1, summary.Working);
    }

    [Theory]
    [InlineData(0, 0, "No warnings")]
    [InlineData(1, 0, "1 error")]
    [InlineData(2, 0, "2 errors")]
    [InlineData(0, 1, "1 notice")]
    [InlineData(2, 3, "2 errors, 3 notices")]
    public void Errors_and_notices_are_named_separately(int errors, int notices, string expected)
    {
        var roster = Roster() with
        {
            Days =
            [
                new RosterDay
                {
                    Date = Monday,
                    ShiftId = Guid.Empty,
                    Warnings =
                    [
                        .. Enumerable.Repeat(Warning(WarningSeverity.Error), errors),
                        .. Enumerable.Repeat(Warning(WarningSeverity.Notice), notices),
                    ],
                },
            ],
        };

        Assert.Equal(expected, Summary(roster).WarningsLabel);
    }

    /// <summary>
    /// A published roster is on a wall somewhere. Which version is on screen is how a stale
    /// sheet gets noticed, and it is the same number printed in the header.
    /// </summary>
    [Fact]
    public void A_published_roster_says_which_version_it_is()
    {
        var summary = Summary(Roster(), version: Version(RosterStatus.Published, 3));

        Assert.True(summary.IsPublished);
        Assert.Equal("Published, version 3", summary.StatusLabel);
    }

    [Fact]
    public void A_draft_says_it_is_not_published()
    {
        var summary = Summary(Roster(), version: Version(RosterStatus.Draft, 1));

        Assert.False(summary.IsPublished);
        Assert.Equal("Draft, not published", summary.StatusLabel);
    }

    private static RosterSummaryViewModel Summary(
        RosterWeek roster,
        params Availability[] availability) =>
        new(roster, availability, Version(RosterStatus.Draft, 1));

    private static RosterSummaryViewModel Summary(
        RosterWeek roster,
        RosterVersion? version,
        params Availability[] availability) =>
        new(roster, availability, version);

    private static RosterWeek Roster(params Assignment[] assignments) =>
        new()
        {
            WeekStart = Monday,
            Days = assignments
                .GroupBy(assignment => assignment.Date)
                .Select(group => new RosterDay
                {
                    Date = group.Key,
                    ShiftId = Guid.Empty,
                    Assignments = group.ToList(),
                })
                .ToList(),
        };

    private static Assignment Assign(Guid employeeId, DateOnly date) =>
        new()
        {
            Date = date,
            ShiftId = Guid.Empty,
            LineId = Line,
            EmployeeId = employeeId,
            Role = AssignmentRole.Worker,
        };

    private static Availability Available(Guid employeeId, DateOnly date, AvailabilityStatus status) =>
        new() { EmployeeId = employeeId, Date = date, Status = status };

    private static RosterWarning Warning(WarningSeverity severity) =>
        new()
        {
            Severity = severity,
            Code = WarningCode.LineUnderHeadcount,
            Message = "A line is short.",
        };

    private static RosterVersion Version(RosterStatus status, int number) =>
        new()
        {
            Id = Guid.NewGuid(),
            WeekStart = Monday,
            VersionNumber = number,
            Status = status,
            CreatedAtUtc = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc),
        };
}
