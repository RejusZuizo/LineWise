using Linewise.Application.Import;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Tests.Builders;

/// <summary>
/// Builds a sheet as the reader would have handed it over, so import rules can be tested
/// without producing a single spreadsheet file.
/// </summary>
internal sealed class SheetBuilder
{
    private readonly List<RawRow> _rows = [];

    public static DateOnly Monday { get; } = new(2026, 8, 3);

    /// <summary>
    /// The scheme the factory moved to: a cell says work or holiday, or it says nothing.
    /// </summary>
    public static ImportTemplate WorkHolidayOrEmpty() => new()
    {
        Id = TestIds.Skill(0),
        Name = "Work, holiday or empty",
        HeaderRowIndex = 1,
        NameColumnIndex = 1,
        FirstDateColumnIndex = 2,
        StatusRules =
        [
            new CellStatusRule(CellMatchKind.Text, "work", AvailabilityStatus.Working),
            new CellStatusRule(CellMatchKind.Text, "w", AvailabilityStatus.Working),
            new CellStatusRule(CellMatchKind.Text, "holiday", AvailabilityStatus.Holiday),
            new CellStatusRule(CellMatchKind.Text, "hol", AvailabilityStatus.Holiday),
            new CellStatusRule(CellMatchKind.Text, "ot", AvailabilityStatus.Overtime),
        ],
        EmptyCellStatus = AvailabilityStatus.Off,
    };

    /// <summary>The older scheme, where the colour of the cell carried the meaning.</summary>
    public static ImportTemplate ColouredCells() => new()
    {
        Id = TestIds.Skill(1),
        Name = "Coloured cells",
        HeaderRowIndex = 1,
        NameColumnIndex = 1,
        FirstDateColumnIndex = 2,
        StatusRules =
        [
            new CellStatusRule(CellMatchKind.FillColour, "#00B050", AvailabilityStatus.Working),
            new CellStatusRule(CellMatchKind.FillColour, "#FF0000", AvailabilityStatus.Holiday),
        ],
        EmptyCellStatus = AvailabilityStatus.Off,
    };

    public SheetBuilder HeaderDates(int dayCount, int rowIndex = 1, int firstDateColumn = 2)
    {
        var cells = new List<RawCell> { new() { ColumnIndex = 1, Text = "Name" } };

        for (var day = 0; day < dayCount; day++)
        {
            cells.Add(new RawCell
            {
                ColumnIndex = firstDateColumn + day,
                Text = Monday.AddDays(day).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                DateValue = Monday.AddDays(day),
            });
        }

        _rows.Add(new RawRow(rowIndex, cells));
        return this;
    }

    /// <summary>A header row whose dates are text rather than real date values.</summary>
    public SheetBuilder HeaderTexts(params string[] headers)
    {
        var cells = new List<RawCell> { new() { ColumnIndex = 1, Text = "Name" } };

        for (var index = 0; index < headers.Length; index++)
        {
            cells.Add(new RawCell { ColumnIndex = 2 + index, Text = headers[index] });
        }

        _rows.Add(new RawRow(1, cells));
        return this;
    }

    /// <summary>A row of text marks, one per day. Null or empty leaves the cell blank.</summary>
    public SheetBuilder Row(int rowIndex, string name, params string?[] marks)
    {
        var cells = new List<RawCell> { new() { ColumnIndex = 1, Text = name } };

        for (var index = 0; index < marks.Length; index++)
        {
            cells.Add(new RawCell { ColumnIndex = 2 + index, Text = marks[index] ?? string.Empty });
        }

        _rows.Add(new RawRow(rowIndex, cells));
        return this;
    }

    /// <summary>A row of coloured cells, one per day.</summary>
    public SheetBuilder ColouredRow(int rowIndex, string name, params string?[] colours)
    {
        var cells = new List<RawCell> { new() { ColumnIndex = 1, Text = name } };

        for (var index = 0; index < colours.Length; index++)
        {
            cells.Add(new RawCell { ColumnIndex = 2 + index, FillColourHex = colours[index] });
        }

        _rows.Add(new RawRow(rowIndex, cells));
        return this;
    }

    /// <summary>A row whose cells have a fill the reader could not resolve to a colour.</summary>
    public SheetBuilder UnreadableRow(int rowIndex, string name, int cellCount)
    {
        var cells = new List<RawCell> { new() { ColumnIndex = 1, Text = name } };

        for (var index = 0; index < cellCount; index++)
        {
            cells.Add(new RawCell { ColumnIndex = 2 + index, FillUnreadable = true });
        }

        _rows.Add(new RawRow(rowIndex, cells));
        return this;
    }

    public SheetBuilder BlankRow(int rowIndex)
    {
        _rows.Add(new RawRow(rowIndex, [new RawCell { ColumnIndex = 1 }]));
        return this;
    }

    public RawSheet Build() => new()
    {
        WorksheetName = "Availability",
        Rows = _rows,
    };
}
