using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Linewise.Application.Persistence;
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

    [ObservableProperty]
    private RosterGridViewModel? _grid;

    [ObservableProperty]
    private string _status = "Starting.";

    [ObservableProperty]
    private bool _isLoading;

    public MainWindowViewModel(IRosterRepository rosters, IConfigurationRepository configuration)
    {
        _rosters = rosters;
        _configuration = configuration;
    }

    /// <summary>For the Avalonia designer, which cannot resolve from the container.</summary>
    public MainWindowViewModel()
    {
    }

    public string Title => "Linewise";

    /// <summary>
    /// Carries the commit it was built from. On a wall sheet a stale roster is spotted by
    /// its printed timestamp; this is the equivalent, and it is the first thing worth
    /// asking for when somebody reports a fault.
    /// </summary>
    public string Version =>
        typeof(MainWindowViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown version";

    public bool HasRoster => Grid is { IsEmpty: false };

    /// <summary>
    /// Loads the week containing <paramref name="anyDateInWeek"/>. Nothing is generated
    /// here: this reads what is stored, and an empty database is a normal first run rather
    /// than a fault.
    /// </summary>
    public async Task LoadAsync(DateOnly anyDateInWeek, CancellationToken cancellationToken = default)
    {
        if (_rosters is null || _configuration is null)
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
                Grid = null;
                Status = $"No roster stored for the week beginning {weekStart:d MMMM yyyy}.";
                Log.Information("No roster found for {WeekStart}.", weekStart);
                return;
            }

            var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);

            Grid = new RosterGridViewModel(stored.Roster, configuration.Lines, configuration.Employees);
            Status = $"Version {stored.Version.VersionNumber}, {stored.Version.Status}.";

            // Counts, never names. The redaction policy covers structured logging of an
            // employee; this is the other half of the same habit.
            Log.Information(
                "Loaded roster for {WeekStart}: {Lines} lines, {Days} days, {Assignments} assignments.",
                weekStart,
                Grid.Rows.Count,
                Grid.DayCount,
                stored.Roster.AllAssignments.Count());
        }
        catch (Exception exception)
        {
            // A failure to read is shown as a sentence, not a dialog full of stack trace.
            Log.Error(exception, "Could not load the roster.");
            Status = "Could not load the roster. See the log for details.";
            Grid = null;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasRoster));
        }
    }

    /// <summary>
    /// The engine works Monday to Sunday, so the grid does too. Uses the ISO ordering
    /// rather than the current culture's first day: the roster's week is a property of the
    /// factory, not of the machine's regional settings.
    /// </summary>
    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
