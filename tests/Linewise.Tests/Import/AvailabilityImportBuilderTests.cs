using Linewise.Application.Import;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Import;

public sealed class AvailabilityImportBuilderTests
{
    private static readonly Guid AdaId = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BramId = new("10000000-0000-0000-0000-000000000002");

    private readonly IAvailabilityImportBuilder _builder = new AvailabilityImportBuilder();

    [Fact]
    public void ACleanSheetImportsFully()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 5)
            .Row(2, "Ada Fictional", "work", "work", "hol", "work", "work")
            .Row(3, "Bram Invented", "work", null, "work", "work", "ot")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        Assert.Equal(5, result.Dates.Count);
        Assert.Equal(2, result.Rows.Count);
        Assert.Empty(result.NeedingAttention);
        Assert.True(result.HasAnythingToCommit);

        var ada = result.Rows.Single(row => row.Match.EmployeeId == AdaId);
        Assert.Equal(AvailabilityStatus.Working, ada.Cells[0].Status);
        Assert.Equal(AvailabilityStatus.Holiday, ada.Cells[2].Status);

        var bram = result.Rows.Single(row => row.Match.EmployeeId == BramId);

        // An empty cell is a day off. Silence is never read as availability.
        Assert.Equal(AvailabilityStatus.Off, bram.Cells[1].Status);
        Assert.Equal(AvailabilityStatus.Overtime, bram.Cells[4].Status);
    }

    [Fact]
    public void MarksAreReadWithoutRegardToCase()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 2)
            .Row(2, "Ada Fictional", "WORK", "  Holiday ")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        var ada = Assert.Single(result.Rows);
        Assert.Equal(AvailabilityStatus.Working, ada.Cells[0].Status);
        Assert.Equal(AvailabilityStatus.Holiday, ada.Cells[1].Status);
    }

    [Fact]
    public void ColouredCellsStillWorkForASiteThatUsesThem()
    {
        // The old scheme has to keep working. The meaning of a mark is configuration, not
        // something compiled in, which is the whole point of the template.
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 3)
            .ColouredRow(2, "Ada Fictional", "#00B050", "FFFF0000", null)
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.ColouredCells(), Workforce());

        var ada = Assert.Single(result.Rows);
        Assert.Equal(AvailabilityStatus.Working, ada.Cells[0].Status);

        // Eight digit ARGB and six digit RGB are the same colour.
        Assert.Equal(AvailabilityStatus.Holiday, ada.Cells[1].Status);
        Assert.Equal(AvailabilityStatus.Off, ada.Cells[2].Status);
    }

    [Fact]
    public void AMarkNobodyConfiguredIsReportedRatherThanTreatedAsADayOff()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 2)
            .Row(2, "Ada Fictional", "work", "TRAINING")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        var warning = Assert.Single(result.Warnings, item => item.Code == WarningCode.ImportCellUnrecognised);
        Assert.Equal(WarningSeverity.Notice, warning.Severity);

        var ada = Assert.Single(result.Rows);
        Assert.False(ada.Cells[1].Recognised);
        Assert.Equal(AvailabilityStatus.Off, ada.Cells[1].Status);
    }

    [Fact]
    public void AColourThatCannotBeResolvedIsReportedRatherThanCrashing()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 2)
            .UnreadableRow(2, "Ada Fictional", cellCount: 2)
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.ColouredCells(), Workforce());

        Assert.Equal(2, result.Warnings.Count(item => item.Code == WarningCode.ImportCellColourUnreadable));
        Assert.All(Assert.Single(result.Rows).Cells, cell => Assert.False(cell.Recognised));
    }

    [Fact]
    public void AnUnknownNameSurfacesForReviewRatherThanBeingDropped()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 2)
            .Row(2, "Ada Fictional", "work", "work")
            .Row(3, "Agency Person", "work", "work")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        // Food production runs on agency staff. A row that vanishes is somebody who does not
        // appear on the wall sheet on Monday morning.
        Assert.Equal(2, result.Rows.Count);

        var unmatched = Assert.Single(result.NeedingAttention);
        Assert.Equal("Agency Person", unmatched.SheetName);
        Assert.Equal(3, unmatched.RowIndex);
        Assert.Single(result.Warnings, item => item.Code == WarningCode.ImportNameUnmatched);
    }

    [Fact]
    public void ALooseNameMatchIsAcceptedButFlaggedForChecking()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 1)
            .Row(2, "Ada Fictionel", "work")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        Assert.Equal(AdaId, Assert.Single(result.Rows).Match.EmployeeId);
        Assert.Single(result.Warnings, item => item.Code == WarningCode.ImportNameMatchedLoosely);
    }

    [Fact]
    public void DatesWrittenAsTextAreReadWhenTheTemplateSaysHow()
    {
        var sheet = new SheetBuilder()
            .HeaderTexts("03/08/2026", "04/08/2026")
            .Row(2, "Ada Fictional", "work", "work")
            .Build();

        var template = SheetBuilder.WorkHolidayOrEmpty() with { DateFormat = "dd/MM/yyyy" };

        var result = _builder.Build(sheet, template, Workforce());

        Assert.Equal([new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 4)], result.Dates);
    }

    [Fact]
    public void AHeaderThatIsNotADateIsReportedRatherThanGuessedAt()
    {
        var sheet = new SheetBuilder()
            .HeaderTexts("03/08/2026", "Notes")
            .Row(2, "Ada Fictional", "work", "work")
            .Build();

        var template = SheetBuilder.WorkHolidayOrEmpty() with { DateFormat = "dd/MM/yyyy" };

        var result = _builder.Build(sheet, template, Workforce());

        Assert.Single(result.Dates);
        Assert.Single(result.Warnings, item => item.Code == WarningCode.ImportDateHeaderUnreadable);
    }

    [Fact]
    public void ASheetWithNoReadableDatesImportsNothingAndSaysSo()
    {
        var sheet = new SheetBuilder()
            .HeaderTexts("Notes", "More notes")
            .Row(2, "Ada Fictional", "work", "work")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        Assert.Empty(result.Rows);
        Assert.False(result.HasAnythingToCommit);
        Assert.Single(result.Warnings, item => item.Code == WarningCode.ImportNoDateColumns);
    }

    [Fact]
    public void BlankRowsAreSkippedWithoutComplaint()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 1)
            .Row(2, "Ada Fictional", "work")
            .BlankRow(3)
            .Row(4, "Bram Invented", "work")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        Assert.Equal(2, result.Rows.Count);
        Assert.DoesNotContain(result.Warnings, item => item.Code == WarningCode.ImportNameUnmatched);
    }

    [Fact]
    public void OnlyMatchedRowsBecomeAvailabilityRecords()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 2)
            .Row(2, "Ada Fictional", "work", "hol")
            .Row(3, "Agency Person", "work", "work")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        var availabilities = result.ToAvailabilities();

        Assert.Equal(2, availabilities.Count);
        Assert.All(availabilities, availability => Assert.Equal(AdaId, availability.EmployeeId));
        Assert.Equal(AvailabilityStatus.Holiday, availabilities[1].Status);
    }

    [Fact]
    public void ImportWarningsCarryNoNames()
    {
        var sheet = new SheetBuilder()
            .HeaderDates(dayCount: 1)
            .Row(2, "Agency Person", "work")
            .Build();

        var result = _builder.Build(sheet, SheetBuilder.WorkHolidayOrEmpty(), Workforce());

        Assert.All(
            result.Warnings,
            warning => Assert.DoesNotContain("Agency", warning.Message, StringComparison.Ordinal));
    }

    private static List<Employee> Workforce() =>
    [
        new() { Id = AdaId, FullName = "Ada Fictional" },
        new() { Id = BramId, FullName = "Bram Invented" },
    ];
}
