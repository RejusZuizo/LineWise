using CommunityToolkit.Mvvm.Input;
using Linewise.Desktop.Resources;
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
    public PersonChipViewModel(
        Assignment assignment,
        string displayName,
        bool isAbsent = false,
        IRosterEditor? editor = null)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        MarkAbsentCommand = new AsyncRelayCommand<AbsenceReason?>(
            reason => editor is null || reason is null
                ? Task.CompletedTask
                : editor.MarkAbsentAsync(this, reason.Value));

        ClearAbsenceCommand = new AsyncRelayCommand(
            () => editor is null ? Task.CompletedTask : editor.ClearAbsenceAsync(this));

        DisplayName = displayName;
        EmployeeId = assignment.EmployeeId;
        Date = assignment.Date;
        LineId = assignment.LineId;
        IsLeader = assignment.Role == AssignmentRole.LineLeader;
        IsOperatingAssistant = assignment.Role == AssignmentRole.OperatingAssistant;
        IsManual = assignment.Source == AssignmentSource.Manual;
        IsLocked = assignment.IsLocked;
        IsAbsent = isAbsent;
        Explanation = assignment.Explanation.ToString();
    }

    /// <summary>
    /// Takes this person out of the day. The reason arrives as the command parameter, so
    /// the whole picker is four menu items bound to one command.
    /// </summary>
    public IAsyncRelayCommand<AbsenceReason?> MarkAbsentCommand { get; }

    /// <summary>Puts them back, for somebody who rang in and then turned up.</summary>
    public IAsyncRelayCommand ClearAbsenceCommand { get; }

    public string DisplayName { get; }

    /// <summary>Who and when, so a command on the chip knows what it is acting on.</summary>
    public Guid EmployeeId { get; }

    public DateOnly Date { get; }

    public Guid LineId { get; }

    public bool IsLeader { get; }

    public bool IsOperatingAssistant { get; }

    /// <summary>A rank of some kind, so the chip is drawn differently from a line worker.</summary>
    public bool IsRanked => IsLeader || IsOperatingAssistant;

    /// <summary>
    /// The word, which is the part that survives every kind of impairment.
    /// </summary>
    /// <remarks>
    /// Each rank now has a colour as well, which is a genuine improvement for the ninety
    /// odd percent of people who can use it. The word stays because roughly eight percent of
    /// men cannot, and because this same data is printed on a monochrome laser and stuck to
    /// a wall. Colour is an additional channel here, never the only one. ADR 0010.
    /// </remarks>
    public string RoleLabel => IsLeader
        ? Strings.RoleLeader
        : IsOperatingAssistant ? Strings.RoleOperatingAssistant : string.Empty;

    /// <summary>
    /// Placed by hand rather than by the engine. Shown from the first version of this grid
    /// even though editing does not arrive until later, because a roster that has been
    /// corrected and a roster that has not are different things to the person reading it.
    /// </summary>
    public bool IsManual { get; }

    public bool IsLocked { get; }

    /// <summary>
    /// Marked absent for this day. The name stays on the line rather than disappearing,
    /// because "who should have been on Ovens this morning" is a question the manager asks
    /// all day and a missing chip cannot answer it.
    /// </summary>
    public bool IsAbsent { get; }

    /// <summary>
    /// The word, for the same reason the leader has one. Absence is greyed, struck through
    /// and labelled: three channels, because a chip that is only a paler shade of the
    /// chip beside it is a chip that gets read as present.
    /// </summary>
    public string AbsenceLabel => IsAbsent ? Strings.RoleAbsent : string.Empty;

    /// <summary>
    /// Which rule placed this person and at what preference rank. The engine has recorded
    /// it since phase 1 and nothing has ever shown it; here it is a tooltip.
    /// </summary>
    public string Explanation { get; }
}
