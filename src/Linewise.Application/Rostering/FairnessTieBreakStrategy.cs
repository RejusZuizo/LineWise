namespace Linewise.Application.Rostering;

/// <summary>
/// Gives the place to whoever has had their preferred line least often. Over a week, and
/// more so over a month, this spreads the good lines and the bad ones evenly.
/// </summary>
public sealed class FairnessTieBreakStrategy : ITieBreakStrategy
{
    public IReadOnlyList<Guid> Prioritise(IReadOnlyCollection<Guid> candidateEmployeeIds, TieBreakContext context)
    {
        ArgumentNullException.ThrowIfNull(candidateEmployeeIds);
        ArgumentNullException.ThrowIfNull(context);

        return candidateEmployeeIds
            .OrderBy(id => context.Ledger.PreferredPlacements(id))
            .ThenBy(id => context.Ledger.TotalPlacements(id))
            // Final, arbitrary, and above all stable. Without it two people with identical
            // records could swap places between runs and the roster would stop reproducing.
            .ThenBy(id => id)
            .ToList();
    }
}
