namespace Linewise.Application.Import;

/// <summary>
/// A worksheet reduced to what an import needs to know, with no spreadsheet library in
/// sight.
/// </summary>
/// <remarks>
/// This is the seam. Reading a workbook needs ClosedXML and belongs in infrastructure;
/// deciding what the contents mean is a rule and belongs here. Splitting them at this type
/// means the matching, the status mapping and the date reading are all testable without
/// producing a single .xlsx file.
/// </remarks>
public sealed record RawSheet
{
    public required string WorksheetName { get; init; }

    public IReadOnlyList<RawRow> Rows { get; init; } = [];
}

/// <param name="RowIndex">Excel's own row number, counted from 1.</param>
public sealed record RawRow(int RowIndex, IReadOnlyList<RawCell> Cells);

/// <summary>One cell, as read.</summary>
public sealed record RawCell
{
    /// <summary>Excel's own column number, counted from 1.</summary>
    public required int ColumnIndex { get; init; }

    /// <summary>The cell's text, already trimmed. Empty when the cell holds nothing.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// The date Excel holds for this cell, when it holds a real one. Preferred over parsing
    /// the text, which is at the mercy of whatever locale typed it.
    /// </summary>
    public DateOnly? DateValue { get; init; }

    /// <summary>The fill colour as <c>#RRGGBB</c>, or null when the cell has no fill.</summary>
    public string? FillColourHex { get; init; }

    /// <summary>
    /// Set when the cell has a fill that could not be resolved to a colour: a theme colour
    /// the reader could not follow, an indexed colour outside the palette, or a conditional
    /// format. Reported rather than guessed at.
    /// </summary>
    public bool FillUnreadable { get; init; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Text) && FillColourHex is null && !FillUnreadable;
}
