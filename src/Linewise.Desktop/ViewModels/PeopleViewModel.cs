using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Persistence;
using Linewise.Application.Validation;
using Linewise.Desktop.Resources;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Serilog;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The highest rank a person is permitted to hold. Ordered so a list sorts by it.
/// </summary>
public enum WorkerCapability
{
    LineLeader = 0,

    OperatingAssistant = 1,

    LineWorker = 2,
}

/// <summary>
/// Who works where: the lines a person works in priority order, and who may lead or assist
/// on each of them.
/// </summary>
/// <remarks>
/// Sixty people in one flat list is a wall of names. They are grouped by what each person is
/// permitted to be, because "who can lead Ovens" is the question this screen is opened to
/// answer and a leader is what a short line most often needs.
/// <para>
/// Priority is the order of a list rather than a number somebody types. A rank that is
/// derived from position cannot be duplicated, cannot skip, and cannot disagree with what is
/// on screen.
/// </para>
/// </remarks>
public sealed partial class PeopleViewModel : ObservableObject
{
    private readonly IConfigurationRepository? _configuration;
    private readonly IRosterRuleValidator? _validator;

    [ObservableProperty]
    private EmployeeViewModel? _selected;

    /// <summary>
    /// Whatever the tree has selected, which may be a group heading rather than a person.
    /// Selecting a heading collapses or expands it and leaves the person on screen alone,
    /// because losing the rules you were editing by clicking a heading would be its own bug.
    /// </summary>
    [ObservableProperty]
    private object? _selectedNode;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _search = string.Empty;

    public PeopleViewModel(IConfigurationRepository configuration, IRosterRuleValidator validator)
    {
        _configuration = configuration;
        _validator = validator;
    }

    /// <summary>For the Avalonia designer, which cannot resolve from the container.</summary>
    public PeopleViewModel()
    {
    }

    public ObservableCollection<EmployeeViewModel> People { get; } = [];

    /// <summary>Everybody matching the search, flat. What the groups are built from.</summary>
    public ObservableCollection<EmployeeViewModel> Visible { get; } = [];

    /// <summary>
    /// The list as it is drawn: one collapsible group per capability, in rank order.
    /// </summary>
    public ObservableCollection<PeopleGroupViewModel> Groups { get; } = [];

    /// <summary>
    /// Combinations of rules no roster could satisfy. Two people both required on a one
    /// slot line, somebody blocked from everywhere, a line nobody may lead.
    /// </summary>
    public ObservableCollection<string> Impossibilities { get; } = [];

    public bool HasBeenChecked { get; private set; }

    public bool IsPossible => HasBeenChecked && Impossibilities.Count == 0;

    public bool HasImpossibilities => Impossibilities.Count > 0;

