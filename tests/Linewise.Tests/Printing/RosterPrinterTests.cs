using Linewise.Application.Printing;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Linewise.Infrastructure.Printing;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Printing;

public sealed class RosterPrinterTests
{
    private readonly IRosterPrinter _printer = new QuestPdfRosterPrinter();

    static RosterPrinterTests()
    {
        // The library refuses to render until the licence is declared. The application does
        // this when infrastructure is registered; a test that constructs the printer
        // directly has to say so itself.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public async Task TheFullSheetHasOnePagePerDay()
    {
        var request = Request(days: 5);

        var document = await _printer.PrintFullSheetAsync(request);

        Assert.True(PrintedRoster.IsAPdf(document));
        Assert.Equal(5, PrintedRoster.PageCount(document));

        PrintedRoster.Save(document, "full-sheet.pdf");
    }

    [Fact]
    public async Task ThePerLineSheetHasOnePagePerLine()
    {
        var request = Request(days: 5);

        var document = await _printer.PrintPerLineSheetsAsync(request);

        Assert.True(PrintedRoster.IsAPdf(document));
        Assert.Equal(request.Lines.Count, PrintedRoster.PageCount(document));

        PrintedRoster.Save(document, "per-line-sheets.pdf");
    }

    [Fact]
    public async Task TheAmendmentSlipIsASinglePage()
    {
        var scenario = Scenario();
        var before = TestEngine.Default().Generate(scenario.Build());
        var after = TestEngine.Default().Generate(scenario.Off("Ada Fictional", day: 0).Build());

        var document = await _printer.PrintAmendmentSlipAsync(
            new AmendmentRequest(Request(days: 5, roster: after), before));

        Assert.True(PrintedRoster.IsAPdf(document));
        Assert.Equal(1, PrintedRoster.PageCount(document));

        PrintedRoster.Save(document, "amendment-slip.pdf");
    }

    [Fact]
    public async Task AnAmendmentSlipWithNothingToSayStillPrints()
    {
        var roster = TestEngine.Default().Generate(Scenario().Build());

        var document = await _printer.PrintAmendmentSlipAsync(
            new AmendmentRequest(Request(days: 5, roster: roster), roster));

        // Better a slip saying nothing changed than a blank page somebody has to interpret.
        Assert.True(PrintedRoster.IsAPdf(document));
        Assert.Equal(1, PrintedRoster.PageCount(document));
    }

    [Fact]
    public async Task ADraftSaysSoOnEveryPage()
    {
        var request = Request(days: 2) with
        {
            Version = new RosterVersion
            {
                Id = Guid.NewGuid(),
                WeekStart = RosterScenarioBuilder.DefaultWeekStart,
                VersionNumber = 3,
                Status = RosterStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
            },
        };

        var document = await _printer.PrintFullSheetAsync(request);

        Assert.True(PrintedRoster.IsAPdf(document));
        PrintedRoster.Save(document, "draft-full-sheet.pdf");
    }

    [Fact]
    public async Task BiggerPaperAndBiggerTypeStillProduceAValidDocument()
    {
        var request = Request(days: 2) with
        {
            Settings = new PrintSettings
            {
                Id = Guid.NewGuid(),
                CompanyName = "A Food Manufacturer",
                PaperSize = PaperSize.A3,
                Orientation = PageOrientation.Landscape,
                BaseFontPoints = 20,
            },
        };

        var document = await _printer.PrintFullSheetAsync(request);

        Assert.True(PrintedRoster.IsAPdf(document));
        Assert.Equal(2, PrintedRoster.PageCount(document));

        PrintedRoster.Save(document, "a3-large-type.pdf");
    }

    [Fact]
    public async Task ALineWithNobodyOnItSaysSoRatherThanPrintingAGap()
    {
        var scenario = new RosterScenarioBuilder()
            .Days(1)
            .Line("Pastry", headcount: 2)
            .Line("Ovens", headcount: 2)
            .LineRequiresSkill("Ovens", "Oven ticket")
            .Employee("Ada Fictional")
            .Employee("Bram Invented");

        var document = await _printer.PrintFullSheetAsync(Request(days: 1, scenario: scenario));

        Assert.True(PrintedRoster.IsAPdf(document));

        PrintedRoster.Save(document, "empty-line.pdf");
    }

    private static PrintRequest Request(
        int days,
        RosterWeek? roster = null,
        RosterScenarioBuilder? scenario = null)
    {
        var built = scenario ?? Scenario().Days(days);
        var request = built.Build();

        return new PrintRequest
        {
            Roster = roster ?? TestEngine.Default().Generate(request),
            Version = new RosterVersion
            {
                Id = Guid.NewGuid(),
                WeekStart = RosterScenarioBuilder.DefaultWeekStart,
                VersionNumber = 2,
                Status = RosterStatus.Published,
                CreatedAtUtc = DateTime.UtcNow,
                PublishedAtUtc = DateTime.UtcNow,
                PublishedBy = "test.operator",
            },
            Lines = request.Configuration.Lines,
            Employees = request.Configuration.Employees,
            Shifts = request.Shifts,
            Settings = new PrintSettings
            {
                Id = Guid.NewGuid(),
                CompanyName = "A Food Manufacturer",
            },
        };
    }

    private static RosterScenarioBuilder Scenario() =>
        new RosterScenarioBuilder()
            .Days(5)
            .Line("Pastry", headcount: 3)
            .Line("Packing", headcount: 2)
            .Line("Chilled prep", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Employee("Eli Pretend")
            .Employee("Fen Imaginary")
            .Employee("Gus Hypothetical")
            .CanLead("Ada Fictional", "Pastry")
            .CanLead("Bram Invented", "Packing")
            .CanLead("Cleo Notreal", "Chilled prep")
            .Prefers("Dara Madeup", "Pastry", rank: 1)
            .Prefers("Eli Pretend", "Packing", rank: 1);
}
