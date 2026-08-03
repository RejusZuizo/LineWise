using Linewise.Application.Printing;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Printing;

public sealed class RosterDiffTests
{
    [Fact]
    public void AnUnchangedRosterProducesNothingToPrint()
    {
        var roster = Generate(Scenario());

        Assert.Empty(RosterDiff.Between(roster, roster));
    }

    [Fact]
    public void SomebodyRingingInSickComesOutAsRemoved()
    {
        var before = Generate(Scenario());
        var after = Generate(Scenario().Off("Ada Fictional", day: 0));

        var change = Assert.Single(
            RosterDiff.Between(before, after),
            candidate => candidate.EmployeeId == Ids("Ada Fictional") && candidate.Date == Day(0));

        Assert.Equal(RosterChangeKind.Removed, change.Kind);
        Assert.NotNull(change.FromLineId);
        Assert.Null(change.ToLineId);
    }

    [Fact]
    public void BeingMovedToAnotherLineComesOutAsMoved()
    {
        var scenario = Scenario();
        var before = Generate(scenario);

        // The manager drags somebody across by hand, which locks them there.
        var after = Generate(scenario.Locked("Ada Fictional", "Packing", day: 0));

        var change = Assert.Single(
            RosterDiff.Between(before, after),
            candidate => candidate.EmployeeId == Ids("Ada Fictional"));

        Assert.Equal(RosterChangeKind.Moved, change.Kind);
        Assert.Equal(scenario.LineId("Pastry"), change.FromLineId);
        Assert.Equal(scenario.LineId("Packing"), change.ToLineId);
    }

    [Fact]
    public void TakingOverALineComesOutAsARoleChange()
    {
        var scenario = Scenario();
        var before = Generate(scenario);

        var after = Generate(scenario.Locked(
            "Ada Fictional",
            "Pastry",
            day: 0,
            role: AssignmentRole.LineLeader));

        var change = Assert.Single(
            RosterDiff.Between(before, after),
            candidate => candidate.EmployeeId == Ids("Ada Fictional") && candidate.Date == Day(0));

        // The people at the line need to know who to ask, so a change of leader is worth a
        // slip even though nobody moved.
        Assert.Equal(RosterChangeKind.RoleChanged, change.Kind);
        Assert.True(change.NowLeading);
    }

    [Fact]
    public void SomebodyComingBackComesOutAsAdded()
    {
        var before = Generate(Scenario().Off("Ada Fictional", day: 0));
        var after = Generate(Scenario());

        var change = Assert.Single(
            RosterDiff.Between(before, after),
            candidate => candidate.EmployeeId == Ids("Ada Fictional") && candidate.Date == Day(0));

        Assert.Equal(RosterChangeKind.Added, change.Kind);
        Assert.Null(change.FromLineId);
    }

    [Fact]
    public void ChangesComeBackInDayOrder()
    {
        var before = Generate(Scenario());
        var after = Generate(Scenario().Off("Ada Fictional", day: 3).Off("Bram Invented", day: 1));

        var changes = RosterDiff.Between(before, after);

        Assert.NotEmpty(changes);
        Assert.Equal(changes.OrderBy(change => change.Date).Select(change => change.Date), changes.Select(change => change.Date));
    }

    private static Linewise.Domain.Rostering.RosterWeek Generate(RosterScenarioBuilder scenario) =>
        TestEngine.Default().Generate(scenario.Build());

    private static DateOnly Day(int offset) => RosterScenarioBuilder.DefaultWeekStart.AddDays(offset);

    private static Guid Ids(string name) => Scenario().EmployeeId(name);

    /// <summary>
    /// Rebuilt each time so that two rosters can be generated from the same starting point
    /// with one thing different. Identifiers are derived from declaration order, so the same
    /// scenario always produces the same people and lines.
    /// </summary>
    private static RosterScenarioBuilder Scenario() =>
        new RosterScenarioBuilder()
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .CanLead("Bram Invented", "Pastry")
            .CanLead("Cleo Notreal", "Packing");
}
