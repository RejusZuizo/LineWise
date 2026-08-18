using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Rostering;
using Linewise.Desktop.Resources;

namespace Linewise.Desktop.ViewModels;

/// <summary>One line on one day: who is on it, and whether that is enough.</summary>
public sealed partial class RosterCellViewModel : ObservableObject
{
    private readonly IRosterEditor? _editor;

    public RosterCellViewModel(
        DateOnly date,
        IEnumerable<PersonChipViewModel> people,
        int required,
        Guid lineId = default,
        bool isClosed = false,
        IRosterEditor? editor = null)
    {
        ArgumentNullException.ThrowIfNull(people);

        Date = date;
        Required = required;
        LineId = lineId;
        IsClosed = isClosed;

        _editor = editor;

        ToggleClosedCommand = new AsyncRelayCommand(
            () => editor is null
                ? Task.CompletedTask
                : editor.SetLineClosedAsync(lineId, date, !isClosed));

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

    public Guid LineId { get; }

    /// <summary>
    /// The line is not running today. Not the same as empty: a line nobody could be found
    /// for and a line nobody was wanted on are different problems, and only one of them is
    /// a problem at all.
    /// </summary>
    public bool IsClosed { get; }

    public string ToggleClosedLabel => IsClosed ? Strings.ReopenLine : Strings.CloseLine;

    public IAsyncRelayCommand ToggleClosedCommand { get; }

    /// <summary>
    /// Who could fill a place here, best first. Loaded when the picker is opened rather than
    /// with the grid: a week is a hundred and forty cells and none of them needs this until
    /// somebody asks.
    /// </summary>
    public ObservableCollection<ReplacementCandidateViewModel> Candidates { get; } = [];

    [ObservableProperty]
    private bool _isPickingReplacement;

    /// <summary>
    /// Somewhere to put somebody. Offered on a short line rather than on every cell, because
    /// a full line does not need filling and a button on it is one more thing to read past.
    /// </summary>
    public bool CanFill => IsShort && !IsClosed;

    [RelayCommand]
    private async Task FindReplacementsAsync()
    {
        if (_editor is null)
        {
            return;
        }

        Candidates.Clear();

        foreach (var candidate in await _editor.FindReplacementsAsync(LineId, Date).ConfigureAwait(true))
        {
            Candidates.Add(new ReplacementCandidateViewModel(candidate, this));
        }

        IsPickingReplacement = true;
    }

    internal async Task PlaceAsync(Guid employeeId)
    {
        IsPickingReplacement = false;

        if (_editor is not null)
        {
            await _editor.PlaceAsync(employeeId, LineId, Date).ConfigureAwait(true);
        }
    }

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
    /// <summary>
    /// Reads "3 of 4", or says the line is shut. The word rather than a blank or a dash: a
    /// cell that simply went empty would read as a line nobody could be found for.
    /// </summary>
    public string Headcount => IsClosed ? Strings.CellClosed : Strings.Headcount(Assigned, Required);

    /// <summary>
    /// Below what the line asked for. Paired with the numbers rather than replacing them:
    /// a cell that is short says so in words as well as in styling.
    /// </summary>
    public bool IsShort => !IsClosed && Assigned < Required;

    /// <summary>
    /// Nobody on the line to ask. An absent leader counts as no leader, because a name on a
    /// sheet is not somebody standing on the line, and this is the first thing a manager
    /// needs to know when the phone call is from the person who runs it.
    /// </summary>
    public bool HasNoLeader =>
        !IsClosed && People.Count > 0 && !People.Any(person => person.IsLeader && !person.IsAbsent);

    public bool IsEmpty => People.Count == 0;
}
