using Linewise.Desktop.Resources;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// What is set up, what is imported, and what state this week is in.
/// </summary>
/// <remarks>
/// The window used to open onto either a roster grid or a getting-started panel, which made
/// a configured factory with no roster for the current week look exactly like a fresh
/// install. The lines were saved; the screen simply never said so.
/// <para>
/// This answers the three questions somebody actually opens the application with: is my
/// setup still here, has this week's sheet been imported, and is there a roster yet.
/// </para>
/// </remarks>
public sealed class DashboardViewModel
{
    public DashboardViewModel(
        DateOnly weekStart,
        IReadOnlyList<ProductionLine> lines,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Availability> availability,
        RosterSummaryViewModel? roster)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(employees);
        ArgumentNullException.ThrowIfNull(availability);

        WeekStart = weekStart;
        LineCount = lines.Count;
        PeopleCount = employees.Count(employee => employee.IsActive);

        TemporaryCount = employees.Count(employee => employee.IsActive && employee.IsTemporary);

        // Somebody marked as in on at least one day of this week. Availability rows exist
        // for people who are off as well, so counting rows would report a sheet as imported
        // when every cell in it said nobody is coming.
        AvailableThisWeek = availability
            .Where(entry => entry.Status is AvailabilityStatus.Working or AvailabilityStatus.Overtime)
            .Select(entry => entry.EmployeeId)
            .Distinct()
            .Count();

        DaysCovered = availability.Select(entry => entry.Date).Distinct().Count();

        Roster = roster;
    }

    public DateOnly WeekStart { get; }

    public int LineCount { get; }

    public int PeopleCount { get; }

    public int TemporaryCount { get; }

    public int AvailableThisWeek { get; }

    public int DaysCovered { get; }

    public RosterSummaryViewModel? Roster { get; }

    /// <summary>
    /// The errors, already grouped by kind and day, worst first.
    /// </summary>
    /// <remarks>
    /// This screen was a readout: five true statements about how many of everything there
    /// is. None of them told the manager what to do next, which is the only question worth
    /// opening an overview to answer.
    /// </remarks>
    public IReadOnlyList<WarningGroupViewModel> Problems { get; init; } = [];

    /// <summary>The few worth putting on the front page. The rest are one click away.</summary>
    public IReadOnlyList<WarningGroupViewModel> TopProblems => [.. Problems.Take(4)];

    public bool HasProblems => Problems.Count > 0;

    public bool HasMoreProblems => Problems.Count > TopProblems.Count;

    public string MoreProblemsLabel => Strings.DashboardMoreProblems(Problems.Count - TopProblems.Count);

    /// <summary>
    /// Nothing wrong, and a roster to say so about. Worth stating plainly: a week with no
    /// errors is the outcome, and a screen that only ever speaks up about problems leaves
    /// somebody wondering whether it looked.
    /// </summary>
    public bool IsAllWell => HasRoster && !HasProblems;

    /// <summary>
    /// Nothing has been set up at all. The only state that deserves to be walked through,
    /// and the reason the guidance stops appearing once a factory exists.
    /// </summary>
    public bool IsFirstRun => LineCount == 0 && PeopleCount == 0;

    public bool HasLines => LineCount > 0;

    public bool HasPeople => PeopleCount > 0;

    public bool HasAvailability => AvailableThisWeek > 0;

    public bool HasRoster => Roster is not null;

    /// <summary>
    /// The one thing worth doing next. A dashboard that lists everything equally is a
    /// dashboard somebody has to read; this says what is missing.
    /// </summary>
    public string NextStep => (HasLines, HasPeople, HasAvailability, HasRoster) switch
    {
        (false, _, _, _) => Strings.NextStepLines,
        (_, false, _, _) => Strings.NextStepPeople,
        (_, _, false, _) => Strings.NextStepImport,
        (_, _, _, false) => Strings.NextStepGenerate,
        _ => Strings.NextStepReview,
    };

    public string WeekLabel => Strings.WeekBeginning(WeekStart);

    public string LinesLabel => Strings.DashboardLines(LineCount);

    public string PeopleLabel => Strings.DashboardPeople(PeopleCount, TemporaryCount);

    public string AvailabilityLabel => HasAvailability
        ? Strings.DashboardAvailability(AvailableThisWeek, DaysCovered)
        : Strings.DashboardNoAvailability;

    public string RosterLabel => Roster is null
        ? Strings.DashboardNoRoster
        : Strings.DashboardRoster(Roster.Placed, Roster.Errors);
}