    public bool HasSelection => Selected is not null;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration is null)
        {
            return;
        }

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);

        People.Clear();

        foreach (var employee in configuration.Employees
            .Where(employee => employee.IsActive)
            .OrderBy(employee => employee.FullName, StringComparer.CurrentCulture))
        {
            People.Add(new EmployeeViewModel(employee, configuration));
        }

        ApplySearch();

        Selected = Visible.FirstOrDefault();
    }

    partial void OnSearchChanged(string value) => ApplySearch();

    partial void OnSelectedChanged(EmployeeViewModel? value) => OnPropertyChanged(nameof(HasSelection));

    /// <summary>
    /// A heading is a legitimate thing to click and is not a person. Ignoring it here is
    /// what keeps the editor on the right of the screen from emptying itself.
    /// </summary>
    partial void OnSelectedNodeChanged(object? value)
    {
        if (value is EmployeeViewModel person)
        {
            Selected = person;
        }
    }

    /// <summary>
    /// Filtering rather than paging. A hundred and fifty people is a long list and a short
    /// search, and somebody looking for one person knows their name.
    /// </summary>
    private void ApplySearch()
    {
        Visible.Clear();

        foreach (var person in People
            .Where(person => string.IsNullOrWhiteSpace(Search)
                || person.Name.Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(person => (int)person.Capability)
            .ThenBy(person => person.Name, StringComparer.CurrentCulture))
        {
            Visible.Add(person);
        }

        BuildGroups();

        LeaderCount = People.Count(person => person.Capability == WorkerCapability.LineLeader);
        AssistantCount = People.Count(person => person.Capability == WorkerCapability.OperatingAssistant);
        WorkerCount = People.Count(person => person.Capability == WorkerCapability.LineWorker);

        OnPropertyChanged(nameof(LeaderCount));
        OnPropertyChanged(nameof(AssistantCount));
        OnPropertyChanged(nameof(WorkerCount));
        OnPropertyChanged(nameof(CapabilityBreakdown));
    }

    /// <summary>
    /// One group per capability, and an empty one is not drawn.
    /// </summary>
    /// <remarks>
    /// Agency and temporary staff are a badge on the row rather than a fourth group. A
    /// temporary worker who is permitted to lead is exactly who a manager is hunting for
    /// when a line has lost its leader, and a Temporary group would have taken them out of
    /// the Line leaders group to say something the badge already says.
    /// </remarks>
    private void BuildGroups()
    {
        Groups.Clear();

        foreach (var capability in (WorkerCapability[])[
            WorkerCapability.LineLeader,
            WorkerCapability.OperatingAssistant,
            WorkerCapability.LineWorker])
        {
            var members = Visible.Where(person => person.Capability == capability).ToList();

            if (members.Count > 0)
            {
                Groups.Add(new PeopleGroupViewModel(capability, members));
            }
        }
    }

    public int LeaderCount { get; private set; }

    public int AssistantCount { get; private set; }

    public int WorkerCount { get; private set; }

    /// <summary>
    /// How the workforce is made up. The number that matters is the leaders: a factory with
    /// four lines and two people permitted to lead them cannot cover a sick call.
    /// </summary>
    public string CapabilityBreakdown =>
        Strings.CapabilityBreakdown(LeaderCount, AssistantCount, WorkerCount);

    /// <summary>
    /// Saves the selected person's rules. Preferences are replaced wholesale rather than
    /// merged: a ranked list is edited as a list, and replacing it leaves no orphaned rank
    /// behind when somebody drops a line from the middle of it.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_configuration is null || Selected is null)
        {
            return;
        }

        var person = Selected;

        await _configuration.ReplacePreferencesAsync(
            person.Id,
            person.ToPreferences(),
            cancellationToken).ConfigureAwait(true);

        await _configuration.ReplaceLeaderEligibilityAsync(
            person.Id,
            person.ToLeaderEligibilities(),
            cancellationToken).ConfigureAwait(true);

        await _configuration.ReplaceOperatingAssistantEligibilityAsync(
            person.Id,
            person.ToAssistantEligibilities(),
            cancellationToken).ConfigureAwait(true);

        // Counts, never names. The redaction rule applies to a log line about a person as
        // much as to a log line containing one.
        Log.Information(
            "Saved rules for {EmployeeId}: {Preferences} preferences, {Leads} lines led, {Assists} assisted.",
            person.Id,
            person.ToPreferences().Count,
            person.ToLeaderEligibilities().Count,
            person.ToAssistantEligibilities().Count);

        Status = Strings.PeopleSaved(person.Name);

        // Permission changed, so which group this person belongs in changed with it.
        ApplySearch();
        Selected = person;

        // Checked on every save rather than only on demand. A contradiction entered thirty
        // seconds ago is one somebody can still explain; the same contradiction found at
        // generate on Monday is a puzzle.
        await CheckRulesAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Runs the phase 1 validator over the whole configuration.
    /// </summary>
    /// <remarks>
    /// The validator has existed since the engine did and nothing has ever called it from
    /// the application. Finding out on Monday morning that a line requires a skill nobody
    /// holds is a great deal worse than being told on the day the rule was written, which
    /// is the sentence written above its own interface and was true of nothing until now.
    /// </remarks>
    [RelayCommand]
    private async Task CheckRulesAsync(CancellationToken cancellationToken)
    {
        if (_configuration is null || _validator is null)
        {
            return;
        }

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);
        var issues = _validator.Validate(configuration);

        Impossibilities.Clear();

        foreach (var issue in issues)
        {
            Impossibilities.Add(issue.Message);
        }

        HasBeenChecked = true;

        OnPropertyChanged(nameof(HasBeenChecked));
        OnPropertyChanged(nameof(IsPossible));
        OnPropertyChanged(nameof(HasImpossibilities));

        Log.Information("Checked the configuration: {Issues} impossible combinations.", issues.Count);
    }
}

