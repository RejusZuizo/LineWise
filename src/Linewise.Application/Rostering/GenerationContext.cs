using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <summary>
/// The request, indexed for lookup, plus the running ledger. Built fresh for every
/// generation so the engine itself holds no state between calls.
/// </summary>
internal sealed class GenerationContext
{
    private readonly Dictionary<Guid, Employee> _employeesById = new();
    private readonly Dictionary<Guid, ProductionLine> _linesById = new();
    private readonly Dictionary<Guid, int> _lineOrder = new();
    private readonly Dictionary<(Guid EmployeeId, DateOnly Date), AvailabilityStatus> _availability = new();
    private readonly Dictionary<(Guid EmployeeId, Guid LineId), LinePreference> _preferences = new();
    private readonly HashSet<(Guid EmployeeId, Guid LineId)> _leaderEligibility = new();

    private readonly HashSet<(Guid EmployeeId, Guid LineId)> _assistantEligibility = new();
    private readonly Dictionary<(DateOnly Date, Guid ShiftId), List<Assignment>> _lockedAssignments = new();
    private readonly Dictionary<DateOnly, HashSet<Guid>> _assignedByDate = new();
    private readonly Dictionary<(Guid LineId, DateOnly Date), int> _demand = new();
    private readonly HashSet<(Guid LineId, DateOnly Date)> _closed = [];

    public GenerationContext(AssignmentRequest request)
    {
        var configuration = request.Configuration;

        Lines = configuration.Lines
            .OrderBy(line => line.DisplayOrder)
            .ThenBy(line => line.Id)
            .ToList();

        for (var index = 0; index < Lines.Count; index++)
        {
            _linesById[Lines[index].Id] = Lines[index];
            _lineOrder[Lines[index].Id] = index;
        }

        // Duplicates in the input are a data fault, not a reason to throw in the manager's
        // face. Last one wins, quietly and predictably.
        foreach (var employee in configuration.Employees)
        {
            _employeesById[employee.Id] = employee;
        }

        foreach (var availability in request.Availabilities)
        {
            _availability[(availability.EmployeeId, availability.Date)] = availability.Status;
        }

        foreach (var preference in configuration.Preferences)
        {
            _preferences[(preference.EmployeeId, preference.LineId)] = preference;
        }

        foreach (var eligibility in configuration.LeaderEligibilities)
        {
            _leaderEligibility.Add((eligibility.EmployeeId, eligibility.LineId));
        }

        foreach (var eligibility in configuration.OperatingAssistantEligibilities)
        {
            _assistantEligibility.Add((eligibility.EmployeeId, eligibility.LineId));
        }

        foreach (var demand in request.Demands)
        {
            _demand[(demand.LineId, demand.Date)] = demand.RequiredHeadcount;

            if (demand.IsClosed)
            {
                _closed.Add((demand.LineId, demand.Date));
            }
        }

        foreach (var assignment in request.LockedAssignments.Where(assignment => assignment.IsLocked))
        {
            var key = (assignment.Date, assignment.ShiftId);

            if (!_lockedAssignments.TryGetValue(key, out var forShift))
            {
                forShift = [];
                _lockedAssignments[key] = forShift;
            }

            forShift.Add(assignment);
        }

        ActiveEmployeeIds = _employeesById.Values
            .Where(employee => employee.IsActive)
            .Select(employee => employee.Id)
            .OrderBy(id => id)
            .ToList();

        Shifts = request.Shifts
            .OrderBy(shift => shift.Date)
            .ThenBy(shift => shift.Name)
            .ThenBy(shift => shift.Id)
            .ToList();

        MandatoryPreferences = BuildMandatoryPreferences(configuration);

        HighestPreferenceRank = configuration.Preferences
            .Where(preference => preference.Type == PreferenceType.Preferred)
            .Select(preference => preference.Rank)
            .DefaultIfEmpty(0)
            .Max();

        Ledger = BuildLedger(request);
    }

    /// <summary>Lines in display order. The engine works through them in this order throughout.</summary>
    public IReadOnlyList<ProductionLine> Lines { get; }

    /// <summary>Shifts in date then shift order.</summary>
    public IReadOnlyList<Shift> Shifts { get; }

    /// <summary>Active employees, ordered so the result does not depend on how the caller sorted its list.</summary>
    public IReadOnlyList<Guid> ActiveEmployeeIds { get; }

    /// <summary>
    /// One mandatory preference per employee, highest ranked first. An employee mandatory on
    /// two lines cannot be on both; the validator reports it, and the engine takes the
    /// highest ranked so it still produces a roster.
    /// </summary>
    public IReadOnlyList<LinePreference> MandatoryPreferences { get; }

    public int HighestPreferenceRank { get; }

    public AssignmentLedger Ledger { get; }

    public ProductionLine? LineById(Guid lineId) => _linesById.GetValueOrDefault(lineId);

