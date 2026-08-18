using Linewise.Application.Persistence;

namespace Linewise.Application.Printing;

/// <inheritdoc cref="IPublishingService"/>
public sealed class PublishingService : IPublishingService
{
    private readonly IRosterRepository _rosters;

    public PublishingService(IRosterRepository rosters)
    {
        ArgumentNullException.ThrowIfNull(rosters);

        _rosters = rosters;
    }

    /// <summary>
    /// What the audit chain records. English rather than the current culture, and held here
    /// rather than in the user-facing string table, for the same reason an absence reason
    /// is: an entry is written once and read years later, possibly on another machine, and
    /// one that changed language with a regional setting would be a poor record.
    /// </summary>
    private const string Reason = "Published for the wall.";

    public async Task<PublishResult> PublishAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        // Read before publishing, not after. Publishing is what makes the new version the
        // published one, so asking afterwards returns the sheet that has just gone up rather
        // than the one it replaced — and an amendment slip comparing a roster with itself
        // says nothing changed, which would be a lie told confidently.
        var previous = await _rosters
            .GetPublishedAsync(weekStart, cancellationToken)
            .ConfigureAwait(false);

        var version = await _rosters
            .PublishAsync(weekStart, Reason, cancellationToken)
            .ConfigureAwait(false);

        return new PublishResult(version, previous?.Roster);
    }
}
