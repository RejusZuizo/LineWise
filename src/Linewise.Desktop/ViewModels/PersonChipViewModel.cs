using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// One person in one cell of the grid.
/// </summary>
/// <remarks>
/// A leader is marked three ways at once: the word, bold weight, and a filled block. That
/// is the same rule the printed sheet follows, for a different reason. On paper it is
/// photocopiers and monochrome printers; on screen it is that roughly eight percent of men
/// have a colour vision deficiency, and the spreadsheet this data comes from already uses
/// red and green, which is the worst possible pair.
/// </remarks>
public sealed class PersonChipViewModel
{
    public PersonChipViewModel(Assignment assignment, string displayName)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        DisplayName = displayName;
        IsLeader = assignment.Role == AssignmentRole.LineLeader;
        IsManual = assignment.Source == AssignmentSource.Manual;
        IsLocked = assignment.IsLocked;
        Explanation = assignment.Explanation.ToString();
    }

    public string DisplayName { get; }

    public bool IsLeader { get; }

    /// <summary>The word, which is the part that survives every kind of impairment.</summary>
    public string RoleLabel => IsLeader ? "Leader" : string.Empty;

    /// <summary>
    /// Placed by hand rather than by the engine. Shown from the first version of this grid
    /// even though editing does not arrive until later, because a roster that has been
    /// corrected and a roster that has not are different things to the person reading it.
    /// </summary>
    public bool IsManual { get; }

    public bool IsLocked { get; }

    /// <summary>
    /// Which rule placed this person and at what preference rank. The engine has recorded
    /// it since phase 1 and nothing has ever shown it; here it is a tooltip.
    /// </summary>
    public string Explanation { get; }
}
