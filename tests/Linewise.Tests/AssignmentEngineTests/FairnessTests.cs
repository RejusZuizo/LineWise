using Linewise.Application.Rostering;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.AssignmentEngineTests;

public sealed class FairnessTests
{
    [Fact]
    public void ThePreferredLineIsSharedOutEvenlyOverFourWeeks()
    {
        // One good line with a single place, four people who all want it, twenty working
        // days. Nobody should be able to claim they never get a turn.
        var names = new[] { "Ada Fictional", "Bram Invented", "Cleo Notreal", "Dara Madeup" };
        var engine = TestEngine.Default();
        var history = new List<HistoricAssignment>();
        var timesOnTheGoodLine = names.ToDictionary(name => name, _ => 0, StringComparer.Ordinal);

        for (var week = 0; week < 4; week++)
        {
            var scenario = BuildWeek(names, week);
            var request = scenario.Build() with { History = history };

            var roster = engine.Generate(request);

            foreach (var name in names)
            {
                timesOnTheGoodLine[name] += roster.AllAssignments
                    .Count(assignment => assignment.EmployeeId == scenario.EmployeeId(name)
                        && assignment.LineId == scenario.LineId("Pastry"));
            }

            history.AddRange(roster.AsHistory());
        }

        Assert.All(timesOnTheGoodLine.Values, count => Assert.Equal(5, count));
    }

    [Fact]
    public void FairnessAlreadyEvensOutWithinASingleWeek()
    {
        // Without a ledger that updates during generation, the same person would take the
        // good line all five days and every individual day would look correct.
        var scenario = new RosterScenarioBuilder()
            .Line("Pastry", headcount: 1)
            .Line("Packing", headcount: 4)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Employee("Eli Pretend")
            .Prefers("Ada Fictional", "Pastry", rank: 1)
            .Prefers("Bram Invented", "Pastry", rank: 1)
            .Prefers("Cleo Notreal", "Pastry", rank: 1)
            .Prefers("Dara Madeup", "Pastry", rank: 1)
            .Prefers("Eli Pretend", "Pastry", rank: 1);

        var roster = TestEngine.Default().Generate(scenario.Build());

        var pastry = scenario.LineId("Pastry");
        var turns = roster.AllAssignments
            .Where(assignment => assignment.LineId == pastry)
            .GroupBy(assignment => assignment.EmployeeId)
            .Select(group => group.Count())
            .ToList();

        Assert.Equal(5, turns.Count);
        Assert.All(turns, count => Assert.Equal(1, count));
    }

    private static RosterScenarioBuilder BuildWeek(IEnumerable<string> names, int week)
    {
        var scenario = new RosterScenarioBuilder()
            .Starting(RosterScenarioBuilder.DefaultWeekStart.AddDays(7 * week))
            .Line("Pastry", headcount: 1)
            .Line("Packing", headcount: 3);

        foreach (var name in names)
        {
            scenario.Employee(name).Prefers(name, "Pastry", rank: 1);
        }

        return scenario;
    }
}
