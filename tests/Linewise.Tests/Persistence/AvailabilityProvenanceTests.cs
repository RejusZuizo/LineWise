using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Persistence;

/// <summary>
/// What an import is allowed to overwrite. The interesting case is the second import: a
/// week corrected and re-sent must not put back the person the manager marked absent that
/// morning.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class AvailabilityProvenanceTests
{
    private static readonly DateOnly Monday = RosterScenarioBuilder.DefaultWeekStart;

    [Fact]
    public async Task AnImportedRecordSaysItCameFromTheSheet()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await ImportAsync(database, [Working(employee, day: 0)]);

        var stored = Assert.Single(await GetAsync(database));
        Assert.Equal(AvailabilitySource.Imported, stored.Source);
    }

    [Fact]
    public async Task SettingAStatusByHandMarksItAsTheManagersOwn()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await ImportAsync(database, [Working(employee, 0)]);
        await SetManualAsync(database, employee, Monday, AvailabilityStatus.Holiday);

        var stored = Assert.Single(await GetAsync(database));
        Assert.Equal(AvailabilityStatus.Holiday, stored.Status);
        Assert.Equal(AvailabilitySource.Manual, stored.Source);
    }

    [Fact]
    public async Task ReimportingTheWeekLeavesAManualRecordAlone()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await ImportAsync(database, [Working(employee, 0)]);
        await SetManualAsync(database, employee, Monday, AvailabilityStatus.Off);

        // The same sheet again, still saying this person is in. It is not wrong; it simply
        // predates the phone call.
        await ImportAsync(database, [Working(employee, 0)]);

        var stored = Assert.Single(await GetAsync(database));
        Assert.Equal(AvailabilityStatus.Off, stored.Status);
        Assert.Equal(AvailabilitySource.Manual, stored.Source);
    }

    [Fact]
    public async Task ReimportingSaysWhichRecordsItKept()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);
        var other = await AddEmployeeAsync(database);

        await SetManualAsync(database, employee, Monday, AvailabilityStatus.Off);

        var kept = await database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.ReplaceImportedAsync(
                Monday,
                Monday.AddDays(7),
                [Working(employee, 0), Working(other, 0)]));

        // Silence here would be the failure mode worth fearing: the import quietly not
        // doing what the operator watched it do.
        var record = Assert.Single(kept);
        Assert.Equal(employee, record.EmployeeId);
        Assert.Equal(Monday, record.Date);
    }

    [Fact]
    public async Task ReimportingStillReplacesEverythingTheSheetOwns()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await ImportAsync(database, [Working(employee, 0), Working(employee, 1)]);

        // A corrected sheet that no longer mentions Tuesday. Provenance must not turn the
        // wholesale replacement into a merge for the records the sheet does own.
        await ImportAsync(database, [Working(employee, 0)]);

        var stored = Assert.Single(await GetAsync(database));
        Assert.Equal(Monday, stored.Date);
    }

    [Fact]
    public async Task AManualRecordOutsideTheImportedRangeIsUntouched()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await SetManualAsync(database, employee, Monday.AddDays(9), AvailabilityStatus.Holiday);
        await ImportAsync(database, [Working(employee, 0)]);

        var stored = await database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.GetAsync(Monday, Monday.AddDays(14)));

        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task SettingAStatusByHandTwiceKeepsTheSecondAnswer()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await SetManualAsync(database, employee, Monday, AvailabilityStatus.Off);

        // Somebody who rang in sick and then turned up anyway. The record is keyed on the
        // person and the day, so this has to replace rather than collide.
        await SetManualAsync(database, employee, Monday, AvailabilityStatus.Working);

        var stored = Assert.Single(await GetAsync(database));
        Assert.Equal(AvailabilityStatus.Working, stored.Status);
    }

    private static Availability Working(Guid employeeId, int day) => new()
    {
        EmployeeId = employeeId,
        Date = Monday.AddDays(day),
        Status = AvailabilityStatus.Working,
    };

    private static async Task<Guid> AddEmployeeAsync(TemporaryDatabase database)
    {
        var id = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = id, FullName = "Ada Fictional" }));

        return id;
    }

    private static Task ImportAsync(TemporaryDatabase database, IReadOnlyList<Availability> availabilities) =>
        database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.ReplaceImportedAsync(Monday, Monday.AddDays(7), availabilities));

    private static Task SetManualAsync(
        TemporaryDatabase database,
        Guid employeeId,
        DateOnly date,
        AvailabilityStatus status) =>
        database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.SetManualAsync(employeeId, date, status));

    private static Task<IReadOnlyList<Availability>> GetAsync(TemporaryDatabase database) =>
        database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.GetAsync(Monday, Monday.AddDays(7)));
}
