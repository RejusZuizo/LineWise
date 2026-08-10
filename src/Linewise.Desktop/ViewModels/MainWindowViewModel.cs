using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Linewise.Application.Persistence;
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

    [ObservableProperty]
    private RosterGridViewModel? _grid;

    [ObservableProperty]
    private RosterSummaryViewModel? _summary;

    [ObservableProperty]
    private string _status = "Starting.";

    [ObservableProperty]
    private bool _isLoading;

    public MainWindowViewModel(
        IRosterRepository rosters,
        IConfigurationRepository configuration,
        IAvailabilityRepository availability)
    {
        _rosters = rosters;
        _configuration = configuration;
        _availability = availability;
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
    /// Carries the state as well as the name, because a published roster is on a wall
    /// somewhere and confusing it with a draft is how two versions of Tuesday end up
    /// posted.
    /// </summary>
    public string Title => Summary is null
        ? "Linewise"
        : $"Linewise — {Grid?.WeekLabel ?? string.Empty} — {Summary.StatusLabel}";

    public string Version =>
        typeof(MainWindowViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown version";

    public bool HasRoster => Grid is { IsEmpty: false };

    public bool HasWarnings => Warnings.Count > 0;

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
            var stored = await _rosters.GetLatestAsync(weekStart, cancellationToken).ConfigureAwait(true);

            if (stored is null)
            {
                Clear();
                Status = $"No roster stored for the week beginning {weekStart:d MMMM yyyy}.";
                Log.Information("No roster found for {WeekStart}.", weekStart);
                return;
            }

            var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);

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
            Status = "Could not load the roster. See the log for details.";
        }
        finally
        {
            IsLoading = false;
            Notify();
        }
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
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>
    /// The engine works Monday to Sunday, so the grid does too. Uses the ISO ordering
    /// rather than the current culture's first day: the roster's week is a property of the
    /// factory, not of the machine's regional settings.
    /// </summary>
    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
