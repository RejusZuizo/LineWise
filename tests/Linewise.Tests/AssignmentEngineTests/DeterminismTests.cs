using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class DeterminismTests
{
    [Fact]
    public void IdenticalInputsProduceIdenticalOutput()
    {
        var request = BusyWeek().Build();

        var first = TestEngine.Default().Generate(request);
        var second = TestEngine.Default().Generate(request);

        Assert.Equal(first.AllAssignments.ToList(), second.AllAssignments.ToList());
        Assert.Equal(
            first.AllWarnings.Select(warning => (warning.Code, warning.LineId, warning.EmployeeId, warning.Date)).ToList(),
            second.AllWarnings.Select(warning => (warning.Code, warning.LineId, warning.EmployeeId, warning.Date)).ToList());
    }

    [Fact]
    public void OneEngineInstanceCarriesNothingBetweenCalls()
    {
        var request = BusyWeek().Build();
        var engine = TestEngine.Default();

        var first = engine.Generate(request);
        var second = engine.Generate(request);

        Assert.Equal(first.AllAssignments.ToList(), second.AllAssignments.ToList());
    }

    [Fact]
    public void TheOrderTheCallerListsPeopleInDoesNotChangeTheRoster()
    {
        var request = BusyWeek().Build();

        var reversed = request with
        {
            Configuration = request.Configuration with
            {
                Employees = request.Configuration.Employees.Reverse().ToList(),
                Lines = request.Configuration.Lines.Reverse().ToList(),
            },
        };

        var fromOriginal = TestEngine.Default().Generate(request);
        var fromReversed = TestEngine.Default().Generate(reversed);

        Assert.Equal(fromOriginal.AllAssignments.ToList(), fromReversed.AllAssignments.ToList());
    }

    private static RosterScenarioBuilder BusyWeek() =>
        new RosterScenarioBuilder()
            .Line("Pastry", headcount: 3)
            .Line("Packing", headcount: 2)
            .Line("Ovens", headcount: 2)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Employee("Eli Pretend")
            .Employee("Fen Imaginary")
            .Employee("Gus Hypothetical")
            .EmployeeHasSkill("Ada Fictional", "Oven ticket")
            .EmployeeHasSkill("Bram Invented", "Oven ticket")
            .EmployeeHasSkill("Cleo Notreal", "Oven ticket")
            .CanLead("Ada Fictional", "Ovens")
            .CanLead("Bram Invented", "Ovens")
            .CanLead("Dara Madeup", "Pastry")
            .CanLead("Eli Pretend", "Pastry")
            .CanLead("Fen Imaginary", "Packing")
            .Prefers("Dara Madeup", "Pastry", rank: 1)
            .Prefers("Eli Pretend", "Pastry", rank: 1)
            .Prefers("Fen Imaginary", "Pastry", rank: 1)
            .Prefers("Gus Hypothetical", "Packing", rank: 1)
            .Prefers("Gus Hypothetical", "Pastry", rank: 2)
            .MustWork("Cleo Notreal", "Ovens")
            .Off("Eli Pretend", day: 2)
            .Overtime("Gus Hypothetical", day: 3);
}
