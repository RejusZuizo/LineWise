using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

/// <summary>
/// A line that is not running. Nobody is placed on it, nothing is warned about it, and its
/// usual crew are free for the lines that are running.
/// </summary>
public sealed class ClosedLineTests
{
    [Fact]
    public void A_closed_line_takes_nobody()
    {
        var roster = TestEngine.Default().Generate(Scenario().Closed("Ovens", day: 0).Build());

        var ovens = LineId(Scenario().Closed("Ovens", 0), "Ovens");

        Assert.DoesNotContain(roster.AllAssignments, assignment => assignment.LineId == ovens);
    }

    /// <summary>
    /// The point of a flag rather than a headcount of zero. A line that is not running is
    /// not short of people, and saying so would bury the lines that are.
    /// </summary>
    [Fact]
    public void A_closed_line_is_not_reported_as_short_or_leaderless()
    {
        var scenario = Scenario().Closed("Ovens", day: 0);
        var roster = TestEngine.Default().Generate(scenario.Build());
        var ovens = LineId(scenario, "Ovens");

        Assert.DoesNotContain(
            roster.AllWarnings,
            warning => warning.LineId == ovens
                && warning.Code is WarningCode.LineUnderHeadcount or WarningCode.LineHasNoLeader);
    }

    [Fact]
    public void Closing_a_line_frees_its_people_for_the_lines_that_are_running()
    {
        var open = TestEngine.Default().Generate(Scenario().Build());
        var closed = TestEngine.Default().Generate(Scenario().Closed("Ovens", day: 0).Build());

        var packing = LineId(Scenario(), "Packing");

        var before = open.AllAssignments.Count(a => a.LineId == packing);
        var after = closed.AllAssignments.Count(a => a.LineId == packing);

        // Packing was short while Ovens was taking people. With Ovens shut it fills up.
        Assert.True(after > before, $"expected Packing to grow from {before}, got {after}");
    }

    [Fact]
    public void Only_the_day_named_is_closed()
    {
        var scenario = Scenario().Days(2).Closed("Ovens", day: 0);
        var roster = TestEngine.Default().Generate(scenario.Build());
        var ovens = LineId(scenario, "Ovens");

        var monday = RosterScenarioBuilder.DefaultWeekStart;

        Assert.DoesNotContain(
            roster.AllAssignments,
            a => a.LineId == ovens && a.Date == monday);

        Assert.Contains(
            roster.AllAssignments,
            a => a.LineId == ovens && a.Date == monday.AddDays(1));
    }

    /// <summary>
    /// Overtime is routed to lines running above their usual complement. A closed line is
    /// not one of them, whatever number is sitting on its record.
    /// </summary>
    [Fact]
    public void Overtime_is_not_routed_to_a_closed_line()
    {
        var scenario = Scenario()
            .Demand("Ovens", day: 0, headcount: 6)
            .Closed("Ovens", day: 0)
            .Overtime("Ada Fictional", day: 0);

        var roster = TestEngine.Default().Generate(scenario.Build());
        var ovens = LineId(scenario, "Ovens");

        Assert.DoesNotContain(roster.AllAssignments, a => a.LineId == ovens);
    }

    /// <summary>
    /// A lock is the manager overruling the engine and the engine does not argue with them.
    /// It does say that the two decisions contradict each other.
    /// </summary>
    [Fact]
    public void A_lock_on_a_closed_line_is_kept_and_reported()
    {
        var scenario = Scenario()
            .Closed("Ovens", day: 0)
            .Locked("Ada Fictional", "Ovens", day: 0);

        var roster = TestEngine.Default().Generate(scenario.Build());
        var ovens = LineId(scenario, "Ovens");

        Assert.Contains(roster.AllAssignments, a => a.LineId == ovens);

        Assert.Contains(
            roster.AllWarnings,
            warning => warning.Code == WarningCode.LockedAssignmentOnAClosedLine);
    }

    private static RosterScenarioBuilder Scenario() =>
        new RosterScenarioBuilder()
            .Days(1)
            .Line("Ovens", headcount: 3)
            .Line("Packing", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .CanLead("Ada Fictional", "Ovens")
            .CanLead("Bram Invented", "Packing");

    private static Guid LineId(RosterScenarioBuilder scenario, string name) =>
        scenario.Build().Configuration.Lines.Single(line => line.Name == name).Id;
}
