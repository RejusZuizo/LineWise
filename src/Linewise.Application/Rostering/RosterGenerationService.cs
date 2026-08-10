using Linewise.Application.Persistence;
using Linewise.Domain.Entities;

namespace Linewise.Application.Rostering;

/// <inheritdoc cref="IRosterGenerationService"/>
public sealed class RosterGenerationService : IRosterGenerationService
{
    /// <summary>
    /// How far back fairness looks. Four weeks is long enough that "who had the worst line
    /// last" is answered from more than the week just gone, and short enough that somebody
    /// returning from a month off is not still being compensated for it.
    /// </summary>
    public const int HistoryWeeks = 4;

    private readonly IAssignmentEngine _engine;
    private readonly IConfigurationRepository _configuration;
    private readonly IAvailabilityRepository _availability;
    private readonly IShiftRepository _shifts;
    private readonly ILineDemandRepository _demands;
    private readonly IRosterRepository _rosters;

    public RosterGenerationService(
        IAssignmentEngine engine,
        IConfigurationRepository configuration,
        IAvailabilityRepository availability,
        IShiftRepository shifts,
        ILineDemandRepository demands,
        IRosterRepository rosters)
    {
        _engine = engine;
        _configuration = configuration;
        _availability = availability;
        _shifts = shifts;
        _demands = demands;
        _rosters = rosters;
    }

    public async Task<StoredRoster> GenerateAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var weekEnd = weekStart.AddDays(7);

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);
        var shifts = await _shifts.GetAsync(weekStart, weekEnd, cancellationToken).ConfigureAwait(false);
        var availability = await _availability.GetAsync(weekStart, weekEnd, cancellationToken).ConfigureAwait(false);
        var demands = await _demands.GetAsync(weekStart, weekEnd, cancellationToken).ConfigureAwait(false);

        var history = await _rosters
            .GetHistoryAsync(weekStart.AddDays(-7 * HistoryWeeks), weekStart, cancellationToken)
            .ConfigureAwait(false);

        // Anything the manager placed by hand survives regeneration. Reading it back out of
        // the stored draft is what makes "generate again" safe to press: the engine is told
        // about those placements rather than being left to rediscover them.
        var existing = await _rosters.GetLatestAsync(weekStart, cancellationToken).ConfigureAwait(false);

        var locked = existing?.Roster.AllAssignments
            .Where(assignment => assignment.IsLocked)
            .ToList() ?? [];

        var roster = _engine.Generate(new AssignmentRequest
        {
            WeekStart = weekStart,
            Configuration = configuration,
            Shifts = shifts,
            Availabilities = availability,
            Demands = demands,
            LockedAssignments = locked,
            History = history,
        });

        var version = await _rosters
            .SaveDraftAsync(weekStart, roster, cancellationToken)
            .ConfigureAwait(false);

        return new StoredRoster(version, roster);
    }
}
