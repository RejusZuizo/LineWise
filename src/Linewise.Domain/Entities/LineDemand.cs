namespace Linewise.Domain.Entities;

/// <summary>
/// Raises a line's headcount for one date.
/// </summary>
/// <remarks>
/// How the manager says a line has more product to get out than usual. It does two jobs at
/// once: the line needs more people, and it is where overtime should go. Keeping it as one
/// number rather than a headcount and a separate "overtime authorised" flag means the two
/// can never contradict each other.
/// </remarks>
public sealed record LineDemand
{
    public required Guid LineId { get; init; }

    public required DateOnly Date { get; init; }

    /// <summary>
    /// What the line needs that day, replacing its standard headcount. Lower than standard
    /// is allowed: a line can be quiet as well as busy.
    /// </summary>
    public required int RequiredHeadcount { get; init; }
}
