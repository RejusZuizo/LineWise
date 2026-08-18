using Linewise.Application.Printing;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Linewise.Infrastructure.Printing;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Printing;

[Trait(TestCategories.Key, TestCategories.Integration)]
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

    /// <summary>
    /// Somebody marked absent keeps their assignment, so that the screen can grey the name
    /// rather than lose it. The sheet on the wall must not be the one place that fact fails
    /// to arrive: a name on a wall is read as somebody who will be standing there.
    /// </summary>
    /// <remarks>
    /// Asserted on length rather than by reading the text out. QuestPDF embeds its fonts as
    /// subsets, so a name is glyph indices in a compressed stream by the time it reaches the
    /// file and no amount of searching the bytes will find it. Length is the honest proxy:
    /// the same layout with one fewer name in it is a measurably smaller document.
    /// <para>
    /// The stronger half of this is the second assertion. Printing a roster with an absence
    /// produces a document of exactly the size of one printed from a roster that never had
    /// that person on the line, which is the claim being made.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Somebody_marked_absent_is_left_off_the_printed_sheet()
    {
        var request = Request(days: 1);
        var ada = request.Employees.Single(employee => employee.FullName == "Ada Fictional");
        var day = RosterScenarioBuilder.DefaultWeekStart;

        var withAda = await _printer.PrintFullSheetAsync(request);

        var absent = await _printer.PrintFullSheetAsync(request with
        {
            Availabilities =
            [
                new Availability { EmployeeId = ada.Id, Date = day, Status = AvailabilityStatus.Off },
            ],
        });

        var neverPlaced = await _printer.PrintFullSheetAsync(request with
        {
            Roster = request.Roster with
            {
                Days =
                [
                    .. request.Roster.Days.Select(rosterDay => rosterDay with
                    {
                        Assignments =
                            [.. rosterDay.Assignments.Where(a => a.EmployeeId != ada.Id)],
                    }),
                ],
            },
        });

        Assert.NotEqual(withAda.Length, absent.Length);
        Assert.Equal(neverPlaced.Length, absent.Length);

        PrintedRoster.Save(absent, "absence-full-sheet.pdf");
    }

    /// <summary>
    /// The safe direction. A week whose sheet was never imported has no availability at all,
    /// and reading that silence as absence would print a sheet with nobody on it.
    /// </summary>
    [Fact]
    public async Task Knowing_nothing_about_availability_prints_everybody()
    {
        var request = Request(days: 1);

        var silent = await _printer.PrintFullSheetAsync(request);
        var stated = await _printer.PrintFullSheetAsync(request with
        {
            Availabilities =
            [
                .. request.Employees.Select(employee => new Availability
                {
                    EmployeeId = employee.Id,
                    Date = RosterScenarioBuilder.DefaultWeekStart,
                    Status = AvailabilityStatus.Working,
                }),
            ],
        });

        Assert.Equal(stated.Length, silent.Length);
    }

    /// <summary>
    /// The layout notes belong on the sheet pinned at the line, where the people they are
    /// written for will see them, and nowhere else.
    /// </summary>
    [Fact]
    public async Task Layout_notes_reach_the_line_s_own_sheet_and_not_the_full_one()
    {
        var plain = Request(days: 1);

        var annotated = plain with
        {
            Lines =
            [
                .. plain.Lines.Select((line, index) => index == 0
                    ? line with { LayoutNotes = "Loader at the cold end, two on the belt." }
                    : line),
            ],
        };

        var perLineBefore = await _printer.PrintPerLineSheetsAsync(plain);
        var perLineAfter = await _printer.PrintPerLineSheetsAsync(annotated);

        var fullBefore = await _printer.PrintFullSheetAsync(plain);
        var fullAfter = await _printer.PrintFullSheetAsync(annotated);

        Assert.NotEqual(perLineBefore.Length, perLineAfter.Length);
        Assert.Equal(fullBefore.Length, fullAfter.Length);

        PrintedRoster.Save(perLineAfter, "per-line-with-notes.pdf");
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
