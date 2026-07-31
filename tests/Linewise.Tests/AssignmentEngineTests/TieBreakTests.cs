using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class TieBreakTests
{
    [Fact]
    public void ATieGoesToWhoeverHasHadTheirPreferredLineLeastOften()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 1)
            .Line("Packing", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .Prefers("Bram Invented", "Pastry", rank: 1)
            // Ada has had the good line twice already this fortnight.
            .PreviouslyWorked("Ada Fictional", "Pastry", daysAgo: 7)
            .PreviouslyWorked("Ada Fictional", "Pastry", daysAgo: 14);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var pastry = Assert.Single(day.OnLine(scenario.LineId("Pastry")));
        Assert.Equal(scenario.EmployeeId("Bram Invented"), pastry.EmployeeId);
    }

    [Fact]
    public void TheTieBreakStrategyDecidesRatherThanTheEngine()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 1)
            .Line("Packing", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .Prefers("Bram Invented", "Pastry", rank: 1);

        var request = scenario.Build();

        var withFairness = TestEngine.Default().Generate(request).Days[0];
        var withReversed = TestEngine.With(new LastDeclaredFirstTieBreakStrategy()).Generate(request).Days[0];

        // Nothing separates the two on record, so fairness falls through to its final tie
        // break and takes whoever sorts first. The swapped strategy takes the other one.
        Assert.Equal(
            scenario.EmployeeId("Ada Fictional"),
            Assert.Single(withFairness.OnLine(scenario.LineId("Pastry"))).EmployeeId);

        Assert.Equal(
            scenario.EmployeeId("Bram Invented"),
            Assert.Single(withReversed.OnLine(scenario.LineId("Pastry"))).EmployeeId);
    }
}
