namespace Linewise.Domain.Entities;

/// <summary>
/// What one line is doing on one date: how many people it needs, or nothing at all.
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

    /// <summary>
    /// The line is not running that day. Nobody is placed on it and nothing is warned about
    /// it, because a line that is not running is not short of people.
    /// </summary>
    /// <remarks>
    /// A flag rather than a headcount of zero. The two would look identical in the store and
    /// mean opposite things to the reader: zero is already a configuration fault the
    /// validator reports, since a line that always needs nobody can never be filled, and a
    /// cell drawn empty for a closed line would be indistinguishable from one that failed to
    /// find anybody.
    /// <para>
    /// The headcount is kept rather than cleared, so reopening the day restores what the
    /// line was set to run at rather than resetting it to standard.
    /// </para>
    /// </remarks>
    public bool IsClosed { get; init; }
}
