namespace Linewise.Application.Rostering;

/// <summary>
/// Decides who wins when several people want the same place at the same preference rank.
/// </summary>
/// <remarks>
/// Injected rather than built in, because this is the rule most likely to be argued about.
/// Fairness ships as the default; seniority is the obvious alternative and needs no change
/// to the engine.
/// </remarks>
public interface ITieBreakStrategy
{
    /// <summary>
    /// Orders candidates, first in line first. Must return a total order: two runs over the
    /// same input have to produce the same sequence, or the roster stops being reproducible.
    /// </summary>
    IReadOnlyList<Guid> Prioritise(IReadOnlyCollection<Guid> candidateEmployeeIds, TieBreakContext context);
}
