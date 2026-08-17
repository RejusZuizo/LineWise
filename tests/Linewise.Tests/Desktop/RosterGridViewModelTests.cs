using Linewise.Desktop.ViewModels;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>
/// The mapping from a generated roster to the grid on screen.
/// </summary>
/// <remarks>
/// No control is constructed and no UI thread is needed, which is the practical test of
/// whether the view models are doing the work rather than the views.
/// </remarks>
public sealed class RosterGridViewModelTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);

    private static readonly Guid Ovens = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Packing = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid Ada = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bram = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Cleo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    [Fact]
    public void Lines_appear_in_their_configured_order()
    {
        // Declared packing first, but it displays second.
        var grid = Build(
            lines: [Line(Packing, "Packing", order: 2), Line(Ovens, "Ovens", order: 1)],
            assignments: []);

        Assert.Equal(["Ovens", "Packing"], grid.Rows.Select(row => row.Name));
    }

    [Fact]
    public void There_is_one_column_per_day_the_roster_covers()
    {
        var grid = Build(days: 3, assignments: []);

        Assert.Equal(3, grid.DayCount);
        Assert.Equal(3, grid.Rows[0].Cells.Count);
        Assert.Equal([Monday, Monday.AddDays(1), Monday.AddDays(2)], grid.Dates);
    }

    /// <summary>
    /// The leader is read first on the printed sheet, so the same order holds on screen.
    /// Somebody holding the paper and somebody at the keyboard are reading one list.
    /// </summary>
    [Fact]
    public void The_leader_is_listed_first_and_the_rest_alphabetically()
    {
        var grid = Build(assignments:
        [
            Assign(Cleo, Ovens, AssignmentRole.Worker),
            Assign(Bram, Ovens, AssignmentRole.Worker),
            Assign(Ada, Ovens, AssignmentRole.LineLeader),
        ]);

        var people = grid.Rows[0].Cells[0].People;

        Assert.Equal(["Ada Fictional", "Bram Invented", "Cleo Notreal"], people.Select(p => p.DisplayName));
        Assert.True(people[0].IsLeader);
    }

    [Fact]
    public void A_leader_is_marked_three_ways()
    {
        var grid = Build(assignments: [Assign(Ada, Ovens, AssignmentRole.LineLeader)]);

        var leader = grid.Rows[0].Cells[0].People[0];

        // The flag drives the block and the weight; the word stands on its own.
        Assert.True(leader.IsLeader);
        Assert.Equal("Leader", leader.RoleLabel);
    }

    [Fact]
    public void Headcount_says_both_numbers()
    {
        var grid = Build(
            lines: [Line(Ovens, "Ovens", order: 1, required: 4)],
            assignments: [Assign(Ada, Ovens, AssignmentRole.LineLeader), Assign(Bram, Ovens)]);

        var cell = grid.Rows[0].Cells[0];

        Assert.Equal("2 of 4", cell.Headcount);
        Assert.True(cell.IsShort);
    }

    [Fact]
    public void A_full_line_is_not_short()
    {
        var grid = Build(
            lines: [Line(Ovens, "Ovens", order: 1, required: 2)],
            assignments: [Assign(Ada, Ovens, AssignmentRole.LineLeader), Assign(Bram, Ovens)]);

        Assert.False(grid.Rows[0].Cells[0].IsShort);
        Assert.Equal(0, grid.Rows[0].ShortDays);
    }

    /// <summary>
    /// A line with people but nobody leading is a different problem from a short line, and
    /// the engine warns about it separately.
    /// </summary>
    [Fact]
    public void A_line_with_nobody_leading_says_so()
    {
        var grid = Build(assignments: [Assign(Ada, Ovens), Assign(Bram, Ovens)]);

        Assert.True(grid.Rows[0].Cells[0].HasNoLeader);
    }

    [Fact]
    public void An_empty_cell_is_not_reported_as_leaderless()
    {
        var grid = Build(assignments: []);

        var cell = grid.Rows[0].Cells[0];

        Assert.True(cell.IsEmpty);
        Assert.False(cell.HasNoLeader);
    }

    [Fact]
    public void Days_short_are_counted_across_the_week()
    {
        var grid = Build(
            days: 3,
            lines: [Line(Ovens, "Ovens", order: 1, required: 2)],
            assignments:
            [
                Assign(Ada, Ovens, AssignmentRole.LineLeader),
                Assign(Bram, Ovens),
                Assign(Ada, Ovens, AssignmentRole.LineLeader, dayOffset: 1),
            ]);

        // Monday is full, Tuesday is one short, Wednesday is empty.
        Assert.Equal(2, grid.Rows[0].ShortDays);
        Assert.True(grid.Rows[0].IsShortAnyDay);
    }

    /// <summary>
    /// Should never happen. Drawing a blank chip instead would look like a rendering fault
    /// and get investigated as one.
    /// </summary>
    [Fact]
    public void An_assignment_with_no_matching_employee_is_labelled_rather_than_blank()
    {
        var grid = Build(assignments: [Assign(Guid.NewGuid(), Ovens)]);

        Assert.Equal(
            RosterGridViewModel.UnknownEmployee,
            grid.Rows[0].Cells[0].People[0].DisplayName);
    }

    [Fact]
    public void A_manual_placement_is_distinguishable()
    {
        var grid = Build(assignments:
        [
            Assign(Ada, Ovens) with { Source = AssignmentSource.Manual, IsLocked = true },
        ]);

        var chip = grid.Rows[0].Cells[0].People[0];

        Assert.True(chip.IsManual);
        Assert.True(chip.IsLocked);
    }

    [Fact]
    public void A_roster_with_no_lines_has_nothing_to_draw()
    {
        var grid = Build(lines: [], assignments: []);

        Assert.True(grid.IsEmpty);
    }

    [Theory]
    [InlineData(2026, 8, 3)]   // Monday itself
    [InlineData(2026, 8, 6)]   // Thursday
    [InlineData(2026, 8, 9)]   // Sunday, the one a culture-dependent answer gets wrong
    public void Any_day_resolves_to_its_monday(int year, int month, int day)
    {
        Assert.Equal(Monday, MainWindowViewModel.MondayOf(new DateOnly(year, month, day)));
    }

    /// <summary>
    /// One day rather than seven. The view somebody wants at the start of a shift, and the
    /// reason the grid does not always have to be a week.
    /// </summary>
    [Fact]
    public void A_single_day_shows_only_that_day()
    {
        var grid = Build(days: 5, assignments: [Assign(Ada, Ovens)], onlyDate: Monday.AddDays(2));

        Assert.True(grid.IsSingleDay);
        Assert.Equal([Monday.AddDays(2)], grid.Dates);
        Assert.Single(grid.Rows[0].Cells);
    }

    [Fact]
    public void A_single_day_keeps_the_people_who_are_on_that_day()
    {
        var grid = Build(
            days: 3,
            assignments: [Assign(Ada, Ovens), Assign(Bram, Ovens, dayOffset: 1)],
            onlyDate: Monday.AddDays(1));

        var cell = Assert.Single(grid.Rows[0].Cells);

        Assert.Equal("Bram Invented", Assert.Single(cell.People).DisplayName);
    }

    /// <summary>
    /// A single day headed "week beginning Monday" is a screen that lies about its contents.
    /// </summary>
    [Fact]
    public void A_single_day_is_headed_with_that_day()
    {
        var week = Build(days: 5, assignments: []);
        var day = Build(days: 5, assignments: [], onlyDate: Monday);

        Assert.Contains("Week beginning", week.WeekLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("Week beginning", day.WeekLabel, StringComparison.Ordinal);
    }

    private static RosterGridViewModel Build(
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<ProductionLine>? lines = null,
        int days = 1,
        DateOnly? onlyDate = null)
    {
        lines ??= [Line(Ovens, "Ovens", order: 1)];

        var roster = new RosterWeek
        {
            WeekStart = Monday,
            Days = Enumerable.Range(0, days)
                .Select(offset => new RosterDay
                {
                    Date = Monday.AddDays(offset),
                    ShiftId = Guid.Empty,
                    Assignments = assignments.Where(a => a.Date == Monday.AddDays(offset)).ToList(),
                })
                .ToList(),
        };

        return new RosterGridViewModel(roster, lines, Employees, onlyDate);
    }

    private static ProductionLine Line(Guid id, string name, int order, int required = 3) =>
        new() { Id = id, Name = name, DisplayOrder = order, RequiredHeadcount = required };

    private static Assignment Assign(
        Guid employeeId,
        Guid lineId,
        AssignmentRole role = AssignmentRole.Worker,
        int dayOffset = 0) =>
        new()
        {
            Date = Monday.AddDays(dayOffset),
            ShiftId = Guid.Empty,
            LineId = lineId,
            EmployeeId = employeeId,
            Role = role,
        };

    private static readonly IReadOnlyList<Employee> Employees =
    [
        new() { Id = Ada, FullName = "Ada Fictional" },
        new() { Id = Bram, FullName = "Bram Invented" },
        new() { Id = Cleo, FullName = "Cleo Notreal" },
    ];
}
