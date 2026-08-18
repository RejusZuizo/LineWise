using ClosedXML.Excel;
using Linewise.Application.Import;
using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Xunit;

namespace Linewise.Tests.Persistence;

/// <summary>
/// The demo, end to end: import a sheet nobody has seen before, then roster the week.
/// </summary>
/// <remarks>
/// A first import has nobody on file, so every row is unmatched and every row is added as
/// temporary. That is the ordinary case in food production rather than an edge one, and it
/// is the path that decides whether the product is usable on the day it is installed.
/// </remarks>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class ImportThenGenerateTests
{
    private const int People = 60;

    private static readonly DateOnly Monday = new(2026, 8, 3);

    [Fact]
    public async Task A_sheet_of_sixty_strangers_becomes_a_rostered_week()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await GivenLines(database, ("Ovens", 6), ("Packing", 8), ("Chilled prep", 5));


        var parsed = await Parse(database, Sheet());

        // Nobody is on file yet, so the matcher settles none of them.
        Assert.Equal(People, parsed.Result.NeedingAttention.Count());
        Assert.Empty(parsed.Result.Matched);

        var committed = await Commit(database, parsed, addEveryoneAsTemporary: true);

        Assert.Equal(People, committed.TemporaryEmployeesAdded);
        Assert.True(committed.AvailabilityRecordsWritten > 0);

        var stored = await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        Assert.NotEmpty(stored.Roster.AllAssignments);

        // Nobody is placed twice on the same day, which is the hard constraint most likely
        // to be broken by a path that assembles its own inputs.
        var duplicates = stored.Roster.AllAssignments
            .GroupBy(assignment => (assignment.Date, assignment.EmployeeId))
            .Where(group => group.Count() > 1);

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// The review step exists so that nothing is written until somebody says so. Parsing a
    /// sheet and then closing the window must leave the database exactly as it was.
    /// </summary>
    [Fact]
    public async Task Parsing_writes_nothing()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await Parse(database, Sheet());

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        Assert.Empty(configuration.Employees);
    }

    /// <summary>
    /// Leaving the boxes unticked is a decision, not a failure. A name the operator chose
    /// to ignore should not add anybody.
    /// </summary>
    [Fact]
    public async Task Rows_left_unresolved_add_nobody()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var parsed = await Parse(database, Sheet());
        var committed = await Commit(database, parsed, addEveryoneAsTemporary: false);

        Assert.Equal(0, committed.TemporaryEmployeesAdded);
        Assert.Equal(People, committed.RowsLeftUnresolved);
    }

    /// <summary>
    /// The Tuesday afternoon case. Somebody rings in sick, the manager marks them absent,
    /// and then a corrected sheet arrives covering the same week.
    /// </summary>
    /// <remarks>
    /// Without provenance on an availability record this is where the morning's work is
    /// silently undone: the sheet predates the phone call and says the person is in.
    /// </remarks>
    [Fact]
    public async Task A_re_import_keeps_what_the_manager_set_by_hand()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var parsed = await Parse(database, Sheet());
        await Commit(database, parsed, addEveryoneAsTemporary: true);

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        var absentee = configuration.Employees[0].Id;

        await database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.SetManualAsync(absentee, Monday, AvailabilityStatus.Off));

        var again = await Commit(database, await Parse(database, Sheet()), addEveryoneAsTemporary: false);

        var availability = await database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.GetAsync(Monday, Monday.AddDays(7)));

        var monday = availability.Single(record => record.EmployeeId == absentee && record.Date == Monday);

        Assert.Equal(AvailabilityStatus.Off, monday.Status);
        Assert.Equal(AvailabilitySource.Manual, monday.Source);

        // Kept, and said out loud. The review screen names who and which day rather than
        // reporting a count nobody can act on.
        var kept = Assert.Single(
            again.Warnings,
            warning => warning.Code == WarningCode.ImportManualAvailabilityKept);

        Assert.Equal(absentee, kept.EmployeeId);
        Assert.Equal(Monday, kept.Date);

        // And the record it did not write is not counted as one it did.
        Assert.Equal(availability.Count - 1, again.AvailabilityRecordsWritten);
    }

    /// <summary>
    /// A sheet laid out nothing like the template: a title block above the dates and a
    /// payroll number to the left of the names. The template alone finds nothing; reading
    /// the sheet finds all of it.
    /// </summary>
    [Fact]
    public async Task A_sheet_the_template_does_not_fit_is_read_anyway()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var parsed = await Parse(database, AwkwardSheet());

        Assert.NotNull(parsed.Result.DetectedLayout);
        Assert.Equal(4, parsed.Result.DetectedLayout!.HeaderRowIndex);
        Assert.Equal(2, parsed.Result.DetectedLayout.NameColumnIndex);

        Assert.Equal(People, parsed.Result.Rows.Count);
        Assert.Contains(
            parsed.Result.Warnings,
            warning => warning.Code == WarningCode.ImportLayoutDetected);
    }

    /// <summary>
    /// The whole of "it adapts": the shape is worked out once, written onto the template,
    /// and the next sheet of that shape is parsed rather than detected again.
    /// </summary>
    [Fact]
    public async Task Committing_teaches_the_template_the_shape()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var first = await Parse(database, AwkwardSheet());
        var committed = await Commit(database, first, addEveryoneAsTemporary: true);

        Assert.Contains(committed.Warnings, warning => warning.Code == WarningCode.ImportLayoutLearned);

        var template = await database.InScopeAsync<IImportRepository, ImportTemplate?>(
            repository => repository.GetTemplateAsync(first.TemplateId));

        Assert.Equal(4, template!.HeaderRowIndex);
        Assert.Equal(2, template.NameColumnIndex);
        Assert.Equal(3, template.FirstDateColumnIndex);

        // Second time round, the same shape needs no detecting.
        var again = await database.InScopeAsync<IAvailabilityImportService, AvailabilityImportResult>(
            service => service.ParseAsync(AwkwardSheet(), first.TemplateId));

        Assert.Null(again.DetectedLayout);
        Assert.Equal(People, again.Rows.Count);
    }

    /// <summary>
    /// A template that fits is never overruled. Detection is a rescue, not an opinion.
    /// </summary>
    [Fact]
    public async Task A_sheet_the_template_does_fit_is_not_detected_at_all()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var parsed = await Parse(database, Sheet());

        Assert.Null(parsed.Result.DetectedLayout);
        Assert.DoesNotContain(
            parsed.Result.Warnings,
            warning => warning.Code == WarningCode.ImportLayoutDetected);
    }

    /// <summary>
    /// The other half of learning. A spelling the operator ties to somebody is a fact about
    /// that person, not about this week's sheet, so next week it matches on its own instead
    /// of being handed back every Monday.
    /// </summary>
    [Fact]
    public async Task A_name_you_match_once_matches_by_itself_next_time()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        // Somebody already on file, spelled the way the sheet spells them.
        var employeeId = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = employeeId, FullName = "Ada Fictional" }));

        var parsed = await Parse(database, OneRowSheet("A. Fictional (nights)"));

        var unmatched = Assert.Single(parsed.Result.NeedingAttention);

        var committed = await database.InScopeAsync<IAvailabilityImportService, ImportCommitResult>(
            service => service.CommitAsync(new ImportCommitRequest
            {
                TemplateId = parsed.TemplateId,
                FileName = "sample-availability.xlsx",
                FileContent = parsed.File,
                Parsed = parsed.Result,
                Resolutions = [new ImportResolution(unmatched.RowIndex, employeeId, AddAsTemporary: false)],
                Reason = "Test import.",
            }));

        Assert.Contains(committed.Warnings, warning => warning.Code == WarningCode.ImportNamesLearned);

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        Assert.Contains("A. Fictional (nights)", configuration.Employees.Single().Aliases);

        // The same sheet again, and nobody has to be asked about it.
        var again = await database.InScopeAsync<IAvailabilityImportService, AvailabilityImportResult>(
            service => service.ParseAsync(OneRowSheet("A. Fictional (nights)"), parsed.TemplateId));

        Assert.Empty(again.NeedingAttention);
        Assert.Equal(employeeId, Assert.Single(again.Matched).Match.EmployeeId);
    }

    /// <summary>Nothing is learned when the spelling is simply the name already on file.</summary>
    [Fact]
    public async Task Matching_somebody_to_their_own_name_learns_nothing()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employeeId = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = employeeId, FullName = "Ada Fictional" }));

        var parsed = await Parse(database, OneRowSheet("Ada Fictional"));
        var committed = await Commit(database, parsed, addEveryoneAsTemporary: false);

        Assert.DoesNotContain(committed.Warnings, warning => warning.Code == WarningCode.ImportNamesLearned);

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        Assert.Empty(configuration.Employees.Single().Aliases);
    }

    /// <summary>One name, laid out the way the template expects.</summary>
    private static byte[] OneRowSheet(string name)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Availability");

        sheet.Cell(1, 1).Value = "Name";

        for (var day = 0; day < 7; day++)
        {
            sheet.Cell(1, 2 + day).Value = Monday.AddDays(day).ToDateTime(TimeOnly.MinValue);
            sheet.Cell(1, 2 + day).Style.DateFormat.Format = "yyyy-mm-dd";
        }

        sheet.Cell(2, 1).Value = name;

        for (var day = 0; day < 7; day++)
        {
            sheet.Cell(2, 2 + day).Value = "work";
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    /// <summary>
    /// The same sheet the site sends, but with a title block above it and a payroll number
    /// beside the names. Every index the default template holds is wrong for it.
    /// </summary>
    private static byte[] AwkwardSheet()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Availability");

        sheet.Cell(1, 1).Value = "A Food Manufacturer";
        sheet.Cell(2, 1).Value = "Weekly availability";

        sheet.Cell(4, 1).Value = "No.";
        sheet.Cell(4, 2).Value = "Name";

        for (var day = 0; day < 7; day++)
        {
            sheet.Cell(4, 3 + day).Value = Monday.AddDays(day).ToDateTime(TimeOnly.MinValue);
            sheet.Cell(4, 3 + day).Style.DateFormat.Format = "yyyy-mm-dd";
        }

        for (var person = 0; person < People; person++)
        {
            sheet.Cell(person + 5, 1).Value = 1000 + person;
            sheet.Cell(person + 5, 2).Value = $"Person {person + 1:00} Fictional";

            for (var day = 0; day < 7; day++)
            {
                sheet.Cell(person + 5, 3 + day).Value = day >= 5 ? string.Empty : "work";
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static async Task<(AvailabilityImportResult Result, Guid TemplateId, byte[] File)> Parse(
        TemporaryDatabase database,
        byte[] file)
    {
        var template = ImportTemplateDefaults.Create();

        await database.InScopeAsync<IImportRepository>(
            repository => repository.SaveTemplateAsync(template));

        var result = await database.InScopeAsync<IAvailabilityImportService, AvailabilityImportResult>(
            service => service.ParseAsync(file, template.Id));

        return (result, template.Id, file);
    }

    private static Task<ImportCommitResult> Commit(
        TemporaryDatabase database,
        (AvailabilityImportResult Result, Guid TemplateId, byte[] File) parsed,
        bool addEveryoneAsTemporary) =>
        database.InScopeAsync<IAvailabilityImportService, ImportCommitResult>(
            service => service.CommitAsync(new ImportCommitRequest
            {
                TemplateId = parsed.TemplateId,
                FileName = "sample-availability.xlsx",
                FileContent = parsed.File,
                Parsed = parsed.Result,
                Resolutions = addEveryoneAsTemporary
                    ?
                    [
                        .. parsed.Result.NeedingAttention.Select(row =>
                            new ImportResolution(row.RowIndex, null, AddAsTemporary: true)),
                    ]
                    : [],
                Reason = "Test import.",
            }));

    private static async Task GivenLines(
        TemporaryDatabase database,
        params (string Name, int Headcount)[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            await database.InScopeAsync<IConfigurationRepository>(configuration =>
                configuration.SaveLineAsync(new ProductionLine
                {
                    Id = Guid.NewGuid(),
                    Name = lines[i].Name,
                    RequiredHeadcount = lines[i].Headcount,
                    DisplayOrder = i + 1,
                }));
        }
    }

    /// <summary>
    /// The same shape as the sheet the site sends: names down the first column, dates across
    /// the first row, written marks in the cells. Generated rather than committed, because a
    /// sheet from the factory is a list of who works where.
    /// </summary>
    private static byte[] Sheet()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Availability");

        sheet.Cell(1, 1).Value = "Name";

        for (var day = 0; day < 7; day++)
        {
            sheet.Cell(1, 2 + day).Value = Monday.AddDays(day).ToDateTime(TimeOnly.MinValue);
            sheet.Cell(1, 2 + day).Style.DateFormat.Format = "yyyy-mm-dd";
        }

        for (var person = 0; person < People; person++)
        {
            sheet.Cell(person + 2, 1).Value = $"Person {person + 1:00} Fictional";

            for (var day = 0; day < 7; day++)
            {
                // Weekdays busy, weekend thin, a scattering of holiday and overtime. Fixed
                // rather than random, so a failure is reproducible.
                var pattern = (person + day) % 9;
                var weekend = day >= 5;

                string mark;

                if (pattern == 1)
                {
                    mark = "holiday";
                }
                else if (pattern == 3)
                {
                    mark = "ot";
                }
                else if (weekend && pattern % 2 == 0)
                {
                    mark = string.Empty;
                }
                else
                {
                    mark = "work";
                }

                sheet.Cell(person + 2, 2 + day).Value = mark;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}
