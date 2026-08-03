using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class OvertimeRoutingTests
{
    [Fact]
    public void OvertimeGoesToTheBusyLineRatherThanTheirFirstChoice()
    {
        // Overtime is paid because a line has more product to get out. Ada would rather be
        // on Pastry, and on a normal day she would be.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .Demand("Packing", day: 0, headcount: 4)
            .Overtime("Ada Fictional", day: 0);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var ada = day.For(scenario.EmployeeId("Ada Fictional"));
        Assert.Equal(scenario.LineId("Packing"), ada.LineId);
        Assert.Equal(PlacementRule.OvertimeCover, ada.Explanation.Rule);
    }

    [Fact]
    public void WithoutOvertimeTheSamePersonGetsTheirFirstChoice()
    {
        // The control. Raised demand alone does not drag people off their preferences; only
        // being on overtime does.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .Demand("Packing", day: 0, headcount: 4);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(scenario.LineId("Pastry"), day.For(scenario.EmployeeId("Ada Fictional")).LineId);
    }

    [Fact]
    public void ARaisedHeadcountIsFilledBeyondTheLineStandard()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Demand("Packing", day: 0, headcount: 4);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(4, day.OnLine(scenario.LineId("Packing")).Count());
        Assert.Empty(day.Coded(WarningCode.EmployeeUnassigned));
    }

    [Fact]
    public void DemandCanLowerALineForAQuietDay()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 4)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Demand("Packing", day: 0, headcount: 2);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(2, day.OnLine(scenario.LineId("Packing")).Count());

        // Two people spare on a quiet day is not the line being short handed.
        Assert.Empty(day.Coded(WarningCode.LineUnderHeadcount));
        Assert.Equal(2, day.Coded(WarningCode.EmployeeUnassigned).Count());
    }

    [Fact]
    public void RoutingIsAPreferenceAndNotARestriction()
    {
        // Ada is on overtime, but she is blocked from the busy line. Rule 8 is absolute, so
        // she falls through to the ordinary rules rather than being left standing about.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .BlockedFrom("Ada Fictional", "Packing")
            .Demand("Packing", day: 0, headcount: 4)
            .Overtime("Ada Fictional", day: 0);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(scenario.LineId("Pastry"), day.For(scenario.EmployeeId("Ada Fictional")).LineId);
        Assert.Equal(PlacementRule.PreferenceRank, day.For(scenario.EmployeeId("Ada Fictional")).Explanation.Rule);
    }

    [Fact]
    public void OvertimeOnAQuietLineIsReported()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Overtime("Ada Fictional", day: 0);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        // Nothing wrong with it, and not worth undoing, but the manager is paying a premium
        // for somebody standing on a line that is not busy.
        var warning = Assert.Single(day.Coded(WarningCode.OvertimeNotOnABusyLine));
        Assert.Equal(WarningSeverity.Notice, warning.Severity);
        Assert.Equal(scenario.EmployeeId("Ada Fictional"), warning.EmployeeId);
    }

    [Fact]
    public void OvertimeOnABusyLineIsNotReported()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Demand("Packing", day: 0, headcount: 2)
            .Overtime("Ada Fictional", day: 0);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Empty(day.Coded(WarningCode.OvertimeNotOnABusyLine));
    }

    [Fact]
    public void ARaisedLineThatDoesNotGetItsExtraPeopleIsReportedSeparately()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Demand("Packing", day: 0, headcount: 5);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        // Three on a line that normally needs two. It will run, which is a different problem
        // from being short handed, so it is said differently.
        var warning = Assert.Single(day.Coded(WarningCode.LineDemandNotCovered));
        Assert.Equal(WarningSeverity.Error, warning.Severity);
        Assert.Empty(day.Coded(WarningCode.LineUnderHeadcount));
    }

    [Fact]
    public void ALineBelowItsOwnStandardIsShortHandedRatherThanUncovered()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 4)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Demand("Packing", day: 0, headcount: 6);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        // Two people on a line that needs four to run at all. The raised demand is the least
        // of the manager's problems here, so only the more serious warning is raised.
        Assert.Single(day.Coded(WarningCode.LineUnderHeadcount));
        Assert.Empty(day.Coded(WarningCode.LineDemandNotCovered));
    }

    [Fact]
    public void DemandOnlyAppliesToTheDayItWasSetFor()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Demand("Packing", day: 0, headcount: 4);

        var week = TestEngine.Default().Generate(scenario.Build());

        Assert.Equal(4, week.Days[0].OnLine(scenario.LineId("Packing")).Count());
        Assert.Equal(2, week.Days[1].OnLine(scenario.LineId("Packing")).Count());
    }
}
