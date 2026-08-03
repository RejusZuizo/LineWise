using Linewise.Application.Import;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Infrastructure.Import;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Import;

/// <summary>
/// End to end over a real .xlsx: the reader turns a file into a sheet, and the builder turns
/// that into an import. Everything here is generated in memory.
/// </summary>
public sealed class SheetReaderTests
{
    private static readonly Guid AdaId = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BramId = new("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CleoId = new("10000000-0000-0000-0000-000000000003");

    private readonly IAvailabilitySheetReader _reader = new ClosedXmlSheetReader();
    private readonly IAvailabilityImportBuilder _builder = new AvailabilityImportBuilder();

    [Fact]
    public async Task ACleanSheetImportsFully()
    {
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var result = await ImportAsync(file, SheetBuilder.WorkHolidayOrEmpty());

        Assert.Equal(5, result.Dates.Count);
        Assert.Equal(SheetBuilder.Monday, result.Dates[0]);
        Assert.Equal(4, result.Rows.Count);

        var ada = result.Rows.Single(row => row.Match.EmployeeId == AdaId);
        Assert.Equal(AvailabilityStatus.Working, ada.Cells[0].Status);
        Assert.Equal(AvailabilityStatus.Holiday, ada.Cells[2].Status);
    }

    [Fact]
    public async Task TrailingWhitespaceStillMatches()
    {
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var result = await ImportAsync(file, SheetBuilder.WorkHolidayOrEmpty());

        var bram = result.Rows.Single(row => row.RowIndex == 3);
        Assert.Equal(BramId, bram.Match.EmployeeId);
        Assert.Equal(NameMatchOutcome.Exact, bram.Match.Outcome);
    }

    [Fact]
    public async Task SurnameFirstStillMatches()
    {
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var result = await ImportAsync(file, SheetBuilder.WorkHolidayOrEmpty());

        var cleo = result.Rows.Single(row => row.RowIndex == 4);
        Assert.Equal(CleoId, cleo.Match.EmployeeId);
        Assert.Equal(NameMatchOutcome.Exact, cleo.Match.Outcome);
    }

    [Fact]
    public async Task AnUnknownNameSurfacesAsUnmatchedRatherThanBeingDropped()
    {
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var result = await ImportAsync(file, SheetBuilder.WorkHolidayOrEmpty());

        var unmatched = Assert.Single(result.NeedingAttention);
        Assert.Equal("Agency Person", unmatched.SheetName);
        Assert.Equal(5, unmatched.RowIndex);
    }

    [Fact]
    public async Task AnEmptyCellIsADayOff()
    {
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var result = await ImportAsync(file, SheetBuilder.WorkHolidayOrEmpty());

        var bram = result.Rows.Single(row => row.RowIndex == 3);
        Assert.Equal(AvailabilityStatus.Off, bram.Cells[3].Status);
        Assert.Equal(AvailabilityStatus.Overtime, bram.Cells[4].Status);
    }

    [Fact]
    public async Task APlainColouredCellResolves()
    {
        var file = WorkbookFixture.PlainColouredCells().ToArray();

        var read = await _reader.ReadAsync(file, null);

        Assert.True(read.Succeeded);
        var cell = read.Sheet!.Rows.Single(row => row.RowIndex == 2).Cells.Single(item => item.ColumnIndex == 2);

        Assert.Equal("#00B050", cell.FillColourHex);
        Assert.False(cell.FillUnreadable);
    }

    [Fact]
    public async Task AThemeColouredCellResolves()
    {
        // The default Excel colour picker produces theme colours, not plain ones. Reporting
        // these as unreadable would fail on the majority of coloured sheets in the wild.
        var file = WorkbookFixture.ThemeColouredCells().ToArray();

        var read = await _reader.ReadAsync(file, null);

        Assert.True(read.Succeeded);

        foreach (var rowIndex in (int[])[2, 3])
        {
            var cell = read.Sheet!.Rows
                .Single(row => row.RowIndex == rowIndex)
                .Cells.Single(item => item.ColumnIndex == 2);

            Assert.False(cell.FillUnreadable);
            Assert.NotNull(cell.FillColourHex);
            Assert.StartsWith("#", cell.FillColourHex, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ATintedThemeColourIsNotTheSameAsTheUntintedOne()
    {
        var file = WorkbookFixture.ThemeColouredCells().ToArray();

        var read = await _reader.ReadAsync(file, null);

        var plain = read.Sheet!.Rows.Single(row => row.RowIndex == 2).Cells.Single(item => item.ColumnIndex == 2);
        var tinted = read.Sheet!.Rows.Single(row => row.RowIndex == 3).Cells.Single(item => item.ColumnIndex == 2);

        Assert.NotEqual(plain.FillColourHex, tinted.FillColourHex);
    }

    [Fact]
    public async Task AFormulaIsNotEvaluated()
    {
        var file = WorkbookFixture.WithAFormula().ToArray();

        var read = await _reader.ReadAsync(file, null);

        // A file from outside must never have its expressions run. Whatever comes back, the
        // read has to survive the attempt without throwing.
        Assert.True(read.Succeeded);
        Assert.NotEmpty(read.Sheet!.Rows);
    }

    [Fact]
    public async Task AFileThatIsNotASpreadsheetIsRefusedWithAWarning()
    {
        var file = WorkbookFixture.NotASpreadsheet().ToArray();

        var read = await _reader.ReadAsync(file, null);

        Assert.False(read.Succeeded);
        Assert.Single(read.Warnings, warning => warning.Code == WarningCode.ImportFileNotAWorkbook);
    }

    [Fact]
    public async Task AZipThatIsNotAWorkbookIsRefused()
    {
        var file = WorkbookFixture.ZipWithoutAWorkbook().ToArray();

        var read = await _reader.ReadAsync(file, null);

        Assert.False(read.Succeeded);
        Assert.Single(read.Warnings, warning => warning.Code == WarningCode.ImportFileNotAWorkbook);
    }

    [Fact]
    public async Task AnArchiveThatExpandsEnormouslyIsRefusedBeforeItIsOpened()
    {
        var file = WorkbookFixture.DecompressionBomb().ToArray();

        var read = await _reader.ReadAsync(file, null);

        Assert.False(read.Succeeded);
        Assert.Single(read.Warnings, warning => warning.Code == WarningCode.ImportFileTooLarge);
    }

    [Fact]
    public async Task AMissingWorksheetIsReportedByName()
    {
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var read = await _reader.ReadAsync(file, "Week 32");

        Assert.False(read.Succeeded);
        Assert.Single(read.Warnings, warning => warning.Code == WarningCode.ImportWorksheetMissing);
    }

    private async Task<AvailabilityImportResult> ImportAsync(byte[] file, ImportTemplate template)
    {
        var read = await _reader.ReadAsync(file, template.WorksheetName);

        Assert.True(read.Succeeded);

        return _builder.Build(read.Sheet!, template, Workforce());
    }

    private static List<Employee> Workforce() =>
    [
        new() { Id = AdaId, FullName = "Ada Fictional" },
        new() { Id = BramId, FullName = "Bram Invented" },
        new() { Id = CleoId, FullName = "Cleo Notreal" },
    ];
}
