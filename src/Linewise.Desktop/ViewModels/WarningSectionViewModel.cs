using System.Collections.ObjectModel;
using Linewise.Desktop.Resources;
using Linewise.Domain.Enums;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// One heading in the warnings panel, and the rows under it.
/// </summary>
/// <remarks>
/// Grouping by kind and day already turned two hundred warnings into twenty one rows, and
/// twenty one rows stacked as cards is still a wall. The two severities are different
/// questions — what stops a line running, and what is merely worth knowing — so they are
/// two sections rather than one list sorted by severity.
/// <para>
/// Errors open, notices shut. A week where thirty people are spare produces a hundred and
/// thirty notices that are all true and none of them urgent, and they should not be the
/// first thing between the manager and the eight rows that matter.
/// </para>
/// </remarks>
public sealed class WarningSectionViewModel
{
    public WarningSectionViewModel(WarningSeverity severity, IReadOnlyList<WarningGroupViewModel> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        Severity = severity;
        Groups = new ObservableCollection<WarningGroupViewModel>(groups);
        Total = groups.Sum(group => group.Count);
    }

    public WarningSeverity Severity { get; }

    public ObservableCollection<WarningGroupViewModel> Groups { get; }

    public bool IsError => Severity == WarningSeverity.Error;

    /// <summary>How many warnings this stands for, not how many rows it draws.</summary>
    public int Total { get; }

    /// <summary>Errors are open on arrival. Notices are not.</summary>
    public bool IsExpanded => IsError;

    public string Title => IsError ? Strings.ErrorCount(Total) : Strings.NoticeCount(Total);

    /// <summary>
    /// Says how many rows are folded away, so a shut section is obviously hiding something
    /// rather than obviously empty.
    /// </summary>
    public string RowsLabel => Strings.WarningRows(Groups.Count);
}
