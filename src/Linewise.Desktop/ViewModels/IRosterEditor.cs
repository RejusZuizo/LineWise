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
}
