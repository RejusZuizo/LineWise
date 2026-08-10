using System.Collections.ObjectModel;

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
        People = new ObservableCollection<PersonChipViewModel>(
            people
                .OrderByDescending(person => person.IsLeader)
                .ThenBy(person => person.DisplayName, StringComparer.CurrentCulture));
    }

    public DateOnly Date { get; }

    public ObservableCollection<PersonChipViewModel> People { get; }

    public int Assigned => People.Count;

    public int Required { get; }

    /// <summary>Reads "3 of 4". Always both numbers, so short is visible without arithmetic.</summary>
    public string Headcount => $"{Assigned} of {Required}";

    /// <summary>
    /// Below what the line asked for. Paired with the numbers rather than replacing them:
    /// a cell that is short says so in words as well as in styling.
    /// </summary>
    public bool IsShort => Assigned < Required;

    public bool HasNoLeader => People.Count > 0 && !People.Any(person => person.IsLeader);

    public bool IsEmpty => People.Count == 0;
}
