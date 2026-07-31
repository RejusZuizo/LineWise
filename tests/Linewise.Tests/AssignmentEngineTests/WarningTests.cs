using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class WarningTests
{
    [Fact]
    public void AnUnderstaffedLineWarnsRatherThanThrowing()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 5)
            .Employee("Ada Fictional");

        var week = TestEngine.Default().Generate(scenario.Build());

        var warning = Assert.Single(week.Days[0].Coded(WarningCode.LineUnderHeadcount));
        Assert.Equal(WarningSeverity.Error, warning.Severity);
        Assert.Equal(scenario.LineId("Pastry"), warning.LineId);
        Assert.Equal(scenario.DateFor(0), warning.Date);
    }

    [Fact]
    public void EverybodyAvailableAndUnplacedIsReported()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var unassigned = day.Coded(WarningCode.EmployeeUnassigned).ToList();
        Assert.Equal(2, unassigned.Count);
        Assert.All(unassigned, warning => Assert.Equal(WarningSeverity.Notice, warning.Severity));
        Assert.All(unassigned, warning => Assert.NotNull(warning.EmployeeId));
    }

    [Fact]
    public void WarningsCarryNoEmployeeNames()
    {
        // Log hygiene, enforced at the point the text is built rather than at the point it
        // is written out. A warning that leaks into a log file must not carry personal data.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented");

        var week = TestEngine.Default().Generate(scenario.Build());

        Assert.All(
            week.AllWarnings,
            warning => Assert.DoesNotContain("Fictional", warning.Message, StringComparison.Ordinal));
        Assert.All(
            week.AllWarnings,
            warning => Assert.DoesNotContain("Invented", warning.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryWarningCarriesReadableText()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 4)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .BlockedFrom("Bram Invented", "Pastry");

        var week = TestEngine.Default().Generate(scenario.Build());

        Assert.NotEmpty(week.AllWarnings);
        Assert.All(week.AllWarnings, warning => Assert.False(string.IsNullOrWhiteSpace(warning.Message)));

        // The message came from the resource file, not from the enum name falling through.
        var underHeadcount = Assert.Single(week.Days[0].Coded(WarningCode.LineUnderHeadcount));
        Assert.Contains("Pastry", underHeadcount.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWeekOfImpossibleRulesStillProducesARoster()
    {
        var scenario = new RosterScenarioBuilder()
            .Line("Ovens", headcount: 4)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .BlockedFrom("Ada Fictional", "Ovens")
            .MustWork("Bram Invented", "Ovens");

        var week = TestEngine.Default().Generate(scenario.Build());

        Assert.Equal(5, week.Days.Count);
        Assert.NotEmpty(week.AllWarnings);
    }
}
