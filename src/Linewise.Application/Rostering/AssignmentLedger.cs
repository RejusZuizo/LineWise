namespace Linewise.Application.Rostering;

/// <inheritdoc cref="IAssignmentLedger"/>
public sealed class AssignmentLedger : IAssignmentLedger
{
    private readonly Dictionary<Guid, int> _preferredPlacements = new();
    private readonly Dictionary<Guid, int> _totalPlacements = new();
    private readonly Dictionary<Guid, int> _leadCounts = new();
    private readonly Dictionary<Guid, DateOnly> _lastLed = new();

    public int PreferredPlacements(Guid employeeId) => _preferredPlacements.GetValueOrDefault(employeeId);

    public int TotalPlacements(Guid employeeId) => _totalPlacements.GetValueOrDefault(employeeId);

    public int LeadCount(Guid employeeId) => _leadCounts.GetValueOrDefault(employeeId);

    public DateOnly? LastLedOn(Guid employeeId) =>
        _lastLed.TryGetValue(employeeId, out var date) ? date : null;

    /// <summary>Records a placement.</summary>
    /// <param name="matchedPreference">
    /// True when the line was one the employee had asked for, whether ranked or required.
    /// </param>
    public void RecordPlacement(Guid employeeId, bool matchedPreference)
    {
        _totalPlacements[employeeId] = TotalPlacements(employeeId) + 1;

        if (matchedPreference)
        {
            _preferredPlacements[employeeId] = PreferredPlacements(employeeId) + 1;
        }
    }

    /// <summary>Records that an employee led a line on a date.</summary>
    public void RecordLead(Guid employeeId, DateOnly date)
    {
        _leadCounts[employeeId] = LeadCount(employeeId) + 1;

        if (!_lastLed.TryGetValue(employeeId, out var existing) || date > existing)
        {
            _lastLed[employeeId] = date;
        }
    }
}
