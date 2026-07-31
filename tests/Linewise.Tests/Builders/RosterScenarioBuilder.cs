using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Tests.Builders;

/// <summary>
/// Builds a scenario for the engine to chew on, so tests read as a description of a
/// factory rather than as a page of object construction.
/// </summary>
/// <remarks>
/// Every name here is obviously invented. Real employee names are personal data and never
/// enter this repository, fixtures included.
/// </remarks>
internal sealed class RosterScenarioBuilder
{
    /// <summary>A Monday, so the week reads the way a manager expects.</summary>
    public static readonly DateOnly DefaultWeekStart = new(2026, 8, 3);

    private readonly Dictionary<string, Employee> _employees = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProductionLine> _lines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Skill> _skills = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Employee, int Day), AvailabilityStatus> _availability = new();
    private readonly HashSet<string> _withoutAvailabilityRecord = new(StringComparer.Ordinal);
    private readonly List<LinePreference> _preferences = [];
    private readonly List<LeaderEligibility> _leaderEligibilities = [];
    private readonly List<Assignment> _lockedAssignments = [];
    private readonly List<HistoricAssignment> _history = [];

    private DateOnly _weekStart = DefaultWeekStart;
    private int _dayCount = 5;

    public RosterScenarioBuilder Days(int dayCount)
    {
        _dayCount = dayCount;
        return this;
    }

    public RosterScenarioBuilder Starting(DateOnly weekStart)
    {
        _weekStart = weekStart;
        return this;
    }

    public RosterScenarioBuilder Line(string name, int headcount = 2)
    {
        _lines[name] = new ProductionLine
        {
            Id = TestIds.Line(_lines.Count),
            Name = name,
            DisplayOrder = _lines.Count,
            RequiredHeadcount = headcount,
        };

        return this;
    }

    public RosterScenarioBuilder LineRequiresSkill(string line, string skill)
    {
        var skillId = EnsureSkill(skill).Id;

        _lines[line] = _lines[line] with
        {
            RequiredSkillIds = _lines[line].RequiredSkillIds.Append(skillId).ToHashSet(),
        };

        return this;
    }

    public RosterScenarioBuilder Employee(string name)
    {
        _employees[name] = new Employee
        {
            Id = TestIds.Employee(_employees.Count),
            FullName = name,
        };

        return this;
    }

    public RosterScenarioBuilder EmployeeHasSkill(string employee, string skill)
    {
        var skillId = EnsureSkill(skill).Id;

        _employees[employee] = _employees[employee] with
        {
            SkillIds = _employees[employee].SkillIds.Append(skillId).ToHashSet(),
        };

        return this;
    }

    public RosterScenarioBuilder Deactivated(string employee)
    {
        _employees[employee] = _employees[employee] with { IsActive = false };
        return this;
    }

    public RosterScenarioBuilder Prefers(string employee, string line, int rank) =>
        AddPreference(employee, line, rank, PreferenceType.Preferred);

    public RosterScenarioBuilder MustWork(string employee, string line, int rank = 1) =>
        AddPreference(employee, line, rank, PreferenceType.Mandatory);

    public RosterScenarioBuilder BlockedFrom(string employee, string line) =>
        AddPreference(employee, line, 0, PreferenceType.Blocked);

    public RosterScenarioBuilder CanLead(string employee, string line)
    {
        _leaderEligibilities.Add(new LeaderEligibility
        {
            EmployeeId = _employees[employee].Id,
            LineId = _lines[line].Id,
        });

        return this;
    }

    public RosterScenarioBuilder Off(string employee, int day)
    {
        _availability[(employee, day)] = AvailabilityStatus.Off;
        return this;
    }

    public RosterScenarioBuilder Overtime(string employee, int day)
    {
        _availability[(employee, day)] = AvailabilityStatus.Overtime;
        return this;
    }

    /// <summary>
    /// Leaves this employee out of the availability sheet entirely, which the engine has to
    /// read as off rather than as available.
    /// </summary>
    public RosterScenarioBuilder NoAvailabilityRecordFor(string employee)
    {
        _withoutAvailabilityRecord.Add(employee);
        return this;
    }

    public RosterScenarioBuilder Locked(
        string employee,
        string line,
        int day = 0,
        AssignmentRole role = AssignmentRole.Worker)
    {
        _lockedAssignments.Add(new Assignment
        {
            Date = DateFor(day),
            ShiftId = TestIds.Shift(day),
            LineId = _lines[line].Id,
            EmployeeId = _employees[employee].Id,
            Role = role,
            IsLocked = true,
            Source = AssignmentSource.Manual,
            Explanation = AssignmentExplanation.ManualOverride,
        });

        return this;
    }

    public RosterScenarioBuilder PreviouslyWorked(
        string employee,
        string line,
        int daysAgo,
        AssignmentRole role = AssignmentRole.Worker)
    {
        _history.Add(new HistoricAssignment
        {
            Date = _weekStart.AddDays(-daysAgo),
            EmployeeId = _employees[employee].Id,
            LineId = _lines[line].Id,
            Role = role,
        });

        return this;
    }

    public RosterScenarioBuilder PreviouslyLed(string employee, string line, int daysAgo) =>
        PreviouslyWorked(employee, line, daysAgo, AssignmentRole.LineLeader);

    public Guid EmployeeId(string name) => _employees[name].Id;

    public Guid LineId(string name) => _lines[name].Id;

    public DateOnly DateFor(int day) => _weekStart.AddDays(day);

    // Everything is copied out, so a request already built is not changed underneath a test
    // that carries on configuring the scenario.
    public RosterConfiguration BuildConfiguration() => new()
    {
        Employees = _employees.Values.ToList(),
        Lines = _lines.Values.ToList(),
        Skills = _skills.Values.ToList(),
        Preferences = _preferences.ToList(),
        LeaderEligibilities = _leaderEligibilities.ToList(),
    };

    public AssignmentRequest Build() => new()
    {
        WeekStart = _weekStart,
        Configuration = BuildConfiguration(),
        Shifts = BuildShifts(),
        Availabilities = BuildAvailabilities(),
        LockedAssignments = _lockedAssignments.ToList(),
        History = _history.ToList(),
    };

    private List<Shift> BuildShifts() =>
        Enumerable.Range(0, _dayCount)
            .Select(day => new Shift
            {
                Id = TestIds.Shift(day),
                Date = DateFor(day),
                Name = ShiftName.Day,
            })
            .ToList();

    /// <summary>
    /// Everybody works every day unless the scenario says otherwise, because most tests are
    /// about a placement rule and not about who turned up.
    /// </summary>
    private List<Availability> BuildAvailabilities()
    {
        var availabilities = new List<Availability>();

        foreach (var (name, employee) in _employees)
        {
            if (_withoutAvailabilityRecord.Contains(name))
            {
                continue;
            }

            for (var day = 0; day < _dayCount; day++)
            {
                availabilities.Add(new Availability
                {
                    EmployeeId = employee.Id,
                    Date = DateFor(day),
                    Status = _availability.GetValueOrDefault((name, day), AvailabilityStatus.Working),
                });
            }
        }

        return availabilities;
    }

    private RosterScenarioBuilder AddPreference(string employee, string line, int rank, PreferenceType type)
    {
        _preferences.Add(new LinePreference
        {
            EmployeeId = _employees[employee].Id,
            LineId = _lines[line].Id,
            Rank = rank,
            Type = type,
        });

        return this;
    }

    private Skill EnsureSkill(string name)
    {
        if (!_skills.TryGetValue(name, out var skill))
        {
            skill = new Skill { Id = TestIds.Skill(_skills.Count), Name = name };
            _skills[name] = skill;
        }

        return skill;
    }
}
