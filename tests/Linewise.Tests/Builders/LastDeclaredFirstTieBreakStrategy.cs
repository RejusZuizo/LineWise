using Linewise.Application.Rostering;

namespace Linewise.Tests.Builders;

/// <summary>
/// The opposite of the shipped strategy. Deliberately perverse: if swapping this in changes
/// who gets a place, the engine really is delegating the decision rather than deciding it.
/// </summary>
internal sealed class LastDeclaredFirstTieBreakStrategy : ITieBreakStrategy
{
    public IReadOnlyList<Guid> Prioritise(IReadOnlyCollection<Guid> candidateEmployeeIds, TieBreakContext context) =>
        candidateEmployeeIds.OrderByDescending(id => id).ToList();
}
