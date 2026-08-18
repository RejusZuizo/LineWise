using Linewise.Application.Rostering;
using Linewise.Domain.Enums;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// What a chip in the grid can ask the shell to do about the person on it.
/// </summary>
/// <remarks>
/// The chip holds its own commands rather than reaching up the tree for the window's,
/// because a context menu opens in a popup outside the visual tree and an ancestor binding
/// into one is the kind of thing that works until it silently does not.
/// <para>
/// Behind an interface so the grid can be built and driven in a test without a window, and
/// so a chip knows what it may ask for rather than holding the shell entire.
/// </para>
/// </remarks>
public interface IRosterEditor
{
    Task MarkAbsentAsync(PersonChipViewModel chip, AbsenceReason reason);

    Task ClearAbsenceAsync(PersonChipViewModel chip);

    /// <summary>Shuts a line for one day, or puts it back into service.</summary>
    Task SetLineClosedAsync(Guid lineId, DateOnly date, bool closed);

    /// <summary>Who could take a place on this line today, best first.</summary>
    Task<IReadOnlyList<ReplacementCandidate>> FindReplacementsAsync(Guid lineId, DateOnly date);

    /// <summary>Puts somebody on the line, locked so a regenerate leaves them there.</summary>
    Task PlaceAsync(Guid employeeId, Guid lineId, DateOnly date);
}
