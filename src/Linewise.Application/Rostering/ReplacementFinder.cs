using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <inheritdoc cref="IReplacementFinder"/>
public sealed class ReplacementFinder : IReplacementFinder
{
    private readonly IConfigurationRepository _configuration;
    private readonly IAvailabilityRepository _availability;
    private readonly IRosterRepository _rosters;

    public ReplacementFinder(
        IConfigurationRepository configuration,
        IAvailabilityRepository availability,
        IRosterRepository rosters)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(rosters);

        _configuration = configuration;
        _availability = availability;
        _rosters = rosters;
    }

    public async Task<IReadOnlyList<ReplacementCandidate>> FindAsync(
        Guid lineId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);
        var line = configuration.Lines.FirstOrDefault(candidate => candidate.Id == lineId);

        if (line is null)
        {
            return [];
        }

        var availability = await _availability
            .GetAsync(date, date.AddDays(1), cancellationToken)
            .ConfigureAwait(false);

        var stored = await _rosters
            .GetLatestAsync(MondayOf(date), cancellationToken)
            .ConfigureAwait(false);

        var placedToday = stored?.Roster.AllAssignments
            .Where(assignment => assignment.Date == date)
            .ToDictionary(assignment => assignment.EmployeeId, assignment => assignment.LineId)
            ?? [];

        var status = availability
            .ToDictionary(record => record.EmployeeId, record => record.Status);

        var lineNames = configuration.Lines.ToDictionary(l => l.Id, l => l.Name);

        var candidates = new List<ReplacementCandidate>();

        foreach (var employee in configuration.Employees.Where(employee => employee.IsActive))
        {
            // Silence is never read as availability. Somebody with no record for the day is
            // not offered, the same rule the engine applies when it decides who to place.
            if (!status.TryGetValue(employee.Id, out var today)
                || today is not (AvailabilityStatus.Working or AvailabilityStatus.Overtime))
            {
                continue;
            }

            var preference = configuration.Preferences.FirstOrDefault(rule =>
                rule.EmployeeId == employee.Id && rule.LineId == lineId);

            // Absolute, exactly as in the engine. Blocked and unskilled are not ranked
            // low — they are not offered at all, because offering them invites the one
            // click that puts somebody on a line they may not be on.
            if (preference?.Type == PreferenceType.Blocked
                || !line.RequiredSkillIds.All(employee.SkillIds.Contains))
            {
                continue;
            }

            // Already here. Not a replacement for a place on this line.
            if (placedToday.TryGetValue(employee.Id, out var placedOn) && placedOn == lineId)
            {
                continue;
            }

            candidates.Add(new ReplacementCandidate
            {
                EmployeeId = employee.Id,
                DisplayName = employee.FullName,
                Status = today,
                PreferenceRank = preference?.Type is PreferenceType.Preferred or PreferenceType.Mandatory
                    ? preference.Rank
                    : null,
                IsRequiredHere = preference?.Type == PreferenceType.Mandatory,
                CanLead = configuration.LeaderEligibilities.Any(eligibility =>
                    eligibility.EmployeeId == employee.Id && eligibility.LineId == lineId),
                CanAssist = configuration.OperatingAssistantEligibilities.Any(eligibility =>
                    eligibility.EmployeeId == employee.Id && eligibility.LineId == lineId),
                AlreadyOnLineId = placedToday.TryGetValue(employee.Id, out var elsewhere) ? elsewhere : null,
                AlreadyOnLineName = placedToday.TryGetValue(employee.Id, out var other)
                    ? lineNames.GetValueOrDefault(other)
                    : null,
            });
        }

        return [.. candidates.OrderBy(Rank).ThenBy(candidate => candidate.DisplayName, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// The order the list is offered in, and the whole reason this exists.
    /// </summary>
    /// <remarks>
    /// Somebody standing about comes before somebody already on a line, because the first
    /// costs nothing and the second opens a second hole. Within each group, a person
    /// required on this line comes first, then whoever chose it and how highly, then anybody
    /// with no opinion. Overtime sorts below plain availability at equal preference: both
    /// are available, and one of them is being paid a premium.
    /// <para>
    /// The final tie is broken by name. That is alphabetical, and it is the last thing
    /// consulted rather than the first, which is the difference between a ranked list and
    /// the register.
    /// </para>
    /// </remarks>
    private static (int Placed, int Required, int Rank, int Premium) Rank(ReplacementCandidate candidate) =>
        (candidate.LeavesAHoleElsewhere ? 1 : 0,
            candidate.IsRequiredHere ? 0 : 1,
            candidate.PreferenceRank ?? int.MaxValue,
            candidate.Status == AvailabilityStatus.Overtime ? 1 : 0);

    private static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
