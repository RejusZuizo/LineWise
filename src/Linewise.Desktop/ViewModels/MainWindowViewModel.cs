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
using Serilog;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The shell. Loads the most recent roster for a week and hands it to the grid, or explains
/// why there is nothing to draw.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
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

    private DateOnly _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Today));

    [ObservableProperty]
    private RosterGridViewModel? _grid;

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
        IThemeService theme)
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
    public ObservableCollection<SidebarLineViewModel> ConfiguredLines { get; } = [];

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

    public bool HasWarnings => Warnings.Count > 0;

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
            || _configuration is null || _shifts is null)
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

            if (stored is null)
            {
                Clear();

                // The sidebar still lists whatever lines exist. A first run with three lines
                // and no roster is a different situation from a first run with nothing, and
                // the window should say which one it is.
                await LoadLinesAsync(cancellationToken).ConfigureAwait(true);

                Status = Strings.NoRosterStored(weekStart);
                Log.Information("No roster found for {WeekStart}.", weekStart);
                return;
            }

            var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);

            ConfiguredLines.Clear();

            foreach (var line in configuration.Lines.OrderBy(line => line.DisplayOrder))
            {
                ConfiguredLines.Add(new SidebarLineViewModel(line));
            }

            var availability = await _availability
                .GetAsync(weekStart, weekStart.AddDays(7), cancellationToken)
                .ConfigureAwait(true);

            Grid = new RosterGridViewModel(stored.Roster, configuration.Lines, configuration.Employees);
            Summary = new RosterSummaryViewModel(stored.Roster, availability, stored.Version);

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

            Status = Summary.WarningsLabel;

            // Counts, never names. The redaction policy covers structured logging of an
            // employee; this is the other half of the same habit.
            Log.Information(
                "Loaded roster for {WeekStart}: {Lines} lines, {Days} days, {Placed} placed, {Errors} errors.",
                weekStart,
                Grid.Rows.Count,
                Grid.DayCount,
                Summary.Placed,
                Summary.Errors);
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
        Warnings.Clear();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasRoster));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasLines));
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
