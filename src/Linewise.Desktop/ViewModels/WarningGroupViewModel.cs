using Linewise.Desktop.Resources;
using Linewise.Domain.Enums;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// Warnings of the same kind on the same day, collapsed into one row.
/// </summary>
/// <remarks>
/// The engine raises one warning per person, which is right: each carries an identifier, and
/// the editing screens will need to know exactly who. Showing them that way is not right. A
/// week where thirty people are spare produces two hundred notices, and a panel with two
/// hundred rows in it is one nobody reads — which costs the manager the eight errors that
/// actually matter.
/// <para>
/// Nothing is hidden. The count is on the row, and grouping is by kind and day, so "22
/// people spare on Monday" is one line that says as much as twenty two did.
/// </para>
/// </remarks>
public sealed class WarningGroupViewModel
{
    public WarningGroupViewModel(IReadOnlyList<WarningViewModel> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        var first = warnings[0];

        Severity = first.Severity;
        Code = first.Code;
        Context = first.Context;
        Count = warnings.Count;

        // A single warning keeps its own words, which name the line and the shortfall. A
        // group cannot: the messages differ per person, and stitching them together would
        // produce a paragraph rather than a summary.
        Message = Count == 1 ? first.Message : Strings.WarningGrouped(Count, first.Message);
    }

    public WarningSeverity Severity { get; }

    public WarningCode Code { get; }

    public string Message { get; }

    public string Context { get; }

    public int Count { get; }

    public bool IsError => Severity == WarningSeverity.Error;

    public bool IsGrouped => Count > 1;

    public string CountLabel => Strings.WarningCount(Count);

    public string SeverityLabel => IsError ? Strings.SeverityError : Strings.SeverityNotice;
}
