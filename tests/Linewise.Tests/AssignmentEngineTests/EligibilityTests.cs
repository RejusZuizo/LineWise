using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class EligibilityTests
{
    [Fact]
    public void ABlockedLineIsNeverAssignedEvenWhenTheLineIsShort()
    {
        // Two thirds empty and two people standing idle, and the answer is still no.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .BlockedFrom("Bram Invented", "Pastry")
            .BlockedFrom("Cleo Notreal", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var placed = Assert.Single(day.Assignments);
        Assert.Equal(scenario.EmployeeId("Ada Fictional"), placed.EmployeeId);
        Assert.Single(day.Coded(WarningCode.LineUnderHeadcount));
        Assert.Equal(2, day.Coded(WarningCode.EmployeeUnassigned).Count());
    }

    [Fact]
    public void ASkillRequirementIsNeverViolated()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Ovens", headcount: 3)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .EmployeeHasSkill("Ada Fictional", "Oven ticket");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var placed = Assert.Single(day.Assignments);
        Assert.Equal(scenario.EmployeeId("Ada Fictional"), placed.EmployeeId);
    }

    [Fact]
    public void EverySkillALineRequiresHasToBeHeld()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Ovens", headcount: 2)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .LineRequiresSkill("Ovens", "Allergen handling")
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .EmployeeHasSkill("Ada Fictional", "Oven ticket")
            .EmployeeHasSkill("Ada Fictional", "Allergen handling")
            .EmployeeHasSkill("Bram Invented", "Oven ticket");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var placed = Assert.Single(day.Assignments);
        Assert.Equal(scenario.EmployeeId("Ada Fictional"), placed.EmployeeId);
    }

    [Fact]
    public void AnInactiveEmployeeIsNeverPlaced()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Deactivated("Bram Invented");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.False(day.Has(scenario.EmployeeId("Bram Invented")));
    }

    [Fact]
    public void AnEmployeeWithNoAvailabilityRecordIsTreatedAsOff()
    {
        // Silence on the sheet is never read as availability.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .NoAvailabilityRecordFor("Bram Invented");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.False(day.Has(scenario.EmployeeId("Bram Invented")));
        Assert.Empty(day.Coded(WarningCode.EmployeeUnassigned));
    }

    [Fact]
    public void NobodyIsRosteredTwiceOnTheSameDay()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(
            day.Assignments.Count,
            day.Assignments.Select(assignment => assignment.EmployeeId).Distinct().Count());
    }
}
