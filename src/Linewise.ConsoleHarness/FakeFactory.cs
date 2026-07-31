using System.Globalization;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.ConsoleHarness;

/// <summary>
/// An invented factory to run the engine against. Every name here is made up, as are the
/// line names and headcounts. Nothing about a real site belongs in this repository.
/// </summary>
internal static class FakeFactory
{
    private static readonly DateOnly WeekStart = new(2026, 8, 3);

    private static readonly string[] Workforce =
    [
        "Ada Fictional",
        "Bram Invented",
        "Cleo Notreal",
        "Dara Madeup",
        "Eli Pretend",
        "Fen Imaginary",
        "Gus Hypothetical",
        "Hana Placeholder",
        "Iris Sample",
        "Jonas Example",
        "Kai Stand-in",
        "Lena Dummy",
        "Milo Mockup",
        "Nia Fabricated",
    ];

    public static AssignmentRequest Build()
    {
        var ovenTicket = new Skill { Id = Id(3, 0), Name = "Oven ticket" };
        var allergenHandling = new Skill { Id = Id(3, 1), Name = "Allergen handling" };

        var lines = new List<ProductionLine>
        {
            Line(0, "Pastry", headcount: 4),
            Line(1, "Packing", headcount: 3),
            Line(2, "Ovens", headcount: 3, ovenTicket.Id),
            Line(3, "Chilled prep", headcount: 3, allergenHandling.Id),
        };

        var employees = Workforce
            .Select((name, index) => new Employee
            {
                Id = Id(1, index),
                FullName = name,
                IsTemporary = index >= 12,
                SkillIds = SkillsFor(index, ovenTicket.Id, allergenHandling.Id),
            })
            .ToList();

        return new AssignmentRequest
        {
            WeekStart = WeekStart,
            Configuration = new RosterConfiguration
            {
                Employees = employees,
                Lines = lines,
                Skills = [ovenTicket, allergenHandling],
                Preferences = BuildPreferences(employees, lines),
                LeaderEligibilities = BuildLeaderEligibilities(employees, lines),
            },
            Shifts = BuildShifts(),
            Availabilities = BuildAvailabilities(employees),
            History = BuildHistory(employees, lines),
        };
    }

    private static ProductionLine Line(int index, string name, int headcount, params Guid[] requiredSkills) => new()
    {
        Id = Id(2, index),
        Name = name,
        DisplayOrder = index,
        RequiredHeadcount = headcount,
        RequiredSkillIds = requiredSkills.ToHashSet(),
    };

    private static HashSet<Guid> SkillsFor(int index, Guid ovenTicket, Guid allergenHandling)
    {
        var skills = new HashSet<Guid>();

        if (index % 3 == 0)
        {
            skills.Add(ovenTicket);
        }

        // Odd numbered, so that the people eligible to lead Chilled prep are also qualified
        // to stand on it.
        if (index % 2 == 1)
        {
            skills.Add(allergenHandling);
        }

        return skills;
    }

    private static List<LinePreference> BuildPreferences(
        IReadOnlyList<Employee> employees,
        IReadOnlyList<ProductionLine> lines)
    {
        var preferences = new List<LinePreference>();

        for (var index = 0; index < employees.Count; index++)
        {
            if (index == 3)
            {
                // Always on Ovens, so a ranked list would only contradict itself.
                continue;
            }

            var first = lines[index % lines.Count];
            var second = lines[(index + 1) % lines.Count];

            preferences.Add(new LinePreference
            {
                EmployeeId = employees[index].Id,
                LineId = first.Id,
                Rank = 1,
                Type = PreferenceType.Preferred,
            });

            preferences.Add(new LinePreference
            {
                EmployeeId = employees[index].Id,
                LineId = second.Id,
                Rank = 2,
                Type = PreferenceType.Preferred,
            });
        }

        // One person who always works Ovens, and one who never works Packing.
        preferences.Add(new LinePreference
        {
            EmployeeId = employees[3].Id,
            LineId = lines[2].Id,
            Rank = 1,
            Type = PreferenceType.Mandatory,
        });

        preferences.Add(new LinePreference
        {
            EmployeeId = employees[5].Id,
            LineId = lines[1].Id,
            Rank = 0,
            Type = PreferenceType.Blocked,
        });

        return preferences;
    }

    private static List<LeaderEligibility> BuildLeaderEligibilities(
        IReadOnlyList<Employee> employees,
        IReadOnlyList<ProductionLine> lines)
    {
        var eligibilities = new List<LeaderEligibility>();

        // The first eight can lead two lines each, so leadership has somewhere to rotate.
        for (var index = 0; index < 8; index++)
        {
            eligibilities.Add(new LeaderEligibility
            {
                EmployeeId = employees[index].Id,
                LineId = lines[index % lines.Count].Id,
            });

            eligibilities.Add(new LeaderEligibility
            {
                EmployeeId = employees[index].Id,
                LineId = lines[(index + 2) % lines.Count].Id,
            });
        }

        return eligibilities;
    }

    private static List<Shift> BuildShifts() =>
        Enumerable.Range(0, 5)
            .Select(day => new Shift
            {
                Id = Id(4, day),
                Date = WeekStart.AddDays(day),
                Name = ShiftName.Day,
            })
            .ToList();

    private static List<Availability> BuildAvailabilities(IReadOnlyList<Employee> employees)
    {
        var availabilities = new List<Availability>();

        for (var index = 0; index < employees.Count; index++)
        {
            for (var day = 0; day < 5; day++)
            {
                var status = (index, day) switch
                {
                    (2, 1) => AvailabilityStatus.Off,
                    (7, 3) => AvailabilityStatus.Off,
                    (9, _) when day >= 3 => AvailabilityStatus.Off,
                    (11, 2) => AvailabilityStatus.Overtime,
                    _ => AvailabilityStatus.Working,
                };

                availabilities.Add(new Availability
                {
                    EmployeeId = employees[index].Id,
                    Date = WeekStart.AddDays(day),
                    Status = status,
                });
            }
        }

        return availabilities;
    }

    /// <summary>A fortnight of past weeks, so fairness and leadership have something to go on.</summary>
    private static List<HistoricAssignment> BuildHistory(
        IReadOnlyList<Employee> employees,
        IReadOnlyList<ProductionLine> lines)
    {
        var history = new List<HistoricAssignment>();

        for (var day = 1; day <= 10; day++)
        {
            for (var index = 0; index < employees.Count; index++)
            {
                history.Add(new HistoricAssignment
                {
                    Date = WeekStart.AddDays(-day),
                    EmployeeId = employees[index].Id,
                    LineId = lines[(index + day) % lines.Count].Id,
                    Role = index == day % employees.Count ? AssignmentRole.LineLeader : AssignmentRole.Worker,
                });
            }
        }

        return history;
    }

    private static Guid Id(int kind, int index) =>
        Guid.ParseExact(
            string.Create(CultureInfo.InvariantCulture, $"{kind:D8}-0000-0000-0000-{index:D12}"),
            "d");
}