/// <summary>One heading in the people list, and everybody under it.</summary>
public sealed class PeopleGroupViewModel
{
    public PeopleGroupViewModel(WorkerCapability capability, IReadOnlyList<EmployeeViewModel> people)
    {
        ArgumentNullException.ThrowIfNull(people);

        Capability = capability;
        People = new ObservableCollection<EmployeeViewModel>(people);
    }

    public WorkerCapability Capability { get; }

    public ObservableCollection<EmployeeViewModel> People { get; }

    public int Count => People.Count;

    /// <summary>Singular and plural are separate resources, never an "s" added in code.</summary>
    public string Title => Capability switch
    {
        WorkerCapability.LineLeader => Strings.GroupLineLeaders,
        WorkerCapability.OperatingAssistant => Strings.GroupOperatingAssistants,
        _ => Strings.GroupLineWorkers,
    };

    public string CountLabel => Strings.GroupCount(Count);
}

/// <summary>One person, the lines they work in order, and the lines they never work.</summary>
public sealed partial class EmployeeViewModel : ObservableObject
{
    private readonly IReadOnlyList<ProductionLine> _allLines;

    public EmployeeViewModel(Employee employee, Application.Rostering.RosterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(configuration);

        Id = employee.Id;
        Name = employee.FullName;
        IsTemporary = employee.IsTemporary;

        _allLines = [.. configuration.Lines.OrderBy(line => line.DisplayOrder)];

        var preferences = configuration.Preferences
            .Where(preference => preference.EmployeeId == employee.Id)
            .ToList();

        bool CanLead(Guid lineId) => configuration.LeaderEligibilities
            .Any(eligibility => eligibility.EmployeeId == employee.Id && eligibility.LineId == lineId);

        bool CanAssist(Guid lineId) => configuration.OperatingAssistantEligibilities
            .Any(eligibility => eligibility.EmployeeId == employee.Id && eligibility.LineId == lineId);

        Blocked = new ObservableCollection<EmployeeLineRuleViewModel>(
            _allLines
                .Where(line => preferences.Any(preference =>
                    preference.LineId == line.Id && preference.Type == PreferenceType.Blocked))
                .Select(line => new EmployeeLineRuleViewModel(this, line, false, false, false)));

        // A line belongs in the worked list if there is a preference for it, and also if
        // this person is merely permitted to lead or assist on it. The second case is what
        // carries the old screen's data across: eligibility used to be settable on a line
        // nobody had an opinion about, and dropping those rows would quietly take away
        // permissions somebody had already granted.
        var worked = _allLines
            .Where(line => Blocked.All(blocked => blocked.LineId != line.Id))
            .Where(line => preferences.Any(preference => preference.LineId == line.Id)
                || CanLead(line.Id)
                || CanAssist(line.Id))
            .OrderBy(line => preferences.FirstOrDefault(preference => preference.LineId == line.Id)?.Rank
                ?? int.MaxValue)
            .ThenBy(line => line.DisplayOrder)
            .ToList();

        Works = new ObservableCollection<EmployeeLineRuleViewModel>(
            worked.Select(line => new EmployeeLineRuleViewModel(
                this,
                line,
                preferences.Any(preference =>
                    preference.LineId == line.Id && preference.Type == PreferenceType.Mandatory),
                CanLead(line.Id),
                CanAssist(line.Id))));

        Addable = [];
        Changed();
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsTemporary { get; }

    /// <summary>
    /// The lines this person works, best first. Position is the priority: the first entry is
    /// their first choice, and nothing anywhere stores a rank the screen could disagree with.
    /// </summary>
    public ObservableCollection<EmployeeLineRuleViewModel> Works { get; }

    /// <summary>
    /// Lines they are never put on. Kept apart from the ordered list rather than being a
    /// fourth state within it, because a refusal has no priority and ordering one would be
    /// meaningless.
    /// </summary>
    public ObservableCollection<EmployeeLineRuleViewModel> Blocked { get; }

    /// <summary>Lines in neither list, offered by the two add buttons.</summary>
    public ObservableCollection<ProductionLine> Addable { get; }

    public bool HasWorks => Works.Count > 0;

    public bool HasBlocked => Blocked.Count > 0;

    public bool HasAddable => Addable.Count > 0;

    /// <summary>
    /// What this person is permitted to be, at their highest rank.
    /// </summary>
    /// <remarks>
    /// A role in this product belongs to an assignment, not to a person: leadership is
    /// chosen per day, which is why a leader still counts toward the line's headcount. What
    /// belongs to the person is the permission, and that is what a list of people can
    /// honestly be organised by.
    /// <para>
    /// Somebody who may lead is listed as a leader even if they are also permitted to
    /// assist, because the highest thing they can be is the useful answer when a line is
    /// short of one.
    /// </para>
    /// </remarks>
    public WorkerCapability Capability =>
        Works.Any(line => line.CanLead) ? WorkerCapability.LineLeader
        : Works.Any(line => line.CanAssist) ? WorkerCapability.OperatingAssistant
        : WorkerCapability.LineWorker;

    public string CapabilityLabel => Capability switch
    {
        WorkerCapability.LineLeader => Strings.RoleLeader,
        WorkerCapability.OperatingAssistant => Strings.RoleOperatingAssistant,
        _ => Strings.RoleLineWorker,
    };

    /// <summary>Which lines they may lead, so the list answers "lead what" as well as "can lead".</summary>
    public string CapabilityDetail
    {
        get
        {
            var leads = Works.Where(line => line.CanLead).Select(line => line.LineName).ToList();
            var assists = Works.Where(line => line.CanAssist).Select(line => line.LineName).ToList();

            return leads.Count > 0
                ? string.Join(", ", leads)
                : assists.Count > 0 ? string.Join(", ", assists) : string.Empty;
        }
    }

    /// <summary>A short answer for the list, so the rules are visible without selecting.</summary>
    public string Summary
    {
        get
        {
            var mandatory = Works.Count(line => line.IsMandatory);

            return Strings.PeopleSummary(Works.Count - mandatory, mandatory, Blocked.Count);
        }
    }

    /// <summary>
    /// Adds a line to the end of the worked list. The end rather than the top: a line just
    /// added is the one they have least call on, and promoting it is a drag away.
    /// </summary>
    [RelayCommand]
    public void AddWorked(ProductionLine? line)
    {
        if (line is null || Works.Any(row => row.LineId == line.Id))
        {
            return;
        }

        Works.Add(new EmployeeLineRuleViewModel(this, line, false, false, false));
        Changed();
    }

    [RelayCommand]
    public void AddBlocked(ProductionLine? line)
    {
        if (line is null || Blocked.Any(row => row.LineId == line.Id))
        {
            return;
        }

        Blocked.Add(new EmployeeLineRuleViewModel(this, line, false, false, false));
        Changed();
    }

    /// <summary>Takes a line out of whichever list holds it, and offers it again.</summary>
    public void Remove(EmployeeLineRuleViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Works.Remove(row);
        Blocked.Remove(row);
        Changed();
    }

    /// <summary>
    /// Moves a worked line to a new position. The one operation drag and the two buttons
    /// both go through, so a mouse and a keyboard cannot produce different orders.
    /// </summary>
    public void Move(EmployeeLineRuleViewModel row, int to)
    {
        ArgumentNullException.ThrowIfNull(row);

        var from = Works.IndexOf(row);

        if (from < 0 || to < 0 || to >= Works.Count || from == to)
        {
            return;
        }

        Works.Move(from, to);
        Changed();
    }

    public void MoveUp(EmployeeLineRuleViewModel row) => Move(row, Works.IndexOf(row) - 1);

    public void MoveDown(EmployeeLineRuleViewModel row) => Move(row, Works.IndexOf(row) + 1);

    /// <summary>
    /// Rank is the position in the list, counted from one. Nothing stores it, so it cannot
    /// be duplicated, cannot skip a number, and cannot disagree with what is on screen.
    /// </summary>
    public IReadOnlyList<LinePreference> ToPreferences() =>
    [
        .. Works.Select((row, index) => new LinePreference
        {
            EmployeeId = Id,
            LineId = row.LineId,
            Rank = index + 1,
            Type = row.IsMandatory ? PreferenceType.Mandatory : PreferenceType.Preferred,
        }),

        // A refusal has no rank. Zero rather than one, so nothing reads it as a first choice
        // if the ordering rules are ever changed underneath it.
        .. Blocked.Select(row => new LinePreference
        {
            EmployeeId = Id,
            LineId = row.LineId,
            Rank = 0,
            Type = PreferenceType.Blocked,
        }),
    ];

    public IReadOnlyList<LeaderEligibility> ToLeaderEligibilities() =>
    [
        .. Works
            .Where(row => row.CanLead)
            .Select(row => new LeaderEligibility { EmployeeId = Id, LineId = row.LineId }),
    ];

    public IReadOnlyList<OperatingAssistantEligibility> ToAssistantEligibilities() =>
    [
        .. Works
            .Where(row => row.CanAssist)
            .Select(row => new OperatingAssistantEligibility { EmployeeId = Id, LineId = row.LineId }),
    ];

    /// <summary>Everything derived from the two lists, told to redraw.</summary>
    internal void Changed()
    {
        RefreshAddable();

        for (var index = 0; index < Works.Count; index++)
        {
            Works[index].SetPosition(index + 1, index == 0, index == Works.Count - 1);
        }

        OnPropertyChanged(nameof(HasWorks));
        OnPropertyChanged(nameof(HasBlocked));
        OnPropertyChanged(nameof(HasAddable));
        OnPropertyChanged(nameof(Capability));
        OnPropertyChanged(nameof(CapabilityLabel));
        OnPropertyChanged(nameof(CapabilityDetail));
        OnPropertyChanged(nameof(Summary));
    }

    private void RefreshAddable()
    {
        Addable.Clear();

        foreach (var line in _allLines
            .Where(line => Works.All(row => row.LineId != line.Id))
            .Where(line => Blocked.All(row => row.LineId != line.Id)))
        {
            Addable.Add(line);
        }
    }
}

/// <summary>
/// A row that knows where it sits in a list and can be told to sit somewhere else.
/// </summary>
/// <remarks>
/// Exists so the drag behaviour in the view can reorder a list without knowing what the
/// list holds or who owns it. The row already knows both.
/// </remarks>
public interface IReorderableRow
{
    void MoveTo(int index);
}

/// <summary>One line in a person's list, worked or refused.</summary>
public sealed partial class EmployeeLineRuleViewModel : ObservableObject, IReorderableRow
{
    private readonly EmployeeViewModel _owner;

