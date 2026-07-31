using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class MandatoryPreferenceTests
{
    [Fact]
    public void AMandatoryPreferenceIsHonoured()
    {
        // Without the mandatory preference Ada would be filled onto Pastry, the first line
        // in display order.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .MustWork("Ada Fictional", "Packing");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var ada = day.For(scenario.EmployeeId("Ada Fictional"));
        Assert.Equal(scenario.LineId("Packing"), ada.LineId);
        Assert.Equal(PlacementRule.MandatoryPreference, ada.Explanation.Rule);
    }

    [Fact]
    public void AMandatoryPreferenceIsNotBrokenForSomebodyWorkingANormalDay()
    {
        // Packing has one place and the manager has already locked somebody into it, so
        // Ada's mandatory preference cannot be met.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 1)
            .Line("Pastry", headcount: 2)
            .Employee("Zed Fictitious")
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .MustWork("Ada Fictional", "Packing")
            .Locked("Zed Fictitious", "Packing");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var warning = Assert.Single(day.Coded(WarningCode.MandatoryPreferenceNotHonoured));
        Assert.Equal(WarningSeverity.Error, warning.Severity);
        Assert.Equal(scenario.EmployeeId("Ada Fictional"), warning.EmployeeId);
        Assert.Equal(scenario.LineId("Packing"), warning.LineId);

        Assert.NotEqual(scenario.LineId("Packing"), day.For(scenario.EmployeeId("Ada Fictional")).LineId);
    }

    [Fact]
    public void AMandatoryPreferenceMayBeBrokenWhenTheEmployeeIsOnOvertime()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 1)
            .Line("Pastry", headcount: 2)
            .Employee("Zed Fictitious")
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .MustWork("Ada Fictional", "Packing")
            .Locked("Zed Fictitious", "Packing")
            .Overtime("Ada Fictional", day: 0);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var warning = Assert.Single(day.Coded(WarningCode.MandatoryPreferenceBrokenForOvertime));
        Assert.Equal(WarningSeverity.Notice, warning.Severity);
        Assert.Empty(day.Coded(WarningCode.MandatoryPreferenceNotHonoured));
    }

    [Fact]
    public void AMandatoryPreferenceLosesToASkillRequirement()
    {
        // Rule 7 is absolute. Being required on a line does not qualify anybody to run it.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Ovens", headcount: 2)
            .Line("Packing", headcount: 2)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .EmployeeHasSkill("Bram Invented", "Oven ticket")
            .MustWork("Ada Fictional", "Ovens");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.DoesNotContain(
            day.OnLine(scenario.LineId("Ovens")),
            assignment => assignment.EmployeeId == scenario.EmployeeId("Ada Fictional"));
        Assert.Single(day.Coded(WarningCode.MandatoryPreferenceNotHonoured));
    }

    [Fact]
    public void SomebodyOffThatDayIsNotForcedOntoTheirMandatoryLine()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .MustWork("Ada Fictional", "Packing")
            .Off("Ada Fictional", day: 0);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.False(day.Has(scenario.EmployeeId("Ada Fictional")));
        Assert.Empty(day.Coded(WarningCode.MandatoryPreferenceNotHonoured));
    }
}
