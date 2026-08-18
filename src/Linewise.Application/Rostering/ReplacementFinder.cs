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

        return ReplacementRanking.For(line, date, configuration, status, placedToday, lineNames);
    }

    private static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