    /// <summary>
    /// How many people this line needs on this date: the demand set for the day when there
    /// is one, and the line's standard headcount otherwise.
    /// </summary>
    public int HeadcountFor(ProductionLine line, DateOnly date) =>
        IsClosed(line, date) ? 0
        : _demand.TryGetValue((line.Id, date), out var demand) ? demand
        : line.RequiredHeadcount;

    /// <summary>
    /// The line is not running on this date. It takes nobody and is warned about for
    /// nothing: a line that is not running is not short of people, and its usual crew are
    /// free for the lines that are running.
    /// </summary>
    public bool IsClosed(ProductionLine line, DateOnly date) => _closed.Contains((line.Id, date));

    /// <summary>
    /// Whether this line is running above its usual complement, which is the manager saying
    /// it has more product to get out. This is what overtime is routed toward.
    /// </summary>
    public bool IsRunningHot(ProductionLine line, DateOnly date) =>
        !IsClosed(line, date) && HeadcountFor(line, date) > line.RequiredHeadcount;

    public AvailabilityStatus StatusOn(Guid employeeId, DateOnly date) =>
        _availability.GetValueOrDefault((employeeId, date), AvailabilityStatus.Off);

    /// <summary>
    /// Whether this person can be placed at all on this date. An employee with no
    /// availability record is off: silence is never read as availability.
    /// </summary>
    public bool CanBeRostered(Guid employeeId, DateOnly date) =>
        _employeesById.TryGetValue(employeeId, out var employee)
        && employee.IsActive
        && StatusOn(employeeId, date) is AvailabilityStatus.Working or AvailabilityStatus.Overtime;

    public LinePreference? PreferenceFor(Guid employeeId, Guid lineId) =>
        _preferences.GetValueOrDefault((employeeId, lineId));

    public bool IsPreferredLine(Guid employeeId, Guid lineId) =>
        PreferenceFor(employeeId, lineId) is { Type: PreferenceType.Preferred or PreferenceType.Mandatory };

    public bool CanLead(Guid employeeId, Guid lineId) =>
        _leaderEligibility.Contains((employeeId, lineId));

    public bool CanAssist(Guid employeeId, Guid lineId) =>
        _assistantEligibility.Contains((employeeId, lineId));

    /// <summary>
    /// Blocked lines and missing skills. Absolute: nothing but a locked assignment placed by
    /// hand gets past this.
    /// </summary>
    public bool IsEligibleFor(Guid employeeId, ProductionLine line)
    {
        if (PreferenceFor(employeeId, line.Id)?.Type == PreferenceType.Blocked)
        {
            return false;
        }

        return _employeesById.TryGetValue(employeeId, out var employee)
            && Eligibility.HasRequiredSkills(employee, line);
    }

    public IReadOnlyList<Assignment> LockedFor(Shift shift) =>
        _lockedAssignments.TryGetValue((shift.Date, shift.Id), out var forShift)
            ? forShift
                .OrderBy(assignment => _lineOrder.GetValueOrDefault(assignment.LineId, int.MaxValue))
                .ThenBy(assignment => assignment.EmployeeId)
                .ToList()
            : [];

    /// <summary>
    /// Who already has a place on this date, across every shift. Shared between shifts so
    /// nobody is rostered onto the day shift and the night shift of the same day.
    /// </summary>
    public HashSet<Guid> AssignedOn(DateOnly date)
    {
        if (!_assignedByDate.TryGetValue(date, out var assigned))
        {
            assigned = [];
            _assignedByDate[date] = assigned;
        }

        return assigned;
    }

    private List<LinePreference> BuildMandatoryPreferences(RosterConfiguration configuration) =>
        configuration.Preferences
            .Where(preference => preference.Type == PreferenceType.Mandatory)
            .Where(preference => _linesById.ContainsKey(preference.LineId))
            .GroupBy(preference => preference.EmployeeId)
            .Select(forEmployee => forEmployee
                .OrderBy(preference => preference.Rank)
                .ThenBy(preference => _lineOrder.GetValueOrDefault(preference.LineId, int.MaxValue))
                .First())
            .OrderBy(preference => preference.Rank)
            .ThenBy(preference => _lineOrder.GetValueOrDefault(preference.LineId, int.MaxValue))
            .ThenBy(preference => preference.EmployeeId)
            .ToList();

    private AssignmentLedger BuildLedger(AssignmentRequest request)
    {
        var ledger = new AssignmentLedger();

        foreach (var historic in request.History
            .OrderBy(historic => historic.Date)
            .ThenBy(historic => historic.EmployeeId))
        {
            ledger.RecordPlacement(historic.EmployeeId, IsPreferredLine(historic.EmployeeId, historic.LineId));

            if (historic.Role == AssignmentRole.LineLeader)
            {
                ledger.RecordLead(historic.EmployeeId, historic.Date);
            }
        }

        return ledger;
    }
}
