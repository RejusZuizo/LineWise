using Linewise.Application.Import;
using Xunit;

namespace Linewise.Tests.Import;

/// <summary>
/// Working out where the names and the dates are on a sheet nobody has described.
/// </summary>
/// <remarks>
/// Built against the two shapes this project has actually seen, plus deliberate distortions
/// of them. There is no real sheet from the site to test against, which is the honest limit
/// of this and is recorded in ADR 0017: detection finds the shapes somebody thought to
/// invent, and a sheet nobody imagined will still need a template built by hand.
/// </remarks>
public sealed class LayoutDetectionTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);

    private readonly IImportLayoutDetector _detector = new ImportLayoutDetector();

    [Fact]
    public void The_ordinary_shape_is_found()
    {
        var layout = _detector.Detect(Sheet(headerRow: 1, nameColumn: 1, firstDateColumn: 2));

        Assert.True(layout.Found);
        Assert.Equal(1, layout.HeaderRowIndex);
        Assert.Equal(1, layout.NameColumnIndex);
        Assert.Equal(2, layout.FirstDateColumnIndex);
        Assert.Equal(7, layout.DateColumnCount);
    }

    /// <summary>
    /// A title block, a logo and a blank line above the dates is an ordinary spreadsheet and
    /// the exact thing a fixed template gets wrong.
    /// </summary>
    [Fact]
    public void Dates_further_down_the_sheet_are_found()
    {
        var layout = _detector.Detect(Sheet(headerRow: 5, nameColumn: 1, firstDateColumn: 2));

        Assert.True(layout.Found);
        Assert.Equal(5, layout.HeaderRowIndex);
        Assert.Equal(1, layout.NameColumnIndex);
    }

    [Fact]
    public void Names_further_across_the_sheet_are_found()
    {
        var layout = _detector.Detect(Sheet(headerRow: 1, nameColumn: 3, firstDateColumn: 4));

        Assert.True(layout.Found);
        Assert.Equal(3, layout.NameColumnIndex);
        Assert.Equal(4, layout.FirstDateColumnIndex);
    }

    /// <summary>
    /// A payroll number in column A and the name in column B, both filled in completely.
    /// The first version of this counted non-empty cells, tied, and took the numbers.
    /// </summary>
    [Fact]
    public void A_column_of_numbers_is_not_a_column_of_names()
    {
        var sheet = new RawSheet
        {
            WorksheetName = "Availability",
            Rows =
            [
                new RawRow(1, [.. Dates(startColumn: 3)]),
                new RawRow(2, [Text(1, "1001"), Text(2, "Ada Fictional"), Text(3, "work")]),
                new RawRow(3, [Text(1, "1002"), Text(2, "Bram Invented"), Text(3, "work")]),
                new RawRow(4, [Text(1, "1003"), Text(2, "Cleo Notreal"), Text(3, "work")]),
            ],
        };

        Assert.Equal(2, _detector.Detect(sheet).NameColumnIndex);
    }

    /// <summary>
    /// One date is as likely to be a "printed on" stamp in a corner as the start of a week.
    /// Refusing is the right answer: a wrong layout that half works produces a plausible
    /// roster built from the wrong cells.
    /// </summary>
    [Fact]
    public void A_single_stray_date_is_not_a_header_row()
    {
        var sheet = new RawSheet
        {
            WorksheetName = "Availability",
            Rows =
            [
                new RawRow(1, [Text(1, "Printed"), Date(2, Monday)]),
                new RawRow(2, [Text(1, "Ada Fictional")]),
            ],
        };

        Assert.False(_detector.Detect(sheet).Found);
    }

    [Fact]
    public void A_sheet_with_no_dates_at_all_is_not_guessed_at()
    {
        var sheet = new RawSheet
        {
            WorksheetName = "Notes",
            Rows = [new RawRow(1, [Text(1, "Ada Fictional"), Text(2, "work")])],
        };

        Assert.False(_detector.Detect(sheet).Found);
    }

    [Fact]
    public void A_sheet_of_dates_with_nobody_on_it_is_not_a_layout()
    {
        var sheet = new RawSheet
        {
            WorksheetName = "Availability",
            Rows = [new RawRow(1, [.. Dates(startColumn: 2)])],
        };

        // Dates but no names column to the left of them, so there is nothing to import.
        Assert.False(_detector.Detect(sheet).Found);
    }

    /// <summary>
    /// The row with a whole week on it beats a row with a couple of stray dates, wherever
    /// each sits.
    /// </summary>
    [Fact]
    public void The_row_with_the_most_dates_wins()
    {
        var sheet = new RawSheet
        {
            WorksheetName = "Availability",
            Rows =
            [
                new RawRow(1, [Text(1, "Week of"), Date(2, Monday), Date(3, Monday.AddDays(7))]),
                new RawRow(3, [Text(1, "Name"), .. Dates(startColumn: 2)]),
                new RawRow(4, [Text(1, "Ada Fictional"), Text(2, "work")]),
            ],
        };

        var layout = _detector.Detect(sheet);

        Assert.Equal(3, layout.HeaderRowIndex);
        Assert.Equal(7, layout.DateColumnCount);
    }

    /// <summary>
    /// Detection produces a template rather than replacing one. Everything the template says
    /// about what a mark means is untouched; only where to look changes. ADR 0017.
    /// </summary>
    [Fact]
    public void Applying_a_layout_keeps_everything_else_about_the_template()
    {
        var template = ImportTemplateDefaults.Create();
        var applied = new DetectedLayout(5, 3, 4, 7).ApplyTo(template);

        Assert.Equal(5, applied.HeaderRowIndex);
        Assert.Equal(3, applied.NameColumnIndex);
        Assert.Equal(4, applied.FirstDateColumnIndex);

        Assert.Equal(template.StatusRules.Count, applied.StatusRules.Count);
        Assert.Equal(template.EmptyCellStatus, applied.EmptyCellStatus);
        Assert.Equal(template.Id, applied.Id);
    }

    private static RawSheet Sheet(int headerRow, int nameColumn, int firstDateColumn)
    {
        var rows = new List<RawRow>
        {
            new(headerRow, [Text(nameColumn, "Name"), .. Dates(firstDateColumn)]),
        };

        foreach (var (name, offset) in new[] { ("Ada Fictional", 1), ("Bram Invented", 2), ("Cleo Notreal", 3) })
        {
            rows.Add(new RawRow(
                headerRow + offset,
                [Text(nameColumn, name), Text(firstDateColumn, "work")]));
        }

        return new RawSheet { WorksheetName = "Availability", Rows = rows };
    }

    private static IEnumerable<RawCell> Dates(int startColumn) =>
        Enumerable.Range(0, 7).Select(day => Date(startColumn + day, Monday.AddDays(day)));

    private static RawCell Date(int column, DateOnly date) =>
        new() { ColumnIndex = column, Text = date.ToString("yyyy-MM-dd"), DateValue = date };

    private static RawCell Text(int column, string text) =>
        new() { ColumnIndex = column, Text = text };
}
