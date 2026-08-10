using System.Collections.ObjectModel;
using Linewise.Desktop.Resources;
using Linewise.Domain.Entities;
using Linewise.Domain.Rostering;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// A week of roster as a grid: production lines down the side, days across the top.
/// </summary>
/// <remarks>
/// Read only. Nothing here edits, generates or saves; those arrive in later changes and
/// this one is the thing they will be judged against.
/// <para>
/// The mapping is a pure function of a <see cref="RosterWeek"/> and the configuration it
/// was generated against, which is what makes it testable without a window.
/// </para>
/// </remarks>
public sealed class RosterGridViewModel
{
    public RosterGridViewModel(
        RosterWeek roster,
        IReadOnlyList<ProductionLine> lines,
        IReadOnlyList<Employee> employees)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(employees);

        WeekStart = roster.WeekStart;

        var names = employees.ToDictionary(employee => employee.Id, employee => employee.FullName);

        // Every date the roster covers, in order. Taken from the roster rather than
        // assuming seven days from the start: a roster generated for a short week should
        // draw a short week rather than five empty columns.
        Dates = new ObservableCollection<DateOnly>(
            roster.Days.Select(day => day.Date).Distinct().Order());

        var assignments = roster.AllAssignments.ToList();

        Rows = new ObservableCollection<LineRowViewModel>(
            lines
                .OrderBy(line => line.DisplayOrder)
                .ThenBy(line => line.Name, StringComparer.CurrentCulture)
                .Select(line => new LineRowViewModel(
                    line,
                    Dates.Select(date => new RosterCellViewModel(
                        date,
                        assignments
                            .Where(a => a.LineId == line.Id && a.Date == date)
                            .Select(a => new PersonChipViewModel(
                                a,
                                names.TryGetValue(a.EmployeeId, out var name) ? name : UnknownEmployee)),
                        line.RequiredHeadcount)))));
    }

    /// <summary>
    /// Shown when an identifier has no employee behind it. Should never appear, and saying
    /// so is better than drawing a blank chip that looks like a rendering fault.
    /// </summary>
    public static string UnknownEmployee => Strings.UnknownEmployee;

    public DateOnly WeekStart { get; }

    public ObservableCollection<DateOnly> Dates { get; }

    public ObservableCollection<LineRowViewModel> Rows { get; }

    public int DayCount => Dates.Count;

    public string WeekLabel => Strings.WeekBeginning(WeekStart);

    /// <summary>Nothing to draw. The window shows guidance instead of an empty grid.</summary>
    public bool IsEmpty => Rows.Count == 0 || Dates.Count == 0;
}
