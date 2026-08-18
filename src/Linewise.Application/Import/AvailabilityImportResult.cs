using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <summary>
/// What a sheet turned out to say, before anything is written anywhere.
/// </summary>
/// <remarks>
/// Step one of the three step pipeline. Parsing produces this and touches no database; the
/// operator reviews it; only then is it committed. An import that half succeeded is worse
/// than one that did not run.
/// </remarks>
public sealed record AvailabilityImportResult
{
    public IReadOnlyList<DateOnly> Dates { get; init; } = [];

    public IReadOnlyList<ImportedRow> Rows { get; init; } = [];

    public IReadOnlyList<RosterWarning> Warnings { get; init; } = [];

    /// <summary>
    /// The layout worked out by reading the sheet, when the template did not fit it. Null
    /// when the template was used, which is the ordinary case once a site has imported once.
    /// Committing stores it, so the next sheet of the same shape needs no detecting.
    /// </summary>
    public DetectedLayout? DetectedLayout { get; init; }

    /// <summary>Rows tied to an employee, whether exactly or after forgiving a typo.</summary>
    public IEnumerable<ImportedRow> Matched =>
        Rows.Where(row => row.Match.Outcome is NameMatchOutcome.Exact or NameMatchOutcome.Fuzzy);

    /// <summary>
    /// Rows a person has to deal with: nobody matched, or too many did. Never dropped
    /// silently, because a dropped row is somebody who does not appear on the wall sheet.
    /// </summary>
    public IEnumerable<ImportedRow> NeedingAttention =>
        Rows.Where(row => row.Match.Outcome is NameMatchOutcome.Unmatched or NameMatchOutcome.Ambiguous);

    /// <summary>Whether there is anything here worth committing.</summary>
    public bool HasAnythingToCommit => Dates.Count > 0 && Matched.Any();

    /// <summary>
    /// Employees who ended up on more than one row: a name listed twice, or a row the
    /// operator pointed at somebody who was already on the sheet.
    /// </summary>
    public IEnumerable<Guid> EmployeesOnMoreThanOneRow =>
        Matched
            .Where(row => row.Match.EmployeeId is not null)
            .GroupBy(row => row.Match.EmployeeId!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

    /// <summary>
    /// The availability records this import would write. Only matched rows: an unmatched
    /// name has no employee to attach to until the operator resolves it.
    /// </summary>
    /// <remarks>
    /// One record per employee per date, first row on the sheet winning. Somebody can end up
    /// on two rows either by being listed twice or by an operator pointing an unfamiliar name
    /// at them during review, and a person cannot be two things on one day. Taking the first
    /// is arbitrary but predictable, and <see cref="EmployeesOnMoreThanOneRow"/> means it is
    /// never silent.
    /// </remarks>
    public IReadOnlyList<Availability> ToAvailabilities() =>
        Matched
            .Where(row => row.Match.EmployeeId is not null)
            .SelectMany(row => row.Cells.Select(cell => new Availability
            {
                EmployeeId = row.Match.EmployeeId!.Value,
                Date = cell.Date,
                Status = cell.Status,
            }))
            .GroupBy(availability => (availability.EmployeeId, availability.Date))
            .Select(group => group.First())
            .ToList();
}

/// <summary>One employee's row on the sheet.</summary>
public sealed record ImportedRow
{
    /// <summary>Excel's own row number, so the operator can go and look at it.</summary>
    public required int RowIndex { get; init; }

    /// <summary>
    /// The name exactly as written on the sheet. Personal data, held only long enough for
    /// the operator to review the import, and never written to a log.
    /// </summary>
    public required string SheetName { get; init; }

    public required NameMatch Match { get; init; }

    public IReadOnlyList<ImportedCell> Cells { get; init; } = [];
}

/// <param name="Date">The date this cell falls under.</param>
/// <param name="Status">What the mark was taken to mean.</param>
/// <param name="Recognised">
/// False when nothing in the template matched the mark. The status falls back to the
/// template's default, and the cell is reported so that a colour nobody mentioned gets
/// noticed rather than quietly becoming a day off.
/// </param>
public sealed record ImportedCell(DateOnly Date, AvailabilityStatus Status, bool Recognised);
