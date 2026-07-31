using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class ExplainabilityTests
{
    [Fact]
    public void EveryAssignmentRecordsTheRuleThatMadeIt()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Employee("Eli Pretend")
            .MustWork("Ada Fictional", "Packing")
            .CanLead("Bram Invented", "Pastry")
            .Prefers("Cleo Notreal", "Pastry", rank: 1)
            .Locked("Eli Pretend", "Packing");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.All(day.Assignments, assignment => Assert.NotNull(assignment.Explanation));

        Assert.Equal(
            PlacementRule.MandatoryPreference,
            day.For(scenario.EmployeeId("Ada Fictional")).Explanation.Rule);

        Assert.Equal(
            PlacementRule.LeaderSelection,
            day.For(scenario.EmployeeId("Bram Invented")).Explanation.Rule);

        Assert.Equal(
            PlacementRule.ManualOverride,
            day.For(scenario.EmployeeId("Eli Pretend")).Explanation.Rule);

        var cleo = day.For(scenario.EmployeeId("Cleo Notreal"));
        Assert.Equal(PlacementRule.PreferenceRank, cleo.Explanation.Rule);
        Assert.Equal(1, cleo.Explanation.PreferenceRank);

        Assert.Equal(
            PlacementRule.Backfill,
            day.For(scenario.EmployeeId("Dara Madeup")).Explanation.Rule);
    }

    [Fact]
    public void APlacementWithNoPreferenceBehindItRecordsNoRank()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.All(day.Assignments, assignment => Assert.Null(assignment.Explanation.PreferenceRank));
    }
}
