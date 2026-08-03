using Linewise.Application.Import;
using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Auditing;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Linewise.Tests.Persistence;
using Xunit;

namespace Linewise.Tests.Import;

/// <summary>
/// The three step pipeline over a real encrypted database and a real .xlsx.
/// </summary>
public sealed class ImportPipelineTests
{
    [Fact]
    public async Task ParsingWritesNothing()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);

        var parsed = await ParseAsync(database, template);

        Assert.NotEmpty(parsed.Rows);

        // Step one must not touch the database. An import that half happened leaves a week
        // that is neither the old one nor the new one.
        var stored = await database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.GetAsync(SheetBuilder.Monday, SheetBuilder.Monday.AddDays(7)));

        Assert.Empty(stored);
    }

    [Fact]
    public async Task CommittingWritesAvailabilityAndKeepsTheFile()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var parsed = await ParseAsync(database, template, file);
        var committed = await CommitAsync(database, template, parsed, file);

        // Three matched people across five days.
        Assert.Equal(15, committed.AvailabilityRecordsWritten);

        var stored = await database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.GetAsync(SheetBuilder.Monday, SheetBuilder.Monday.AddDays(7)));

        Assert.Equal(15, stored.Count);
        Assert.Contains(stored, availability => availability.Status == AvailabilityStatus.Holiday);
        Assert.Contains(stored, availability => availability.Status == AvailabilityStatus.Overtime);

        // The file is kept so a mapping fix can be replayed against it rather than requiring
        // a fresh copy from the office.
        var kept = await database.InScopeAsync<IImportRepository, byte[]?>(
            repository => repository.GetFileAsync(committed.ImportId));

        Assert.NotNull(kept);
        Assert.Equal(file, kept);
    }

    [Fact]
    public async Task AKeptFileCanBeParsedAgainAfterTheTemplateIsCorrected()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var parsed = await ParseAsync(database, template, file);
        var committed = await CommitAsync(database, template, parsed, file);

        // Somebody notices a fortnight later that "ot" should not have meant overtime.
        var corrected = template with
        {
            StatusRules = template.StatusRules
                .Select(rule => rule.Value == "ot" ? rule with { Status = AvailabilityStatus.Working } : rule)
                .ToList(),
        };

        await database.InScopeAsync<IImportRepository>(repository => repository.SaveTemplateAsync(corrected));

        var kept = await database.InScopeAsync<IImportRepository, byte[]?>(
            repository => repository.GetFileAsync(committed.ImportId));

        var reparsed = await database.InScopeAsync<IAvailabilityImportService, AvailabilityImportResult>(
            service => service.ParseAsync(kept!, template.Id));

        Assert.DoesNotContain(
            reparsed.Rows.SelectMany(row => row.Cells),
            cell => cell.Status == AvailabilityStatus.Overtime);
    }

    [Fact]
    public async Task AnUnknownNameCanBeAddedAsATemporaryWorkerDuringReview()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var parsed = await ParseAsync(database, template, file);
        var agencyRow = Assert.Single(parsed.NeedingAttention);

        var committed = await CommitAsync(
            database,
            template,
            parsed,
            file,
            [new ImportResolution(agencyRow.RowIndex, null, AddAsTemporary: true)]);

        Assert.Equal(1, committed.TemporaryEmployeesAdded);
        Assert.Equal(0, committed.RowsLeftUnresolved);

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        var temporary = Assert.Single(configuration.Employees, employee => employee.IsTemporary);
        Assert.Equal("Agency Person", temporary.FullName);

        // Twenty records now: four people across five days.
        Assert.Equal(20, committed.AvailabilityRecordsWritten);
    }

    [Fact]
    public async Task AnUnknownNameCanBePointedAtSomebodyAlreadyOnFile()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var parsed = await ParseAsync(database, template, file);
        var agencyRow = Assert.Single(parsed.NeedingAttention);

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());
        var existing = configuration.Employees[0].Id;

        var committed = await CommitAsync(
            database,
            template,
            parsed,
            file,
            [new ImportResolution(agencyRow.RowIndex, existing, AddAsTemporary: false)]);

        Assert.Equal(0, committed.TemporaryEmployeesAdded);
        Assert.Equal(0, committed.RowsLeftUnresolved);

        // That employee is now on two rows. One person cannot be two things on one day, so
        // the first row wins and the operator is told rather than the write failing.
        Assert.Single(committed.Warnings, warning => warning.Code == WarningCode.ImportEmployeeOnMoreThanOneRow);
        Assert.Equal(15, committed.AvailabilityRecordsWritten);
    }

    [Fact]
    public async Task AnImportStillCommitsWithRowsLeftUnresolved()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var parsed = await ParseAsync(database, template, file);
        var committed = await CommitAsync(database, template, parsed, file);

        // A manager who cannot get four fifths of a week onto the wall because one agency
        // name is unfamiliar goes back to the spreadsheet and does not come back.
        Assert.Equal(1, committed.RowsLeftUnresolved);
        Assert.Single(committed.Warnings, warning => warning.Code == WarningCode.ImportRowsLeftUnresolved);
        Assert.Equal(15, committed.AvailabilityRecordsWritten);
    }

    [Fact]
    public async Task CommittingIsRecordedInTheAuditChain()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var parsed = await ParseAsync(database, template, file);
        await CommitAsync(database, template, parsed, file);

        var entries = await database.InScopeAsync<Application.Abstractions.IAuditLog, IReadOnlyList<AuditEntry>>(
            log => log.ReadAllAsync());

        var entry = Assert.Single(entries, item => item.Action == AuditAction.ImportCommitted);
        Assert.Equal("Signed off by the shift manager.", entry.Reason);

        var verification = await database.InScopeAsync<Application.Abstractions.IAuditLog, AuditChainVerification>(
            log => log.VerifyAsync());

        Assert.True(verification.IsIntact);
    }

    [Fact]
    public async Task ATemplateRoundTripsWithItsRules()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);

        var loaded = await database.InScopeAsync<IImportRepository, ImportTemplate?>(
            repository => repository.GetTemplateAsync(template.Id));

        Assert.NotNull(loaded);
        Assert.Equal(template.StatusRules, loaded.StatusRules);
        Assert.Equal(template.EmptyCellStatus, loaded.EmptyCellStatus);
        Assert.Equal(template.FirstDateColumnIndex, loaded.FirstDateColumnIndex);
    }

    [Fact]
    public async Task ARunningImportReplacesTheWeekRatherThanMergingIntoIt()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var template = await SeedAsync(database);
        var file = WorkbookFixture.WrittenMarks().ToArray();

        var parsed = await ParseAsync(database, template, file);
        await CommitAsync(database, template, parsed, file);
        await CommitAsync(database, template, parsed, file);

        var stored = await database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.GetAsync(SheetBuilder.Monday, SheetBuilder.Monday.AddDays(7)));

        // Re-importing corrects a week. It does not stack a second copy on top of it.
        Assert.Equal(15, stored.Count);
    }

    private static async Task<ImportTemplate> SeedAsync(TemporaryDatabase database)
    {
        var template = SheetBuilder.WorkHolidayOrEmpty() with { Id = Guid.NewGuid() };

        await database.InScopeAsync<IImportRepository>(
            repository => repository.SaveTemplateAsync(template));

        foreach (var name in (string[])["Ada Fictional", "Bram Invented", "Cleo Notreal"])
        {
            await database.InScopeAsync<IConfigurationRepository>(
                repository => repository.SaveEmployeeAsync(new Employee
                {
                    Id = Guid.NewGuid(),
                    FullName = name,
                }));
        }

        return template;
    }

    private static Task<AvailabilityImportResult> ParseAsync(
        TemporaryDatabase database,
        ImportTemplate template,
        byte[]? file = null) =>
        database.InScopeAsync<IAvailabilityImportService, AvailabilityImportResult>(
            service => service.ParseAsync(file ?? WorkbookFixture.WrittenMarks().ToArray(), template.Id));

    private static Task<ImportCommitResult> CommitAsync(
        TemporaryDatabase database,
        ImportTemplate template,
        AvailabilityImportResult parsed,
        byte[] file,
        IReadOnlyList<ImportResolution>? resolutions = null) =>
        database.InScopeAsync<IAvailabilityImportService, ImportCommitResult>(
            service => service.CommitAsync(new ImportCommitRequest
            {
                TemplateId = template.Id,
                FileName = "week-32.xlsx",
                FileContent = file,
                Parsed = parsed,
                Resolutions = resolutions ?? [],
                Reason = "Signed off by the shift manager.",
            }));
}