    /// <summary>
    /// They must be on this line, not merely prefer it. A flag on the row rather than a
    /// separate list: a requirement still has a priority relative to the rest, and the
    /// engine may still break it when somebody is on overtime.
    /// </summary>
    [ObservableProperty]
    private bool _isMandatory;

    [ObservableProperty]
    private bool _canLead;

    [ObservableProperty]
    private bool _canAssist;

    [ObservableProperty]
    private int _position;

    [ObservableProperty]
    private bool _isFirst;

    [ObservableProperty]
    private bool _isLast;

    public EmployeeLineRuleViewModel(
        EmployeeViewModel owner,
        ProductionLine line,
        bool isMandatory,
        bool canLead,
        bool canAssist)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(line);

        _owner = owner;
        LineId = line.Id;
        LineName = line.Name;
        AccentColour = line.AccentColour;

        _isMandatory = isMandatory;
        _canLead = canLead;
        _canAssist = canAssist;
    }

    public Guid LineId { get; }

    public string LineName { get; }

    public string AccentColour { get; }

    /// <summary>Reads "1st choice". The number is derived, never entered.</summary>
    public string PositionLabel => Strings.PeopleChoice(Position);

    /// <summary>Where a drag drops this row. The same path the arrows take.</summary>
    public void MoveTo(int index) => _owner.Move(this, index);

    [RelayCommand]
    private void MoveUp() => _owner.MoveUp(this);

    [RelayCommand]
    private void MoveDown() => _owner.MoveDown(this);

    [RelayCommand]
    private void Remove() => _owner.Remove(this);

    internal void SetPosition(int position, bool isFirst, bool isLast)
    {
        Position = position;
        IsFirst = isFirst;
        IsLast = isLast;

        OnPropertyChanged(nameof(PositionLabel));
    }

    partial void OnIsMandatoryChanged(bool value) => _owner.Changed();

    partial void OnCanLeadChanged(bool value) => _owner.Changed();

    partial void OnCanAssistChanged(bool value) => _owner.Changed();
}
