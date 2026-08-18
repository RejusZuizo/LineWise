using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Linewise.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Linewise.Tests.Rostering;

/// <summary>
/// Marking somebody absent, against a real database so the audit chain is exercised rather
/// than mocked away.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class AbsenceServiceTests
{
    private static readonly DateOnly Monday = RosterScenarioBuilder.DefaultWeekStart;

    [Fact]
    public async Task Marking_absent_writes_availability_as_the_managers_own()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await MarkAbsentAsync(database, employee, AbsenceReason.NotInToday);

        var record = Assert.Single(await AvailabilityAsync(database));

        Assert.Equal(AvailabilityStatus.Off, record.Status);
        Assert.Equal(AvailabilitySource.Manual, record.Source);
    }

    /// <summary>
    /// The one absence the sheet already has a word for. Both mean unavailable and the
    /// engine treats them identically, but a planned day off and a phone call at seven are
    /// different things to the manager staring at a line that is short.
    /// </summary>
    [Fact]
    public async Task A_holiday_is_recorded_as_a_holiday()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await MarkAbsentAsync(database, employee, AbsenceReason.Holiday);

        Assert.Equal(AvailabilityStatus.Holiday, Assert.Single(await AvailabilityAsync(database)).Status);
    }

    [Fact]
    public async Task Marking_absent_writes_an_audit_entry_naming_nobody()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await MarkAbsentAsync(database, employee, AbsenceReason.SentHome);

        var entries = await database.InScopeAsync<IAuditLog, IReadOnlyList<AuditEntryRow>>(
            async log => [.. (await log.ReadAllAsync()).Select(entry =>
                new AuditEntryRow(entry.Action, entry.Summary, entry.Reason))]);

        var entry = Assert.Single(entries, row => row.Action == AuditAction.RosterEdited);

        Assert.Equal("Sent home.", entry.Reason);

        // Identifiers, never names. The audit log is one of the places personal data would
        // otherwise accumulate quietly for years.
        Assert.Contains(employee.ToString(), entry.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", entry.Summary, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The audit chain is written once and read years later, possibly on another machine.
    /// A reason that changed language with a regional setting would be a poor record.
    /// </summary>
    [Theory]
    [InlineData(AbsenceReason.NotInToday, "Not in today.")]
    [InlineData(AbsenceReason.Holiday, "Holiday.")]
    [InlineData(AbsenceReason.SentHome, "Sent home.")]
    public async Task Each_reason_writes_its_own_words(AbsenceReason reason, string expected)
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await MarkAbsentAsync(database, employee, reason);

        var reasons = await database.InScopeAsync<IAuditLog, IReadOnlyList<string>>(
            async log => [.. (await log.ReadAllAsync()).Select(entry => entry.Reason)]);

        Assert.Contains(expected, reasons);
    }

    /// <summary>
    /// The places the absence leaves to fill, which is what the replacement picker works
    /// from. The assignments themselves stay put: the grid greys the name rather than
    /// losing it.
    /// </summary>
    [Fact]
    public async Task The_places_they_were_holding_are_reported_and_left_alone()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var scenario = await GivenARosteredWeekAsync(database);

        var result = await MarkAbsentAsync(database, scenario.Absentee, AbsenceReason.NotInToday);

        var vacated = Assert.Single(result.Vacated);
        Assert.Equal(scenario.Absentee, vacated.EmployeeId);
        Assert.Equal(Monday, vacated.Date);

        // Still in the stored roster afterwards. Removing it would take away the answer to
        // "who should have been on Ovens this morning".
        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetLatestAsync(Monday));

        Assert.Contains(
            stored!.Roster.AllAssignments,
            assignment => assignment.EmployeeId == scenario.Absentee && assignment.Date == Monday);
    }

    [Fact]
    public async Task Marking_absent_before_a_roster_exists_vacates_nothing()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        var result = await MarkAbsentAsync(database, employee, AbsenceReason.NotInToday);

        // An ordinary outcome rather than a failure. Somebody can ring in before the week
        // has been generated.
        Assert.Empty(result.Vacated);
    }

    [Fact]
    public async Task Marking_somebody_back_in_restores_them_and_stays_manual()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var employee = await AddEmployeeAsync(database);

        await MarkAbsentAsync(database, employee, AbsenceReason.NotInToday);

        await database.InScopeAsync<IAbsenceService>(service =>
            service.ClearAbsenceAsync(employee, Monday));

        var record = Assert.Single(await AvailabilityAsync(database));

        Assert.Equal(AvailabilityStatus.Working, record.Status);

        // Still the manager's own. The second decision has no more business being
        // overwritten by the next import than the first did.
        Assert.Equal(AvailabilitySource.Manual, record.Source);
    }

    /// <summary>
    /// The property the whole of ADR 0014 exists for, end to end through the service the
    /// screen actually calls.
    /// </summary>
    [Fact]
    public async Task An_absence_survives_regenerating_the_week()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var scenario = await GivenARosteredWeekAsync(database);

        await MarkAbsentAsync(database, scenario.Absentee, AbsenceReason.NotInToday);

        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        var record = (await AvailabilityAsync(database))
            .Single(a => a.EmployeeId == scenario.Absentee && a.Date == Monday);

        Assert.Equal(AvailabilityStatus.Off, record.Status);

        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetLatestAsync(Monday));

        // And the engine does not put them back on a line, because it reads the same
        // availability everything else does.
        Assert.DoesNotContain(
            stored!.Roster.AllAssignments,
            assignment => assignment.EmployeeId == scenario.Absentee && assignment.Date == Monday);
    }

    private static Task<AbsenceResult> MarkAbsentAsync(
        TemporaryDatabase database,
        Guid employeeId,
        AbsenceReason reason) =>
        database.InScopeAsync<IAbsenceService, AbsenceResult>(service =>
            service.MarkAbsentAsync(new MarkAbsentRequest
            {
                EmployeeId = employeeId,
                Date = Monday,
                Reason = reason,
            }));

    private static Task<IReadOnlyList<Availability>> AvailabilityAsync(TemporaryDatabase database) =>
        database.InScopeAsync<IAvailabilityRepository, IReadOnlyList<Availability>>(
            repository => repository.GetAsync(Monday, Monday.AddDays(7)));

    private static async Task<Guid> AddEmployeeAsync(TemporaryDatabase database, string name = "Ada Fictional")
    {
        var id = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = id, FullName = name }));

        return id;
    }

    /// <summary>
    /// A line, three people, everybody in, and a generated week. Enough that somebody is
    /// actually standing on a line to be taken off it.
    /// </summary>
    private static async Task<(Guid Absentee, Guid LineId)> GivenARosteredWeekAsync(TemporaryDatabase database)
    {
        var lineId = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveLineAsync(new ProductionLine
            {
                Id = lineId,
                Name = "Ovens",
                RequiredHeadcount = 3,
                DisplayOrder = 1,
            }));

        var people = new List<Guid>
        {
            await AddEmployeeAsync(database, "Ada Fictional"),
            await AddEmployeeAsync(database, "Bram Invented"),
            await AddEmployeeAsync(database, "Cleo Notreal"),
        };

        await database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.ReplaceImportedAsync(
                Monday,
                Monday.AddDays(7),
                [.. people.SelectMany(person => Enumerable.Range(0, 7).Select(offset => new Availability
                {
                    EmployeeId = person,
                    Date = Monday.AddDays(offset),
                    Status = AvailabilityStatus.Working,
                }))]));

        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        return (people[0], lineId);
    }

    private sealed record AuditEntryRow(AuditAction Action, string Summary, string Reason);
}
