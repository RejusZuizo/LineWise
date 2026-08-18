using System.Collections.ObjectModel;
using Linewise.Desktop.Resources;

namespace Linewise.Desktop.ViewModels;

/// <summary>One line on one day: who is on it, and whether that is enough.</summary>
public sealed class RosterCellViewModel
{
    public RosterCellViewModel(DateOnly date, IEnumerable<PersonChipViewModel> people, int required)
    {
        ArgumentNullException.ThrowIfNull(people);

        Date = date;
        Required = required;

        // Leader first, then by name. The same order the printed sheet uses, so somebody
        // holding the paper and somebody at the screen are reading the same list.
        //
        // Absence does not reorder anything. A name that jumped to the bottom of the cell
        // the moment somebody rang in would be harder to find at exactly the point the
        // manager is looking for it.
        People = new ObservableCollection<PersonChipViewModel>(
            people
                .OrderByDescending(person => person.IsLeader)
                .ThenBy(person => person.DisplayName, StringComparer.CurrentCulture));
    }

    public DateOnly Date { get; }

    public ObservableCollection<PersonChipViewModel> People { get; }

    /// <summary>
    /// How many will actually be on the line. Somebody marked absent still has a chip, and
    /// is deliberately not counted here: the cell reading "5 of 6" the moment somebody
    /// rings in is the whole point of marking them absent.
    /// </summary>
    public int Assigned => People.Count(person => !person.IsAbsent);

    /// <summary>Rostered but not coming in. Zero on an ordinary day.</summary>
    public int Absent => People.Count(person => person.IsAbsent);

    public bool HasAbsences => Absent > 0;

    /// <summary>Reads "2 absent", beside the headcount rather than instead of it.</summary>
    public string AbsentLabel => Strings.CellAbsent(Absent);

    public int Required { get; }

    /// <summary>Reads "3 of 4". Always both numbers, so short is visible without arithmetic.</summary>
    public string Headcount => Strings.Headcount(Assigned, Required);

    /// <summary>
    /// Below what the line asked for. Paired with the numbers rather than replacing them:
    /// a cell that is short says so in words as well as in styling.
    /// </summary>
    public bool IsShort => Assigned < Required;

    /// <summary>
    /// Nobody on the line to ask. An absent leader counts as no leader, because a name on a
    /// sheet is not somebody standing on the line, and this is the first thing a manager
    /// needs to know when the phone call is from the person who runs it.
    /// </summary>
    public bool HasNoLeader =>
        People.Count > 0 && !People.Any(person => person.IsLeader && !person.IsAbsent);

    public bool IsEmpty => People.Count == 0;
}
