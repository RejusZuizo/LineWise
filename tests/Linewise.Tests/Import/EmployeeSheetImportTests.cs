using Linewise.Application.Import;
using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Tests.Builders;
using Linewise.Tests.Persistence;
using Xunit;

namespace Linewise.Tests.Import;

[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class EmployeeSheetImportTests
{
    private static readonly EmployeeSheetTemplate Template =
        new(WorksheetName: null, HeaderRowIndex: 1, NameColumnIndex: 1, AliasColumnIndex: 2);

    [Fact]
    public async Task PeopleAlreadyOnFileAreListedButNotDuplicated()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AddAsync(database, "Ada Fictional");

        var parsed = await ParseAsync(database);

        // Two rows name Ada, one of them surname first. Neither should create a second
        // record: two records for one person is how a roster double books them.
        Assert.Equal(2, parsed.AlreadyKnown.Count());
        Assert.Equal(2, parsed.NewPeople.Count());
    }

    [Fact]
    public async Task OnlyNewPeopleAreAdded()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await AddAsync(database, "Ada Fictional");

        var parsed = await ParseAsync(database);

        var added = await database.InScopeAsync<IEmployeeSheetImporter, int>(
            importer => importer.CommitAsync(parsed));

        Assert.Equal(2, added);

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        Assert.Equal(3, configuration.Employees.Count);
    }

    [Fact]
    public async Task AliasesComeAcrossFromTheSheet()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var parsed = await ParseAsync(database);
        await database.InScopeAsync<IEmployeeSheetImporter>(importer => importer.CommitAsync(parsed));

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        var dara = Assert.Single(configuration.Employees, employee => employee.FullName == "Dara Madeup");

        // Populating these up front is what stops the same names being resolved by hand every
        // week for the rest of the product's life.
        Assert.Equal(2, dara.Aliases.Count);
        Assert.Contains("Dara M", dara.Aliases, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ImportedAliasesAreThenUsedForMatching()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var parsed = await ParseAsync(database);
        await database.InScopeAsync<IEmployeeSheetImporter>(importer => importer.CommitAsync(parsed));

        var configuration = await database.InScopeAsync<IConfigurationRepository, RosterConfiguration>(
            repository => repository.GetAsync());

        var match = new EmployeeNameMatcher(configuration.Employees).Match("D Madeup");

        Assert.Equal(NameMatchOutcome.Exact, match.Outcome);
    }

    private static Task<EmployeeImportResult> ParseAsync(TemporaryDatabase database) =>
        database.InScopeAsync<IEmployeeSheetImporter, EmployeeImportResult>(
            importer => importer.ParseAsync(WorkbookFixture.EmployeeList().ToArray(), Template));

    private static Task AddAsync(TemporaryDatabase database, string fullName) =>
        database.InScopeAsync<IConfigurationRepository>(
            repository => repository.SaveEmployeeAsync(new Employee
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
            }));
}
