using System.Collections.ObjectModel;
using Linewise.Domain.Entities;

namespace Linewise.Desktop.ViewModels;

/// <summary>One production line across the whole week.</summary>
public sealed class LineRowViewModel
{
    public LineRowViewModel(ProductionLine line, IEnumerable<RosterCellViewModel> cells)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(cells);

        Name = line.Name;
        AccentColour = line.AccentColour;
        RequiredHeadcount = line.RequiredHeadcount;
        Cells = new ObservableCollection<RosterCellViewModel>(cells);
    }

    public string Name { get; }

    /// <summary>
    /// The line's one saturated colour, used on the row header beside the name and nowhere
    /// else. It distinguishes rows at a glance; the name is what carries the meaning.
    /// </summary>
    public string AccentColour { get; }

    public int RequiredHeadcount { get; }

    public ObservableCollection<RosterCellViewModel> Cells { get; }

    /// <summary>How many days this week the line is short. Read at the end of the row.</summary>
    public int ShortDays => Cells.Count(cell => cell.IsShort);

    public bool IsShortAnyDay => ShortDays > 0;
}
