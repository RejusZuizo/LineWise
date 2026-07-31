using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class LeaderSelectionTests
{
    [Fact]
    public void OneLeaderIsChosenPerLineFromThoseEligibleToLeadIt()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .CanLead("Bram Invented", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var leader = Assert.Single(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.LineLeader);
        Assert.Equal(scenario.EmployeeId("Bram Invented"), leader.EmployeeId);
        Assert.Equal(PlacementRule.LeaderSelection, leader.Explanation.Rule);
    }

    [Fact]
    public void LeadershipGoesToWhoeverLedLeastRecently()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .CanLead("Ada Fictional", "Pastry")
            .CanLead("Bram Invented", "Pastry")
            .CanLead("Cleo Notreal", "Pastry")
            .PreviouslyLed("Ada Fictional", "Pastry", daysAgo: 1)
            .PreviouslyLed("Bram Invented", "Pastry", daysAgo: 21)
            .PreviouslyLed("Cleo Notreal", "Pastry", daysAgo: 7);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var leader = Assert.Single(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.LineLeader);
        Assert.Equal(scenario.EmployeeId("Bram Invented"), leader.EmployeeId);
    }

    [Fact]
    public void SomebodyWhoHasNeverLedGoesBeforeAnybodyWhoHas()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .CanLead("Ada Fictional", "Pastry")
            .CanLead("Bram Invented", "Pastry")
            .PreviouslyLed("Ada Fictional", "Pastry", daysAgo: 90);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var leader = Assert.Single(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.LineLeader);
        Assert.Equal(scenario.EmployeeId("Bram Invented"), leader.EmployeeId);
    }

    [Fact]
    public void TheLeaderCountsTowardTheLineHeadcount()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .CanLead("Cleo Notreal", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(2, day.OnLine(scenario.LineId("Pastry")).Count());
        Assert.Single(day.Coded(WarningCode.EmployeeUnassigned));
        Assert.Empty(day.Coded(WarningCode.LineUnderHeadcount));
    }

    [Fact]
    public void SomebodyAlreadyOnTheLineIsPromotedRatherThanTakingAnExtraPlace()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .MustWork("Ada Fictional", "Pastry")
            .CanLead("Ada Fictional", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var ada = day.For(scenario.EmployeeId("Ada Fictional"));
        Assert.Equal(AssignmentRole.LineLeader, ada.Role);

        // Promoted in place, so the answer to "why is she on Pastry" is still the rule that
        // put her there.
        Assert.Equal(PlacementRule.MandatoryPreference, ada.Explanation.Rule);
    }

    [Fact]
    public void ALineWithNobodyEligibleToLeadItWarns()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var warning = Assert.Single(day.Coded(WarningCode.LineHasNoLeader));
        Assert.Equal(WarningSeverity.Error, warning.Severity);
        Assert.Equal(scenario.LineId("Pastry"), warning.LineId);
    }
}
