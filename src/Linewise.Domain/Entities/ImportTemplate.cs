using Linewise.Domain.Enums;

namespace Linewise.Domain.Entities;

/// <summary>
/// Describes the shape of one factory's availability sheet: where the names are, where the
/// dates are, and what a mark in a cell means.
/// </summary>
/// <remarks>
/// This entity is the reason a second factory with a different layout can use the product
/// without a rebuild. It is also the reason the first factory can change its own sheet
/// without one, which is not a hypothetical.
/// </remarks>
public sealed record ImportTemplate
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Which worksheet to read. Null takes the first one in the workbook.</summary>
    public string? WorksheetName { get; init; }

    /// <summary>Row holding the dates, counted from 1 as Excel counts.</summary>
    public required int HeaderRowIndex { get; init; }

    /// <summary>Column holding employee names, counted from 1.</summary>
    public required int NameColumnIndex { get; init; }

    /// <summary>First column holding a date, counted from 1.</summary>
    public required int FirstDateColumnIndex { get; init; }

    /// <summary>
    /// How to read a header that is text rather than a real date. Null means only accept
    /// cells Excel already holds as dates, which is the safer default: a sheet typed by hand
    /// will have at least one header that does not parse, and inventing a date is worse than
    /// reporting one.
    /// </summary>
    public string? DateFormat { get; init; }

    /// <summary>
    /// What a mark means. Evaluated in order, and text rules are always tried before colour
    /// rules regardless of how the list is arranged.
    /// </summary>
    public IReadOnlyList<CellStatusRule> StatusRules { get; init; } = [];

    /// <summary>
    /// What an empty cell means. Off for most sites: a blank is how people write "not in".
    /// </summary>
    public AvailabilityStatus EmptyCellStatus { get; init; } = AvailabilityStatus.Off;

    /// <summary>
    /// Whether a cell that is neither empty nor matched by any rule should be reported.
    /// Leaving this on is how a new colour nobody mentioned gets noticed rather than quietly
    /// becoming a day off.
    /// </summary>
    public bool ReportUnrecognisedCells { get; init; } = true;
}

/// <summary>One mapping from a mark on the sheet to a status.</summary>
/// <param name="Kind">Whether the rule reads the cell's text or its fill colour.</param>
/// <param name="Value">
/// The text to match, or the colour as a hex string such as <c>#FFC000</c>. Compared case
/// insensitively either way.
/// </param>
/// <param name="Status">What that mark means.</param>
public sealed record CellStatusRule(CellMatchKind Kind, string Value, AvailabilityStatus Status);
