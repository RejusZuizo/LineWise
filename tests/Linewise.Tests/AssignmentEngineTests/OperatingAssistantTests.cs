using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

/// <summary>
/// The second in charge on a line: how many, chosen from whom, and what happens when there
/// is nobody to choose.
/// </summary>
public sealed class OperatingAssistantTests
{
    /// <summary>
    /// The ordinary case, and the one that matters most. A site that does not use the role
    /// must behave exactly as it did before the role existed.
    /// </summary>
    [Fact]
    public void ALineAskingForNoneGetsNone()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .CanAssist("Ada Fictional", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.DoesNotContain(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.OperatingAssistant);
    }

    [Fact]
    public void ALineAskingForOneGetsOneFromThosePermittedToAssistIt()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3, operatingAssistants: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .CanAssist("Bram Invented", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var assistant = Assert.Single(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.OperatingAssistant);

        Assert.Equal(scenario.EmployeeId("Bram Invented"), assistant.EmployeeId);
    }

    [Fact]
    public void ALineAskingForTwoGetsTwo()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 4, operatingAssistants: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .CanAssist("Ada Fictional", "Pastry")
            .CanAssist("Bram Invented", "Pastry")
            .CanAssist("Cleo Notreal", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(
            2,
            day.Assignments.Count(assignment =>
                assignment.Role == AssignmentRole.OperatingAssistant));
    }

    /// <summary>
    /// Nobody permitted to assist is a warning, not a crash and not a quiet substitution.
    /// The engine has never thrown for a business rule failure and does not start here.
    /// </summary>
    [Fact]
    public void NobodyPermittedToAssistWarnsRatherThanFillingTheSlotAnyway()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3, operatingAssistants: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.DoesNotContain(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.OperatingAssistant);

        Assert.Contains(
            day.Warnings,
            warning => warning.Code == WarningCode.LineShortOfOperatingAssistants);
    }

    /// <summary>
    /// One person cannot hold two ranks. Promoting the leader a second time would quietly
    /// cost the line the leader it had already found, which is the failure this guards.
    /// </summary>
    [Fact]
    public void TheLeaderIsNotAlsoMadeTheAssistant()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3, operatingAssistants: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .CanLead("Ada Fictional", "Pastry")
            .CanAssist("Ada Fictional", "Pastry")
            .CanAssist("Bram Invented", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var leader = Assert.Single(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.LineLeader);

        var assistant = Assert.Single(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.OperatingAssistant);

        Assert.NotEqual(leader.EmployeeId, assistant.EmployeeId);
    }

    /// <summary>
    /// The assistant comes out of the line's headcount rather than adding to it, exactly as
    /// a leader does. A line of three with an assistant is three people, not four.
    /// </summary>
    [Fact]
    public void AnAssistantComesOutOfTheHeadcountRatherThanOnTopOfIt()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3, operatingAssistants: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Employee("Eli Pretend")
            .CanAssist("Ada Fictional", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(3, day.Assignments.Count);
    }

    /// <summary>
    /// A line the person is blocked from, or lacks a skill for, is still off limits. The
    /// rank does not buy a way past the absolute constraints.
    /// </summary>
    [Fact]
    public void BeingPermittedToAssistDoesNotOverrideABlockedLine()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2, operatingAssistants: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .CanAssist("Ada Fictional", "Pastry")
            .BlockedFrom("Ada Fictional", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.DoesNotContain(
            day.Assignments,
            assignment => assignment.EmployeeId == scenario.EmployeeId("Ada Fictional"));
    }
}
