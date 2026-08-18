using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Persistence;
using Linewise.Application.Printing;
using Linewise.Application.Rostering;
using Linewise.Desktop.Resources;
using Linewise.Desktop.Services;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The shell. Loads the most recent roster for a week and hands it to the grid, or explains
/// why there is nothing to draw.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IRosterEditor
{
    /// <summary>
    /// Where a unit of work comes from.
    /// </summary>
    /// <remarks>
    /// This window lives for the whole run of the application, and the repositories it used
    /// to hold are scoped to a database context. Holding them made that context live just as
    /// long: one change tracker, accumulating every entity the application ever wrote, for
    /// hours. That is a captive dependency, and it is what turned "I cannot save rules
    /// sometimes" from an unlikely race into a certainty.
    /// <para>
    /// Each operation now opens its own scope, does its work and disposes it. That is also
    /// the honest shape: a load is one unit of work and should read one consistent snapshot,
    /// rather than whatever a months-old tracker happens to be holding.
    /// </para>
    /// </remarks>
    private readonly IServiceScopeFactory? _scopes;

    private readonly IDialogService? _dialogs;
    private readonly IThemeService? _theme;

    private DateOnly _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Today));

    // Kept so the grid can be rebuilt for a different date without going back to the
    // database. Switching between today and the week is a change of view, not of data.
    private RosterWeek? _loadedRoster;
    private RosterConfiguration? _loadedConfiguration;

    // Who is actually in. Kept beside the roster because the grid is rebuilt from both, and
    // rereading availability to switch between today and the week would make a view toggle
    // look like a reload.
    private Attendance _attendance = Attendance.Everybody;

    // Which lines are shut on which days. Kept beside the roster for the same reason
    // attendance is: switching between today and the week must not look like a reload.
    private IReadOnlyList<LineDemand> _loadedDemands = [];

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

    /// <summary>
    /// How the printed sheet is laid out. Held here because the settings panel is part of
    /// this window, and stored in the database so it travels with a backup rather than
    /// living in a configuration file beside the executable.
    /// </summary>
    [ObservableProperty]
    private PrintSettingsViewModel? _printSettings;

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
        IServiceScopeFactory scopes,
        IDialogService dialogs,
        IThemeService theme)
    {
        _scopes = scopes;
        _dialogs = dialogs;
        _theme = theme;
    }

    /// <summary>
    /// Runs one piece of work against its own scope, and disposes it afterwards.
    /// </summary>
    /// <remarks>
    /// The whole operation shares one scope rather than one per service, because a load that
    /// reads the roster, the configuration and the availability is one question and should
    /// get one answer.
    /// </remarks>
    private async Task<TResult> InScopeAsync<TResult>(Func<IServiceProvider, Task<TResult>> work)
    {
        await using var scope = _scopes!.CreateAsyncScope();

        return await work(scope.ServiceProvider).ConfigureAwait(true);
    }

    private async Task InScopeAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = _scopes!.CreateAsyncScope();

        await work(scope.ServiceProvider).ConfigureAwait(true);
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

    /// <summary>
    /// What the panel draws: errors under one heading, notices under another. Two questions
    /// rather than one list — what stops a line running, and what is merely worth knowing.
    /// </summary>
    public ObservableCollection<WarningSectionViewModel> WarningSections { get; } = [];

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

    /// <summary>
    /// Which palette is on, as a switch rather than a button that flips something.
    /// </summary>
    /// <remarks>
    /// A settings panel says what the state is; a button in a list only says what pressing
    /// it would do. The theme service holds the truth either way.
    /// </remarks>
    public bool IsDarkTheme
    {
        get => _theme?.IsDark ?? false;
        set
        {
            if (_theme is not null && value != _theme.IsDark)
            {
                _theme.Toggle();
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void ShowDashboard() => IsShowingRoster = false;

    /// <summary>
    /// The week before, the week after, and back to this one.
    /// </summary>
    /// <remarks>
    /// The week on screen was fixed to whichever one contained today, and nothing could
    /// change it. A rostering tool that can only show the current week cannot be used for
    /// the thing rostering is for: a manager builds Monday's roster on the Thursday before
    /// it.
    /// </remarks>
    [RelayCommand]
    private Task PreviousWeekAsync(CancellationToken cancellationToken) =>
        LoadAsync(_weekStart.AddDays(-7), cancellationToken);

    [RelayCommand]
    private Task NextWeekAsync(CancellationToken cancellationToken) =>
        LoadAsync(_weekStart.AddDays(7), cancellationToken);

    [RelayCommand]
    private Task ThisWeekAsync(CancellationToken cancellationToken) =>
        LoadAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);

    /// <summary>
    /// Whether the week on screen is the one today falls in. Drives a way back, and the
    /// label that says where you are.
    /// </summary>
    public bool IsThisWeek => _weekStart == MondayOf(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>The week on screen, named. Always says which week, never just "this week".</summary>
    public string WeekLabel => Strings.WeekBeginning(_weekStart);

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
            this,
            _loadedDemands);

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
        await FillGapsAsync(cancellationToken).ConfigureAwait(true);
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
        await FillGapsAsync(cancellationToken).ConfigureAwait(true);
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

        if (_scopes is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await InScopeAsync(services =>
                services.GetRequiredService<IAbsenceService>().MarkAbsentAsync(
                    new MarkAbsentRequest
                    {
                        EmployeeId = chip.EmployeeId,
                        Date = chip.Date,
                        Reason = reason,
                    })).ConfigureAwait(true);

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

        if (_scopes is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await InScopeAsync(services =>
                services.GetRequiredService<IAbsenceService>()
                    .ClearAbsenceAsync(chip.EmployeeId, chip.Date)).ConfigureAwait(true);

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

    public Task<IReadOnlyList<ReplacementCandidate>> FindReplacementsAsync(Guid lineId, DateOnly date) =>
        _scopes is null
            ? Task.FromResult<IReadOnlyList<ReplacementCandidate>>([])
            : InScopeAsync(services =>
                services.GetRequiredService<IReplacementFinder>().FindAsync(lineId, date));

    /// <summary>
    /// Puts somebody on a line for a day, locked, and redraws.
    /// </summary>
    /// <remarks>
    /// Locked and marked manual, so the generate somebody presses afterwards leaves them
    /// where they were put. That is the rule the whole of phase 6 rests on.
    /// </remarks>
    public async Task PlaceAsync(Guid employeeId, Guid lineId, DateOnly date)
    {
        if (_scopes is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await InScopeAsync(services =>
                services.GetRequiredService<IAbsenceService>()
                    .PlaceAsync(employeeId, lineId, date)).ConfigureAwait(true);

            Log.Information("Placed somebody on {LineId} for {Date}, locked.", lineId, date);

            await LoadAsync(_weekStart).ConfigureAwait(true);
            Status = Strings.PlacedCover;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not place somebody on a line.");
            Status = Strings.CouldNotPlace;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Shuts a line for a day, or puts it back into service.
    /// </summary>
    /// <remarks>
    /// Written and redrawn immediately, but nobody is moved. The people who were on the line
    /// stay where the last generate put them until somebody presses generate again, which is
    /// the same rule every other rule change follows: editing a rule does not rewrite a week
    /// that has already been published.
    /// </remarks>
    public async Task SetLineClosedAsync(Guid lineId, DateOnly date, bool closed)
    {
        if (_scopes is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await InScopeAsync(services =>
            {
                var lineDays = services.GetRequiredService<ILineDayService>();

                return closed
                    ? lineDays.CloseAsync(lineId, date)
                    : lineDays.ReopenAsync(lineId, date);
            }).ConfigureAwait(true);

            Log.Information("Line {LineId} {State} for {Date}.", lineId, closed ? "closed" : "reopened", date);

            await LoadAsync(_weekStart).ConfigureAwait(true);
            Status = closed ? Strings.LineClosedFor(date) : Strings.LineReopenedFor(date);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not change whether a line is running.");
            Status = Strings.CouldNotCloseLine;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Stores how the printed sheet should look. Written as it is changed rather than
    /// behind a save button: there is nothing to review, and a settings panel that needs
    /// saving is one somebody closes without saving.
    /// </summary>
    private async Task SavePrintSettingsAsync(Domain.Entities.PrintSettings settings)
    {
        if (_scopes is null)
        {
            return;
        }

        try
        {
            await InScopeAsync(services =>
                services.GetRequiredService<IConfigurationRepository>()
                    .SavePrintSettingsAsync(settings)).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not save the print settings.");
            Status = Strings.CouldNotSaveSettings;
        }
    }

    /// <summary>
    /// Whether this week already has a roster, so generating again would rebuild one that
    /// has been looked at. Drives the warning on the button rather than blocking it.
    /// </summary>
    public bool WouldRegenerate => HasRoster;

    /// <summary>
    /// Fills whatever the rules can now fill, then redraws.
    /// </summary>
    /// <remarks>
    /// Run after the rules are edited. Making somebody eligible to lead a line that has no
    /// leader should put them on it, and it should not take pressing generate — which would
    /// also fix it, and would reshuffle a week that has already been reviewed to do so.
    /// <para>
    /// Only ever adds. Nobody already placed moves, and nobody is taken off another line.
    /// </para>
    /// </remarks>
    private async Task FillGapsAsync(CancellationToken cancellationToken)
    {
        if (_scopes is not null)
        {
            try
            {
                var filled = await InScopeAsync(services =>
                    services.GetRequiredService<IGapFiller>()
                        .FillAsync(_weekStart, cancellationToken)).ConfigureAwait(true);

                await LoadAsync(_weekStart, cancellationToken).ConfigureAwait(true);

                if (filled > 0)
                {
                    Log.Information("Filled {Filled} places after a rule change.", filled);
                    Status = Strings.GapsFilled(filled);
                }

                return;
            }
            catch (Exception exception)
            {
                // A week that could not be topped up is still a week worth drawing.
                Log.Error(exception, "Could not fill the gaps after a rule change.");
            }
        }

        await LoadAsync(_weekStart, cancellationToken).ConfigureAwait(true);
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
        if (_scopes is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var stored = await InScopeAsync(services =>
                services.GetRequiredService<IRosterGenerationService>()
                    .GenerateAsync(_weekStart, cancellationToken)).ConfigureAwait(true);

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
    private Task PrintAsync(CancellationToken cancellationToken) =>
        PrintDocumentAsync(
            (printer, request) => printer.PrintFullSheetAsync(request, cancellationToken),
            $"linewise-{_weekStart:yyyy-MM-dd}.pdf",
            cancellationToken);

    /// <summary>
    /// Builds what every layout needs, renders one, and hands it to whatever prints a PDF
    /// on this machine.
    /// </summary>
    /// <remarks>
    /// Three layouts, one path. The full sheet, the per-line sheets and the amendment slip
    /// all want the same week assembled the same way, and three copies of that assembly is
    /// three chances for one of them to forget who is absent.
    /// </remarks>
    private async Task PrintDocumentAsync(
        Func<IRosterPrinter, PrintRequest, Task<byte[]>> render,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (_scopes is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            // One scope for the whole print. What goes on this sheet is one question, and
            // reading it through several contexts would let the answer change halfway.
            var document = await InScopeAsync(async services =>
            {
                var stored = await services.GetRequiredService<IRosterRepository>()
                    .GetLatestAsync(_weekStart, cancellationToken)
                    .ConfigureAwait(true);

                if (stored is null)
                {
                    return null;
                }

                var configuration = services.GetRequiredService<IConfigurationRepository>();
                var rules = await configuration.GetAsync(cancellationToken).ConfigureAwait(true);

                var request = new PrintRequest
                {
                    Roster = stored.Roster,
                    Version = stored.Version,
                    Lines = rules.Lines,
                    Employees = rules.Employees,
                    Shifts = await services.GetRequiredService<IShiftRepository>()
                        .GetAsync(_weekStart, _weekStart.AddDays(7), cancellationToken)
                        .ConfigureAwait(true),

                    // So the sheet on the wall does not name somebody who rang in at seven.
                    Availabilities = await services.GetRequiredService<IAvailabilityRepository>()
                        .GetAsync(_weekStart, _weekStart.AddDays(7), cancellationToken)
                        .ConfigureAwait(true),
                    Settings = await configuration
                        .GetPrintSettingsAsync(cancellationToken)
                        .ConfigureAwait(true),
                };

                return await render(services.GetRequiredService<IRosterPrinter>(), request)
                    .ConfigureAwait(true);
            }).ConfigureAwait(true);

            if (document is null)
            {
                Status = Strings.NothingToPrint;
                return;
            }

            await InScopeAsync(services => services.GetRequiredService<IDocumentLauncher>()
                .PrintAsync(document, fileName, cancellationToken)).ConfigureAwait(true);

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
    /// Publishes the week, then offers the sheet or the slip.
    /// </summary>
    /// <remarks>
    /// Publishing increments the version and stamps who did it and when, so a sheet on a
    /// wall can be told apart from the one somebody printed on Tuesday. It is deliberately
    /// separate from printing: a week can be published and printed twice, or published and
    /// not printed at all, and neither is a mistake.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasRoster))]
    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        if (_scopes is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await InScopeAsync(services =>
                services.GetRequiredService<IPublishingService>()
                    .PublishAsync(_weekStart, cancellationToken)).ConfigureAwait(true);

            _previouslyPublished = result.PreviouslyPublished;

            Log.Information(
                "Published {WeekStart} as version {Version}.",
                _weekStart,
                result.Version.VersionNumber);

            await LoadAsync(_weekStart, cancellationToken).ConfigureAwait(true);

            Status = result.CanAmend
                ? Strings.PublishedWithSlip(result.Version.VersionNumber)
                : Strings.Published(result.Version.VersionNumber);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not publish the roster.");
            Status = Strings.CouldNotPublish;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// What was on the wall before the last publish, kept so a slip can be printed after it.
    /// </summary>
    private RosterWeek? _previouslyPublished;

    /// <summary>
    /// There is an earlier published sheet for a slip to describe the changes from. A week
    /// published for the first time has nothing to amend, and the full sheet is the only
    /// honest answer.
    /// </summary>
    public bool CanPrintAmendment => _previouslyPublished is not null;

    /// <summary>
    /// One page saying only what changed, to pin beside a roster already on the wall.
    /// </summary>
    /// <remarks>
    /// RosterDiff has computed these changes since phase 4 and nothing has ever called it.
    /// Reprinting the whole sheet every time somebody rings in sick is how a wall ends up
    /// with four versions of Tuesday on it.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanPrintAmendment))]
    private Task PrintAmendmentAsync(CancellationToken cancellationToken) =>
        PrintDocumentAsync(
            (printer, request) => printer.PrintAmendmentSlipAsync(
                new AmendmentRequest(request, _previouslyPublished!),
                cancellationToken),
            $"linewise-amendment-{_weekStart:yyyy-MM-dd}.pdf",
            cancellationToken);

    /// <summary>
    /// One page per line, to pin at that line. Carries the line's layout notes, which is
    /// where they were always meant to be read.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasRoster))]
    private Task PrintPerLineAsync(CancellationToken cancellationToken) =>
        PrintDocumentAsync(
            (printer, request) => printer.PrintPerLineSheetsAsync(request, cancellationToken),
            $"linewise-lines-{_weekStart:yyyy-MM-dd}.pdf",
            cancellationToken);

    /// <summary>
    /// Loads the week containing <paramref name="anyDateInWeek"/>. Nothing is generated
    /// here: this reads what is stored, and an empty database is a normal first run rather
    /// than a fault.
    /// </summary>
    public async Task LoadAsync(DateOnly anyDateInWeek, CancellationToken cancellationToken = default)
    {
        if (_scopes is null)
        {
            return;
        }

        IsLoading = true;

        try
        {
            var weekStart = MondayOf(anyDateInWeek);
            _weekStart = weekStart;

            // One scope for the whole load. A week's roster, rules, availability and demands
            // are one question, and reading them through four contexts would let the answer
            // change between the first read and the last.
            var (stored, configuration, printSettings, weekAvailability, demands) =
                await InScopeAsync(async services =>
                {
                    var rosters = services.GetRequiredService<IRosterRepository>();
                    var rules = services.GetRequiredService<IConfigurationRepository>();

                    return (
                        await rosters.GetLatestAsync(weekStart, cancellationToken).ConfigureAwait(true),
                        await rules.GetAsync(cancellationToken).ConfigureAwait(true),
                        await rules.GetPrintSettingsAsync(cancellationToken).ConfigureAwait(true),
                        await services.GetRequiredService<IAvailabilityRepository>()
                            .GetAsync(weekStart, weekStart.AddDays(7), cancellationToken)
                            .ConfigureAwait(true),
                        await services.GetRequiredService<ILineDemandRepository>()
                            .GetAsync(weekStart, weekStart.AddDays(7), cancellationToken)
                            .ConfigureAwait(true));
                }).ConfigureAwait(true);

            PrintSettings ??= new PrintSettingsViewModel(printSettings, SavePrintSettingsAsync);

            _loadedDemands = demands;

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
                this,
                _loadedDemands);
            Summary = new RosterSummaryViewModel(stored.Roster, weekAvailability, stored.Version);

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

            // Built after the warnings, not before. The overview leads with what needs
            // deciding, and it cannot do that until there is something to lead with.
            Dashboard = new DashboardViewModel(
                weekStart,
                configuration.Lines,
                configuration.Employees,
                weekAvailability,
                Summary)
            {
                Problems = [.. WarningGroups.Where(group => group.IsError)],
            };

            WarningSections.Clear();

            foreach (var severity in (WarningSeverity[])[WarningSeverity.Error, WarningSeverity.Notice])
            {
                var groups = WarningGroups.Where(group => group.Severity == severity).ToList();

                if (groups.Count > 0)
                {
                    WarningSections.Add(new WarningSectionViewModel(severity, groups));
                }
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
        if (_scopes is null)
        {
            return;
        }

        var configuration = await InScopeAsync(services =>
            services.GetRequiredService<IConfigurationRepository>()
                .GetAsync(cancellationToken)).ConfigureAwait(true);

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
        _loadedDemands = [];
        Warnings.Clear();
        WarningGroups.Clear();
        WarningSections.Clear();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasRoster));
        OnPropertyChanged(nameof(WouldRegenerate));
        OnPropertyChanged(nameof(IsThisWeek));
        OnPropertyChanged(nameof(WeekLabel));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(Title));
        PrintCommand.NotifyCanExecuteChanged();
        PrintPerLineCommand.NotifyCanExecuteChanged();
        PublishCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanPrintAmendment));
        PrintAmendmentCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// The engine works Monday to Sunday, so the grid does too. Uses the ISO ordering
    /// rather than the current culture's first day: the roster's week is a property of the
    /// factory, not of the machine's regional settings.
    /// </summary>
    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
