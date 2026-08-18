using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Persistence;
using Linewise.Application.Printing;
using Linewise.Application.Rostering;
using Linewise.Desktop.Resources;
using Linewise.Desktop.Services;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Serilog;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The shell. Loads the most recent roster for a week and hands it to the grid, or explains
/// why there is nothing to draw.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IRosterEditor
{
    private readonly IRosterRepository? _rosters;
    private readonly IConfigurationRepository? _configuration;
    private readonly IAvailabilityRepository? _availability;
    private readonly IRosterGenerationService? _generation;
    private readonly IRosterPrinter? _printer;
    private readonly IDocumentLauncher? _launcher;
    private readonly IShiftRepository? _shifts;
    private readonly IDialogService? _dialogs;
    private readonly IThemeService? _theme;
    private readonly IAbsenceService? _absences;

    private DateOnly _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Today));

    // Kept so the grid can be rebuilt for a different date without going back to the
    // database. Switching between today and the week is a change of view, not of data.
    private RosterWeek? _loadedRoster;
    private RosterConfiguration? _loadedConfiguration;

    // Who is actually in. Kept beside the roster because the grid is rebuilt from both, and
    // rereading availability to switch between today and the week would make a view toggle
    // look like a reload.
    private Attendance _attendance = Attendance.Everybody;

    [ObservableProperty]
    private RosterGridViewModel? _grid;

    /// <summary>
    /// What is set up and what state the week is in. The window opens on this rather than
    /// on the grid, so a configured factory with no roster yet stops looking like a fresh
    /// install.
    /// </summary>
    [ObservableProperty]
    private DashboardViewModel? _dashboard;

    /// <summary>
    /// The grid is somewhere you go, not what you are dropped into. Somebody opening the
    /// application on a Monday wants to know whether the sheet is in before they want to
    /// read a hundred and forty names.
    /// </summary>
    [ObservableProperty]
    private bool _isShowingRoster;

    /// <summary>
    /// Today, or the whole week. Defaults to today: the grid is opened far more often to
    /// answer "who is on Ovens this morning" than to plan seven days at once.
    /// </summary>
    [ObservableProperty]
    private bool _isSingleDay = true;

    [ObservableProperty]
    private RosterSummaryViewModel? _summary;

    [ObservableProperty]
    private string _status = Strings.Starting;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// A long running action is in progress. Disables the toolbar rather than showing a
    /// spinner over the grid: the roster on screen is still the truth while a new one is
    /// being built, and hiding it would be a step backwards.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    public MainWindowViewModel(
        IRosterRepository rosters,
        IConfigurationRepository configuration,
        IAvailabilityRepository availability,
        IRosterGenerationService generation,
        IRosterPrinter printer,
        IDocumentLauncher launcher,
        IShiftRepository shifts,
        IDialogService dialogs,
        IThemeService theme,
        IAbsenceService absences)
    {
        _rosters = rosters;
        _configuration = configuration;
        _availability = availability;
        _generation = generation;
        _printer = printer;
        _launcher = launcher;
        _shifts = shifts;
        _dialogs = dialogs;
        _theme = theme;
        _absences = absences;
    }

    /// <summary>For the Avalonia designer, which cannot resolve from the container.</summary>
    public MainWindowViewModel()
    {
    }

    /// <summary>
    /// Errors first, then by date. A warnings strip sorted by when it happened buries the
    /// line that cannot run under a week of notices about people who were not needed.
    /// </summary>
    public ObservableCollection<WarningViewModel> Warnings { get; } = [];

    /// <summary>
    /// The configured lines, listed in the sidebar. Shown whether or not a roster exists,
    /// because on a first run the lines are the thing somebody has to create before
    /// anything else works, and a list of nothing is a clearer prompt than a hidden panel.
    /// </summary>
    /// <summary>
    /// What the panel actually shows: one row per kind of warning per day, with a count.
    /// The flat list is still built, because it is what the editing screens will need.
    /// </summary>
    public ObservableCollection<WarningGroupViewModel> WarningGroups { get; } = [];

    public ObservableCollection<SidebarLineViewModel> ConfiguredLines { get; } = [];

    /// <summary>
    /// Both panels collapse. On a 1360 wide window the sidebar and the warnings take 588
    /// pixels between them, which is most of a day column, and somebody reading names does
    /// not need either of them on screen.
    /// </summary>
    [ObservableProperty]
    private bool _isNavigationVisible = true;

    [ObservableProperty]
    private bool _areWarningsVisible = true;

    public bool HasLines => ConfiguredLines.Count > 0;

    /// <summary>
    /// Carries the state as well as the name, because a published roster is on a wall
    /// somewhere and confusing it with a draft is how two versions of Tuesday end up
    /// posted.
    /// </summary>
    public string Title => Summary is null
        ? Strings.ProductName
        : Strings.WindowTitle(Grid?.WeekLabel ?? string.Empty, Summary.StatusLabel);

    public string Version =>
        typeof(MainWindowViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown version";

    public bool HasRoster => Grid is { IsEmpty: false };

    public bool HasWarnings => WarningGroups.Count > 0;

    /// <summary>
    /// Nothing configured at all. The only state that earns a walkthrough — once a factory
    /// exists the guidance goes away and does not come back.
    /// </summary>
    public bool IsFirstRun => Dashboard is null or { IsFirstRun: true };

    /// <summary>
    /// Switches between the light and dark palettes.
    /// </summary>
    /// <remarks>
    /// Light remains the default, because a roster on screen is compared against a sheet of
    /// white paper on a wall. This exists for the office rather than the wall, and it
    /// deliberately does not follow the system: the comparison is a property of the job,
    /// not of the time of day.
    /// <para>
    /// Not yet persisted. The choice resets on restart until the settings screen in phase 7
    /// gives it somewhere to live in the database, where it travels with a backup.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private void ToggleTheme() => _theme?.Toggle();

    [RelayCommand]
    private void ShowDashboard() => IsShowingRoster = false;

    [RelayCommand]
    private void ShowToday()
    {
        IsSingleDay = true;
        Rebuild();
    }

    [RelayCommand]
    private void ShowWholeWeek()
    {
        IsSingleDay = false;
        Rebuild();
    }

    /// <summary>
    /// Redraws the grid from what is already in hand. No database read: the roster on screen
    /// is the same roster either way, and going back for it would make a view toggle look
    /// like a reload.
    /// </summary>
    private void Rebuild()
    {
        if (_loadedRoster is null || _loadedConfiguration is null)
        {
            return;
        }

        Grid = new RosterGridViewModel(
            _loadedRoster,
            _loadedConfiguration.Lines,
            _loadedConfiguration.Employees,
            IsSingleDay ? DayInView() : null,
            _attendance,
            this);

        OnPropertyChanged(nameof(HasRoster));
    }

    /// <summary>
    /// Today when today is in the week on screen, and the Monday of it otherwise. Opening
    /// last week's roster and being shown an empty Saturday would be technically correct
    /// and useless.
    /// </summary>
    private DateOnly DayInView()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return today >= _weekStart && today < _weekStart.AddDays(7) ? today : _weekStart;
    }

    [RelayCommand]
    private void ShowRoster() => IsShowingRoster = true;

    [RelayCommand]
    private void ToggleNavigation() => IsNavigationVisible = !IsNavigationVisible;

    [RelayCommand]
    private void ToggleWarnings() => AreWarningsVisible = !AreWarningsVisible;

    /// <summary>
    /// Opens people and priorities, then redraws. Changing who prefers what does not move
    /// anybody on its own: the roster on screen was generated under the old rules, and it
    /// stays that way until somebody presses generate. Rewriting a published week because
    /// a preference was edited would be the opposite of keeping the manager in control.
    /// </summary>
    [RelayCommand]
    private async Task ShowPeopleAsync(CancellationToken cancellationToken)
    {
        if (_dialogs is null)
        {
            return;
        }

        await _dialogs.ShowPeopleAsync().ConfigureAwait(true);
        await LoadAsync(_weekStart, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens line setup, then redraws. A line added while this window was open should show
    /// up without anybody having to restart the application.
    /// </summary>
    [RelayCommand]
    private async Task SetUpLinesAsync(CancellationToken cancellationToken)
    {
        if (_dialogs is null)
        {
            return;
        }

        await _dialogs.ShowLineEditorAsync().ConfigureAwait(true);
        await LoadAsync(_weekStart, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens the import, then redraws if anything was written. An import that the operator
    /// closed without committing should leave the screen exactly as it was.
    /// </summary>
    [RelayCommand]
    private async Task ImportSheetAsync(CancellationToken cancellationToken)
    {
        if (_dialogs is null)
        {
            return;
        }

        if (await _dialogs.ShowImportAsync().ConfigureAwait(true))
        {
            await LoadAsync(_weekStart, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Takes somebody out of a day and redraws. Their name stays on the line, greyed, so
    /// the manager can still see who should have been there.
    /// </summary>
    /// <remarks>
    /// Writes availability rather than the roster, marked as the manager's own so neither a
    /// regenerate nor a re-import of the sheet puts them back. ADR 0014.
    /// <para>
    /// The reason is picked from a short list of operational words. It says why the roster
    /// changed and never why the person is away: a reason for an absence would put health
    /// data into an audit chain that has no delete path.
    /// </para>
    /// </remarks>
    public async Task MarkAbsentAsync(PersonChipViewModel chip, AbsenceReason reason)
    {
        ArgumentNullException.ThrowIfNull(chip);

        if (_absences is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _absences.MarkAbsentAsync(
                new MarkAbsentRequest
                {
                    EmployeeId = chip.EmployeeId,
                    Date = chip.Date,
                    Reason = reason,
                }).ConfigureAwait(true);

            // A count and a date. Never the name, and never the reason, which is the half
            // of this that a log file has no business holding.
            Log.Information(
                "Marked somebody absent on {Date}, vacating {Vacated} places.",
                chip.Date,
                result.Vacated.Count);

            await LoadAsync(_weekStart).ConfigureAwait(true);
            Status = Strings.MarkedAbsent(result.Vacated.Count);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not mark somebody absent.");
            Status = Strings.CouldNotMarkAbsent;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Puts somebody back. For the person who rang in and then turned up anyway, which
    /// happens often enough that having to undo it through the import would be absurd.
    /// </summary>
    public async Task ClearAbsenceAsync(PersonChipViewModel chip)
    {
        ArgumentNullException.ThrowIfNull(chip);

        if (_absences is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await _absences.ClearAbsenceAsync(chip.EmployeeId, chip.Date).ConfigureAwait(true);

            await LoadAsync(_weekStart).ConfigureAwait(true);
            Status = Strings.MarkedBackIn;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not mark somebody back in.");
            Status = Strings.CouldNotMarkAbsent;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Builds a roster for the week on screen and stores it as the draft, then redraws.
    /// </summary>
    /// <remarks>
    /// Anything placed by hand survives, because the generation service reads the locked
    /// assignments back out of the existing draft and hands them to the engine. Pressing
    /// this twice is safe, which is the property that makes it usable at all.
    /// </remarks>
    [RelayCommand]
    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        if (_generation is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var stored = await _generation.GenerateAsync(_weekStart, cancellationToken).ConfigureAwait(true);

            Log.Information(
                "Generated {WeekStart}: {Assignments} assignments, {Warnings} warnings.",
                _weekStart,
                stored.Roster.AllAssignments.Count(),
                stored.Roster.AllWarnings.Count());

            await LoadAsync(_weekStart, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not generate the roster.");
            Status = Strings.CouldNotGenerate;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Produces the wall sheet and hands it to whatever prints a PDF on this machine.
    /// </summary>
    /// <remarks>
    /// Not Avalonia's printing. A PDF handed to the platform prints identically from every
    /// application on that machine, which is what the operator already knows how to drive.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasRoster))]
    private async Task PrintAsync(CancellationToken cancellationToken)
    {
        if (_printer is null || _launcher is null || _rosters is null
            || _configuration is null || _shifts is null || _availability is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var stored = await _rosters.GetLatestAsync(_weekStart, cancellationToken).ConfigureAwait(true);

            if (stored is null)
            {
                Status = Strings.NothingToPrint;
                return;
            }

            var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);
            var shifts = await _shifts
                .GetAsync(_weekStart, _weekStart.AddDays(7), cancellationToken)
                .ConfigureAwait(true);
            var settings = await _configuration
                .GetPrintSettingsAsync(cancellationToken)
                .ConfigureAwait(true);

            var document = await _printer.PrintFullSheetAsync(
                new PrintRequest
                {
                    Roster = stored.Roster,
                    Version = stored.Version,
                    Lines = configuration.Lines,
                    Employees = configuration.Employees,
                    Shifts = shifts,

                    // So the sheet on the wall does not name somebody who rang in at seven.
                    Availabilities = await _availability
                        .GetAsync(_weekStart, _weekStart.AddDays(7), cancellationToken)
                        .ConfigureAwait(true),
                    Settings = settings,
                },
                cancellationToken).ConfigureAwait(true);

            await _launcher.PrintAsync(
                document,
                $"linewise-{_weekStart:yyyy-MM-dd}.pdf",
                cancellationToken).ConfigureAwait(true);

            Status = Strings.SentToPrinter;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not print the roster.");
            Status = Strings.CouldNotPrint;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Loads the week containing <paramref name="anyDateInWeek"/>. Nothing is generated
    /// here: this reads what is stored, and an empty database is a normal first run rather
    /// than a fault.
    /// </summary>
    public async Task LoadAsync(DateOnly anyDateInWeek, CancellationToken cancellationToken = default)
    {
        if (_rosters is null || _configuration is null || _availability is null)
        {
            return;
        }

        IsLoading = true;

        try
        {
            var weekStart = MondayOf(anyDateInWeek);
            _weekStart = weekStart;
            var stored = await _rosters.GetLatestAsync(weekStart, cancellationToken).ConfigureAwait(true);

            var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);

            var weekAvailability = await _availability
                .GetAsync(weekStart, weekStart.AddDays(7), cancellationToken)
                .ConfigureAwait(true);

            if (stored is null)
            {
                Clear();

                ConfiguredLines.Clear();

                foreach (var line in configuration.Lines.OrderBy(line => line.DisplayOrder))
                {
                    ConfiguredLines.Add(new SidebarLineViewModel(line));
                }

                // The dashboard is built even with no roster. That is the whole point: the
                // lines and the people are still there, and the screen has to say so instead
                // of offering to walk somebody through a setup they finished weeks ago.
                Dashboard = new DashboardViewModel(
                    weekStart,
                    configuration.Lines,
                    configuration.Employees,
                    weekAvailability,
                    roster: null);

                IsShowingRoster = false;
                Status = Strings.NoRosterStored(weekStart);
                Log.Information("No roster found for {WeekStart}.", weekStart);
                return;
            }

            ConfiguredLines.Clear();

            foreach (var line in configuration.Lines.OrderBy(line => line.DisplayOrder))
            {
                ConfiguredLines.Add(new SidebarLineViewModel(line));
            }

            _loadedRoster = stored.Roster;
            _loadedConfiguration = configuration;
            _attendance = new Attendance(weekAvailability);

            Grid = new RosterGridViewModel(
                stored.Roster,
                configuration.Lines,
                configuration.Employees,
                IsSingleDay ? DayInView() : null,
                _attendance,
                this);
            Summary = new RosterSummaryViewModel(stored.Roster, weekAvailability, stored.Version);

            Dashboard = new DashboardViewModel(
                weekStart,
                configuration.Lines,
                configuration.Employees,
                weekAvailability,
                Summary);

            var lineNames = configuration.Lines.ToDictionary(line => line.Id, line => line.Name);
            var employeeNames = configuration.Employees.ToDictionary(e => e.Id, e => e.FullName);

            Warnings.Clear();

            foreach (var warning in stored.Roster.AllWarnings
                .OrderByDescending(warning => warning.Severity == WarningSeverity.Error)
                .ThenBy(warning => warning.Date ?? DateOnly.MinValue))
            {
                Warnings.Add(new WarningViewModel(
                    warning,
                    warning.LineId is { } lineId && lineNames.TryGetValue(lineId, out var line) ? line : null,
                    warning.EmployeeId is { } id && employeeNames.TryGetValue(id, out var name) ? name : null));
            }

            WarningGroups.Clear();

            // Grouped by kind and day. Errors keep their order at the top, because the
            // grouping must not bury the eight things that matter under the two hundred
            // that are merely true.
            foreach (var group in Warnings
                .GroupBy(warning => (warning.Code, warning.Date))
                .OrderByDescending(group => group.Any(warning => warning.IsError))
                .ThenBy(group => group.Key.Date ?? DateOnly.MinValue)
                .ThenBy(group => group.Key.Code))
            {
                WarningGroups.Add(new WarningGroupViewModel([.. group]));
            }

            Status = Summary.WarningsLabel;

            // Counts, never names. The redaction policy covers structured logging of an
            // employee; this is the other half of the same habit.
            Log.Information(
                "Loaded roster for {WeekStart}: {Lines} lines, {Days} days, {Placed} placed, "
                + "{Errors} errors, {Warnings} warnings shown as {Groups} rows.",
                weekStart,
                Grid.Rows.Count,
                Grid.DayCount,
                Summary.Placed,
                Summary.Errors,
                Warnings.Count,
                WarningGroups.Count);
        }
        catch (Exception exception)
        {
            // A failure to read is shown as a sentence, not a dialog full of stack trace.
            Log.Error(exception, "Could not load the roster.");
            Clear();
            Status = Strings.CouldNotLoad;
        }
        finally
        {
            IsLoading = false;
            Notify();
        }
    }

    private async Task LoadLinesAsync(CancellationToken cancellationToken)
    {
        if (_configuration is null)
        {
            return;
        }

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);

        ConfiguredLines.Clear();

        foreach (var line in configuration.Lines.OrderBy(line => line.DisplayOrder))
        {
            ConfiguredLines.Add(new SidebarLineViewModel(line));
        }

        OnPropertyChanged(nameof(HasLines));
    }

    private void Clear()
    {
        Grid = null;
        Summary = null;
        Dashboard = null;
        _loadedRoster = null;
        _loadedConfiguration = null;
        _attendance = Attendance.Everybody;
        Warnings.Clear();
        WarningGroups.Clear();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasRoster));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(Title));
        PrintCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// The engine works Monday to Sunday, so the grid does too. Uses the ISO ordering
    /// rather than the current culture's first day: the roster's week is a property of the
    /// factory, not of the machine's regional settings.
    /// </summary>
    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
