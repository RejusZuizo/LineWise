using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class LockedAssignmentTests
{
    [Fact]
    public void LockedAssignmentsSurviveRegeneration()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 1)
            .Line("Packing", headcount: 1)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .Prefers("Bram Invented", "Packing", rank: 1);

        var engine = TestEngine.Default();

        var before = engine.Generate(scenario.Build()).Days[0];
        Assert.Equal(scenario.LineId("Pastry"), before.For(scenario.EmployeeId("Ada Fictional")).LineId);

        // The manager moves Bram onto Pastry by hand and generates again.
        var after = engine.Generate(scenario.Locked("Bram Invented", "Pastry").Build()).Days[0];

        var pastry = Assert.Single(after.OnLine(scenario.LineId("Pastry")));
        Assert.Equal(scenario.EmployeeId("Bram Invented"), pastry.EmployeeId);
        Assert.True(pastry.IsLocked);
        Assert.Equal(AssignmentSource.Manual, pastry.Source);

        // Ada loses her first choice to the lock rather than the lock losing to her.
        Assert.Equal(scenario.LineId("Packing"), after.For(scenario.EmployeeId("Ada Fictional")).LineId);
    }

    [Fact]
    public void ALockedAssignmentForSomebodyOffThatDayIsKeptAndReported()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Off("Ada Fictional", day: 0)
            .Locked("Ada Fictional", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.True(day.Has(scenario.EmployeeId("Ada Fictional")));

        var warning = Assert.Single(day.Coded(WarningCode.LockedAssignmentConflictsWithAvailability));
        Assert.Equal(WarningSeverity.Notice, warning.Severity);
        Assert.Equal(scenario.EmployeeId("Ada Fictional"), warning.EmployeeId);
    }

    [Fact]
    public void ALockedAssignmentOntoABlockedLineIsKeptAndReported()
    {
        // A lock is the manager overruling the engine, and the engine does not argue. It
        // does say something, because this is usually a mistake.
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .BlockedFrom("Ada Fictional", "Pastry")
            .Locked("Ada Fictional", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(scenario.LineId("Pastry"), day.For(scenario.EmployeeId("Ada Fictional")).LineId);
        Assert.Single(day.Coded(WarningCode.LockedAssignmentViolatesEligibility));
    }

    [Fact]
    public void ALockedLeaderIsNotReplaced()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 3)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .CanLead("Bram Invented", "Pastry")
            .Locked("Ada Fictional", "Pastry", day: 0, role: AssignmentRole.LineLeader);

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        var leader = Assert.Single(
            day.Assignments,
            assignment => assignment.Role == AssignmentRole.LineLeader);
        Assert.Equal(scenario.EmployeeId("Ada Fictional"), leader.EmployeeId);
    }

    [Fact]
    public void ALockedAssignmentFillsAPlaceOnTheLine()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Locked("Cleo Notreal", "Pastry");

        var day = TestEngine.Default().Generate(scenario.Build()).Days[0];

        Assert.Equal(2, day.OnLine(scenario.LineId("Pastry")).Count());
    }
}
