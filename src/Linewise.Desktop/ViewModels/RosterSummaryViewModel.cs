using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The counts along the bottom of the window: how many are placed, how many are not, and
/// how many are unavailable.
/// </summary>
/// <remarks>
/// Derived rather than stored. Everything here is a function of the roster and the
/// availability it was generated against, so there is no second source of truth to drift.
/// <para>
/// Counted per person per day rather than per assignment, because a person appears once a
/// day and "eleven placed" should mean eleven people, not eleven rows.
/// </para>
/// </remarks>
public sealed class RosterSummaryViewModel
{
    public RosterSummaryViewModel(
        RosterWeek roster,
        IReadOnlyList<Availability> availability,
        RosterVersion? version)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(availability);

        var placed = roster.AllAssignments
            .Select(assignment => (assignment.Date, assignment.EmployeeId))
            .ToHashSet();

        Placed = placed.Count;

        Working = availability.Count(a => a.Status == AvailabilityStatus.Working);
        Overtime = availability.Count(a => a.Status == AvailabilityStatus.Overtime);
        Off = availability.Count(a => a.Status is AvailabilityStatus.Off or AvailabilityStatus.Holiday);

        // Available and standing about. The engine already warns for each of these
        // individually; this is the number, so it is visible without reading the strip.
        Unplaced = availability
            .Count(a => a.Status is AvailabilityStatus.Working or AvailabilityStatus.Overtime
                && !placed.Contains((a.Date, a.EmployeeId)));

        Errors = roster.AllWarnings.Count(warning => warning.Severity == WarningSeverity.Error);
        Notices = roster.AllWarnings.Count(warning => warning.Severity == WarningSeverity.Notice);

        Status = version?.Status ?? RosterStatus.Draft;
        VersionNumber = version?.VersionNumber ?? 0;
    }

    public int Placed { get; }

    public int Unplaced { get; }

    public int Working { get; }

    public int Overtime { get; }

    public int Off { get; }

    public int Errors { get; }

    public int Notices { get; }

    public RosterStatus Status { get; }

    public int VersionNumber { get; }

    public bool IsPublished => Status == RosterStatus.Published;

    /// <summary>
    /// Published rosters are on a wall somewhere. Saying which version is on screen is how
    /// a stale sheet gets noticed, and it is the same number printed in the header.
    /// </summary>
    public string StatusLabel => IsPublished
        ? $"Published, version {VersionNumber}"
        : "Draft, not published";

    public string CountsLabel =>
        $"{Placed} placed · {Unplaced} unplaced · {Overtime} on overtime · {Off} off";

    public bool HasWarnings => Errors + Notices > 0;

    /// <summary>
    /// Errors and notices counted separately and named. "12 warnings" invites ignoring all
    /// twelve; "2 errors" does not.
    /// </summary>
    public string WarningsLabel => (Errors, Notices) switch
    {
        (0, 0) => "No warnings",
        (0, var notices) => $"{notices} notice{Plural(notices)}",
        (var errors, 0) => $"{errors} error{Plural(errors)}",
        var (errors, notices) => $"{errors} error{Plural(errors)}, {notices} notice{Plural(notices)}",
    };

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
