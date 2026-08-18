namespace Linewise.Application.Rostering;

/// <summary>
/// Tops a stored week up without disturbing it: fills the places the rules can now fill,
/// and moves nobody who is already standing somewhere.
/// </summary>
/// <remarks>
/// The case this exists for: a line has no leader, somebody is made eligible to lead it,
/// and the roster should say so without anybody pressing generate. Pressing generate would
/// also fix it, and would reshuffle a week that has already been reviewed to do it.
/// <para>
/// Only ever adds. Nobody already placed is moved, nothing is removed, and a person already
/// on another line is not taken off it — filling one hole by opening another is a decision
/// for the manager to make deliberately, through the replacement picker, which names the
/// line it would empty.
/// </para>
/// </remarks>
public interface IGapFiller
{
    /// <summary>Fills what can be filled, and says how many places that was.</summary>
    Task<int> FillAsync(DateOnly weekStart, CancellationToken cancellationToken = default);
}
