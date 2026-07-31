using Linewise.Application.Validation;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Validation;

public sealed class RosterRuleValidatorTests
{
    private readonly IRosterRuleValidator _validator = new RosterRuleValidator();

    [Fact]
    public void AWorkableConfigurationRaisesNothing()
    {
        var configuration = Workable().BuildConfiguration();

        Assert.Empty(_validator.Validate(configuration));
    }

    [Fact]
    public void TwoEmployeesRequiredOnASingleSlotLineIsImpossible()
    {
        var scenario = Workable()
            .Line("Sorting", headcount: 1)
            .CanLead("Ada Fictional", "Sorting")
            .MustWork("Cleo Notreal", "Sorting", rank: 2)
            .MustWork("Dara Madeup", "Sorting", rank: 2);

        var warning = Single(scenario, WarningCode.TooManyMandatoryPreferencesForLine);

        Assert.Equal(WarningSeverity.Error, warning.Severity);
        Assert.Equal(scenario.LineId("Sorting"), warning.LineId);
    }

    [Fact]
    public void AnEmployeeBlockedFromEveryLineIsImpossible()
    {
        var scenario = Workable()
            .BlockedFrom("Cleo Notreal", "Pastry")
            .BlockedFrom("Cleo Notreal", "Packing");

        var warning = Single(scenario, WarningCode.EmployeeBlockedFromEveryLine);

        Assert.Equal(scenario.EmployeeId("Cleo Notreal"), warning.EmployeeId);
    }

    [Fact]
    public void ALineRequiringASkillNobodyHoldsIsImpossible()
    {
        var scenario = Workable()
            .Line("Ovens", headcount: 2)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .CanLead("Ada Fictional", "Ovens");

        var warning = Single(scenario, WarningCode.LineRequiresSkillNobodyHolds);

        Assert.Equal(scenario.LineId("Ovens"), warning.LineId);
        Assert.Contains("Oven ticket", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALineNobodyCanLeadIsImpossible()
    {
        var scenario = Workable().Line("Sorting", headcount: 2);

        var warning = Single(scenario, WarningCode.LineHasNoEligibleLeader);

        Assert.Equal(scenario.LineId("Sorting"), warning.LineId);
    }

    [Fact]
    public void ALineWhoseOnlyLeaderIsBlockedFromItIsImpossible()
    {
        var scenario = Workable()
            .Line("Sorting", headcount: 2)
            .CanLead("Cleo Notreal", "Sorting")
            .BlockedFrom("Cleo Notreal", "Sorting");

        Assert.Contains(Validate(scenario), warning => warning.Code == WarningCode.LineHasNoEligibleLeader);
    }

    [Fact]
    public void BeingRequiredOnAndBlockedFromTheSameLineIsContradictory()
    {
        var scenario = Workable()
            .MustWork("Cleo Notreal", "Packing", rank: 2)
            .BlockedFrom("Cleo Notreal", "Packing");

        var warning = Single(scenario, WarningCode.ContradictoryPreference);

        Assert.Equal(scenario.EmployeeId("Cleo Notreal"), warning.EmployeeId);
        Assert.Equal(scenario.LineId("Packing"), warning.LineId);
    }

    [Fact]
    public void BeingRequiredOnALineWithoutItsSkillsIsImpossible()
    {
        var scenario = Workable()
            .Line("Ovens", headcount: 2)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .EmployeeHasSkill("Ada Fictional", "Oven ticket")
            .CanLead("Ada Fictional", "Ovens")
            .MustWork("Cleo Notreal", "Ovens", rank: 2);

        var warning = Single(scenario, WarningCode.MandatoryPreferenceWithoutRequiredSkills);

        Assert.Equal(scenario.EmployeeId("Cleo Notreal"), warning.EmployeeId);
    }

    [Fact]
    public void TwoLinesAtTheSameRankIsReported()
    {
        var scenario = Workable()
            .Prefers("Cleo Notreal", "Pastry", rank: 1)
            .Prefers("Cleo Notreal", "Packing", rank: 1);

        var warning = Single(scenario, WarningCode.DuplicatePreferenceRank);

        Assert.Equal(WarningSeverity.Notice, warning.Severity);
        Assert.Equal(scenario.EmployeeId("Cleo Notreal"), warning.EmployeeId);
    }

    [Fact]
    public void ALineWithNoPlacesIsReported()
    {
        var scenario = Workable()
            .Line("Sorting", headcount: 0)
            .CanLead("Ada Fictional", "Sorting");

        var warning = Single(scenario, WarningCode.LineHasNoPlaces);

        Assert.Equal(scenario.LineId("Sorting"), warning.LineId);
    }

    [Fact]
    public void BeingRequiredOnTwoLinesIsReported()
    {
        var scenario = Workable()
            .MustWork("Cleo Notreal", "Pastry", rank: 1)
            .MustWork("Cleo Notreal", "Packing", rank: 2);

        var warning = Single(scenario, WarningCode.EmployeeMandatoryOnMultipleLines);

        Assert.Equal(scenario.EmployeeId("Cleo Notreal"), warning.EmployeeId);

        // Reported against the line the engine will actually use.
        Assert.Equal(scenario.LineId("Pastry"), warning.LineId);
    }

    [Fact]
    public void AnInactiveEmployeeIsNotHeldAgainstTheConfiguration()
    {
        var scenario = Workable()
            .Employee("Zed Fictitious")
            .BlockedFrom("Zed Fictitious", "Pastry")
            .BlockedFrom("Zed Fictitious", "Packing")
            .Deactivated("Zed Fictitious");

        Assert.Empty(_validator.Validate(scenario.BuildConfiguration()));
    }

    /// <summary>Two lines, four people, everybody employable and every line leadable.</summary>
    private static RosterScenarioBuilder Workable() =>
        new RosterScenarioBuilder()
            .Line("Pastry", headcount: 3)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .CanLead("Ada Fictional", "Pastry")
            .CanLead("Bram Invented", "Packing");

    private IReadOnlyList<RosterWarning> Validate(RosterScenarioBuilder scenario) =>
        _validator.Validate(scenario.BuildConfiguration());

    private RosterWarning Single(RosterScenarioBuilder scenario, WarningCode code) =>
        Assert.Single(Validate(scenario), warning => warning.Code == code);
}
