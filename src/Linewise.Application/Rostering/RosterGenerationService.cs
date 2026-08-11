using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

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
        var shifts = await EnsureShiftsAsync(weekStart, weekEnd, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// A roster is generated per shift, so a week with no shifts produces no days at all —
    /// an empty grid, with nothing on screen to explain why.
    /// </summary>
    /// <remarks>
    /// A single day shift per date is created when none exist. The design document is
    /// explicit that the product must be useful with zero rules configured, producing a
    /// reasonable roster from availability and headcount alone; requiring somebody to
    /// define a shift before the first generate is exactly the kind of setup step that
    /// kills a tool at first contact.
    /// <para>
    /// Only ever adds. A site running nights configures them once and this leaves them
    /// alone thereafter, because <c>EnsureAsync</c> returns what already exists.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<Shift>> EnsureShiftsAsync(
        DateOnly weekStart,
        DateOnly weekEnd,
        CancellationToken cancellationToken)
    {
        var existing = await _shifts.GetAsync(weekStart, weekEnd, cancellationToken).ConfigureAwait(false);

        var missing = Enumerable
            .Range(0, weekEnd.DayNumber - weekStart.DayNumber)
            .Select(offset => weekStart.AddDays(offset))
            .Where(date => !existing.Any(shift => shift.Date == date))
            .Select(date => new Shift { Id = Guid.NewGuid(), Date = date, Name = ShiftName.Day })
            .ToList();

        return missing.Count == 0
            ? existing
            : await _shifts.EnsureAsync([.. existing, .. missing], cancellationToken).ConfigureAwait(false);
    }
}
