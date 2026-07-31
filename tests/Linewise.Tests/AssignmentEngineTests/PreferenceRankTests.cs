using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class PreferenceRankTests
{
    [Fact]
    public void ASecondChoiceIsUsedWhenTheFirstChoiceIsFull()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 1)
            .Line("Packing", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .Prefers("Ada Fictional", "Packing", rank: 2)
            .Prefers("Bram Invented", "Pastry", rank: 1)
            .Prefers("Bram Invented", "Packing", rank: 2);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        // Ada takes the single Pastry place on the last tie break, so Bram falls to his second.
        var bram = day.For(scenario.EmployeeId("Bram Invented"));
        Assert.Equal(scenario.LineId("Packing"), bram.LineId);
        Assert.Equal(PlacementRule.PreferenceRank, bram.Explanation.Rule);
        Assert.Equal(2, bram.Explanation.PreferenceRank);
    }

    [Fact]
    public void EveryFirstChoiceIsPlacedBeforeAnySecondChoice()
    {
        // Ada sorts first, and going employee by employee would hand her Pastry as a second
        // choice before Bram's first choice was ever considered. Rank comes first.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 1)
            .Line("Packing", headcount: 1)
            .Line("Prep", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Zed Fictitious")
            .Prefers("Ada Fictional", "Packing", rank: 1)
            .Prefers("Ada Fictional", "Pastry", rank: 2)
            .Prefers("Bram Invented", "Pastry", rank: 1)
            .Locked("Zed Fictitious", "Packing");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var pastry = Assert.Single(day.OnLine(scenario.LineId("Pastry")));
        Assert.Equal(scenario.EmployeeId("Bram Invented"), pastry.EmployeeId);
        Assert.Equal(1, pastry.Explanation.PreferenceRank);

        // Ada's first choice was taken and her second went to a first choice, so she is
        // filled in wherever there is room.
        var ada = day.For(scenario.EmployeeId("Ada Fictional"));
        Assert.Equal(scenario.LineId("Prep"), ada.LineId);
        Assert.Equal(PlacementRule.Backfill, ada.Explanation.Rule);
    }

    [Fact]
    public void APreferenceIsIgnoredWhenTheLineIsBlocked()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .BlockedFrom("Ada Fictional", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(scenario.LineId("Packing"), day.For(scenario.EmployeeId("Ada Fictional")).LineId);
    }
}
