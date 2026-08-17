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
/// Who works where: ranked line preferences, and who may lead or assist on each line.
/// </summary>
/// <remarks>
/// The engine has honoured every one of these rules since phase 1 and there has never been
/// a way to enter them. Until now the only preferences in the product were the ones a test
/// constructed, which meant the engine's most interesting behaviour was unreachable from
/// the application.
/// </remarks>
public sealed partial class PeopleViewModel : ObservableObject
{
    private readonly IConfigurationRepository? _configuration;
    private readonly IRosterRuleValidator? _validator;

    [ObservableProperty]
    private EmployeeViewModel? _selected;

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

    public ObservableCollection<EmployeeViewModel> Visible { get; } = [];

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
    /// Filtering rather than paging. A hundred and fifty people is a long list and a short
    /// search, and somebody looking for one person knows their name.
    /// </summary>
    private void ApplySearch()
    {
        Visible.Clear();

        // Leaders first, then assistants, then line workers, alphabetical within each. A
        // manager looking for cover for a line that has lost its leader reads the top of
        // this list, which is the only reason to order it by anything but name.
        foreach (var person in People
            .Where(person => string.IsNullOrWhiteSpace(Search)
                || person.Name.Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(person => (int)person.Capability)
            .ThenBy(person => person.Name, StringComparer.CurrentCulture))
        {
            Visible.Add(person);
        }

        LeaderCount = People.Count(person => person.Capability == WorkerCapability.LineLeader);
        AssistantCount = People.Count(person => person.Capability == WorkerCapability.OperatingAssistant);
        WorkerCount = People.Count(person => person.Capability == WorkerCapability.LineWorker);

        OnPropertyChanged(nameof(LeaderCount));
        OnPropertyChanged(nameof(AssistantCount));
        OnPropertyChanged(nameof(WorkerCount));
        OnPropertyChanged(nameof(CapabilityBreakdown));
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

        // Permission changed, so where this person sits in the list changed with it.
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

/// <summary>One person, and their rule for every line.</summary>
public sealed partial class EmployeeViewModel : ObservableObject
{
    public EmployeeViewModel(Employee employee, Application.Rostering.RosterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(configuration);

        Id = employee.Id;
        Name = employee.FullName;
        IsTemporary = employee.IsTemporary;

        // A row per line, whether or not a rule exists for it. The alternative is a list
        // that only shows the lines somebody has already had an opinion about, which is
        // the shape that makes an unset preference invisible.
        Lines = new ObservableCollection<EmployeeLineRuleViewModel>(
            configuration.Lines
                .OrderBy(line => line.DisplayOrder)
                .Select(line => new EmployeeLineRuleViewModel(
                    line,
                    configuration.Preferences.FirstOrDefault(preference =>
                        preference.EmployeeId == employee.Id && preference.LineId == line.Id),
                    configuration.LeaderEligibilities.Any(eligibility =>
                        eligibility.EmployeeId == employee.Id && eligibility.LineId == line.Id),
                    configuration.OperatingAssistantEligibilities.Any(eligibility =>
                        eligibility.EmployeeId == employee.Id && eligibility.LineId == line.Id))));
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsTemporary { get; }

    public ObservableCollection<EmployeeLineRuleViewModel> Lines { get; }

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
        Lines.Any(line => line.CanLead) ? WorkerCapability.LineLeader
        : Lines.Any(line => line.CanAssist) ? WorkerCapability.OperatingAssistant
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
            var leads = Lines.Where(line => line.CanLead).Select(line => line.LineName).ToList();
            var assists = Lines.Where(line => line.CanAssist).Select(line => line.LineName).ToList();

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
            var mandatory = Lines.Count(line => line.IsMandatory);
            var blocked = Lines.Count(line => line.IsBlocked);
            var preferred = Lines.Count(line => line.IsPreferred);

            return Strings.PeopleSummary(preferred, mandatory, blocked);
        }
    }

    public IReadOnlyList<LinePreference> ToPreferences() =>
    [
        .. Lines
            .Where(line => line.Type is not null)
            .Select(line => new LinePreference
            {
                EmployeeId = Id,
                LineId = line.LineId,
                Rank = line.Rank,
                Type = line.Type!.Value,
            }),
    ];

    public IReadOnlyList<LeaderEligibility> ToLeaderEligibilities() =>
    [
        .. Lines
            .Where(line => line.CanLead)
            .Select(line => new LeaderEligibility { EmployeeId = Id, LineId = line.LineId }),
    ];

    public IReadOnlyList<OperatingAssistantEligibility> ToAssistantEligibilities() =>
    [
        .. Lines
            .Where(line => line.CanAssist)
            .Select(line => new OperatingAssistantEligibility { EmployeeId = Id, LineId = line.LineId }),
    ];
}

/// <summary>What one person's rule is for one line.</summary>
public sealed partial class EmployeeLineRuleViewModel : ObservableObject
{
    /// <summary>
    /// Null means no opinion, which is not the same as "preferred at rank zero". The engine
    /// treats an absent preference as freedom to place somebody anywhere, and that has to be
    /// expressible on the screen or every person acquires an opinion about every line.
    /// </summary>
    [ObservableProperty]
    private PreferenceType? _type;

    [ObservableProperty]
    private int _rank = 1;

    [ObservableProperty]
    private bool _canLead;

    [ObservableProperty]
    private bool _canAssist;

    public EmployeeLineRuleViewModel(
        ProductionLine line,
        LinePreference? preference,
        bool canLead,
        bool canAssist)
    {
        ArgumentNullException.ThrowIfNull(line);

        LineId = line.Id;
        LineName = line.Name;
        AccentColour = line.AccentColour;

        _type = preference?.Type;
        _rank = preference?.Rank ?? 1;
        _canLead = canLead;
        _canAssist = canAssist;
    }

    public Guid LineId { get; }

    public string LineName { get; }

    public string AccentColour { get; }

    public bool IsMandatory
    {
        get => Type == PreferenceType.Mandatory;
        set
        {
            if (value)
            {
                Type = PreferenceType.Mandatory;
            }
        }
    }

    public bool IsBlocked
    {
        get => Type == PreferenceType.Blocked;
        set
        {
            if (value)
            {
                Type = PreferenceType.Blocked;
            }
        }
    }

    public bool IsPreferred
    {
        get => Type == PreferenceType.Preferred;
        set
        {
            if (value)
            {
                Type = PreferenceType.Preferred;
            }
        }
    }

    /// <summary>Rank only means anything for a wish or a requirement, never for a refusal.</summary>
    public bool IsRankRelevant => Type is PreferenceType.Preferred or PreferenceType.Mandatory;

    /// <summary>
    /// Settable, so a radio button drives the value through its own binding.
    /// </summary>
    /// <remarks>
    /// These were get-only, with a command beside each radio button doing the actual work.
    /// <c>IsChecked</c> is two way by default, so every click tried to write back to a
    /// property that had no setter, and the state only changed because the command happened
    /// to fire as well. The control and the value were kept in step by luck.
    /// <para>
    /// Only a true assignment does anything. A radio group unsets the previous member by
    /// writing false to it, and taking that literally would clear the value that the
    /// newly selected member has just set, in an order nobody controls.
    /// </para>
    /// </remarks>
    public bool IsNoPreference
    {
        get => Type is null;
        set
        {
            if (value)
            {
                Type = null;
            }
        }
    }

    partial void OnTypeChanged(PreferenceType? value)
    {
        OnPropertyChanged(nameof(IsMandatory));
        OnPropertyChanged(nameof(IsBlocked));
        OnPropertyChanged(nameof(IsPreferred));
        OnPropertyChanged(nameof(IsRankRelevant));
        OnPropertyChanged(nameof(IsNoPreference));

        // Being blocked from a line and being allowed to run it are contradictory, and the
        // validator would report it. Better to make it unrepresentable here.
        if (value == PreferenceType.Blocked)
        {
            CanLead = false;
            CanAssist = false;
        }
    }
}
