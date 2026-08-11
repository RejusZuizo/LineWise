using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Xunit;

namespace Linewise.Tests.Persistence;

/// <summary>
/// The path a person actually walks: set up lines, have people and their availability, press
/// generate, get a roster.
/// </summary>
/// <remarks>
/// Everything here goes through the real repositories and a real encrypted database. The
/// engine has been tested to death against fakes since phase 1; what had never been tested
/// is whether the pieces meet. Two defects came out of running the application by hand this
/// week — an uninitialised database and a week with no shifts in it — and neither would have
/// been caught by a test that stubbed the layer below.
/// </remarks>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class DemoPathTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);

    [Fact]
    public async Task A_factory_with_lines_and_people_produces_a_populated_roster()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var lines = await GivenLines(database, ("Ovens", 3), ("Packing", 4));
        var employees = await GivenEmployees(database, 12);
        await GivenEveryoneWorking(database, employees);

        var stored = await Generate(database);

        Assert.NotEmpty(stored.Roster.AllAssignments);

        // Seven days, both lines, filled to headcount from twelve available people.
        Assert.Equal(7, stored.Roster.Days.Select(day => day.Date).Distinct().Count());
        Assert.All(
            stored.Roster.AllAssignments,
            assignment => Assert.Contains(assignment.LineId, lines));
    }

    /// <summary>
    /// The defect this test exists for. A first run has no shifts, and a roster is generated
    /// per shift, so without this the grid stays empty with nothing on screen explaining why.
    /// </summary>
    [Fact]
    public async Task A_first_run_does_not_need_a_shift_to_be_configured_first()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await GivenLines(database, ("Ovens", 2));
        var employees = await GivenEmployees(database, 4);
        await GivenEveryoneWorking(database, employees);

        var shiftsBefore = await database.InScopeAsync<IShiftRepository, IReadOnlyList<Shift>>(
            shifts => shifts.GetAsync(Monday, Monday.AddDays(7)));

        Assert.Empty(shiftsBefore);

        var stored = await Generate(database);

        Assert.NotEmpty(stored.Roster.AllAssignments);
    }

    /// <summary>
    /// Nobody available is not a fault. It is a bank holiday, and the answer is an empty
    /// roster with warnings, not an exception.
    /// </summary>
    [Fact]
    public async Task Nobody_available_produces_warnings_rather_than_a_failure()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await GivenLines(database, ("Ovens", 3));
        await GivenEmployees(database, 5);

        var stored = await Generate(database);

        Assert.Empty(stored.Roster.AllAssignments);
        Assert.NotEmpty(stored.Roster.AllWarnings);
    }

    [Fact]
    public async Task The_generated_roster_survives_a_round_trip_to_the_database()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await GivenLines(database, ("Ovens", 3));
        var employees = await GivenEmployees(database, 6);
        await GivenEveryoneWorking(database, employees);

        var generated = await Generate(database);

        var reloaded = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            rosters => rosters.GetLatestAsync(Monday));

        Assert.NotNull(reloaded);
        Assert.Equal(
            generated.Roster.AllAssignments.Count(),
            reloaded!.Roster.AllAssignments.Count());
    }

    private static Task<StoredRoster> Generate(TemporaryDatabase database) =>
        database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

    private static async Task<List<Guid>> GivenLines(
        TemporaryDatabase database,
        params (string Name, int Headcount)[] lines)
    {
        var ids = new List<Guid>();

        for (var i = 0; i < lines.Length; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);

            await database.InScopeAsync<IConfigurationRepository>(configuration =>
                configuration.SaveLineAsync(new ProductionLine
                {
                    Id = id,
                    Name = lines[i].Name,
                    RequiredHeadcount = lines[i].Headcount,
                    DisplayOrder = i + 1,
                }));
        }

        return ids;
    }

    private static async Task<List<Guid>> GivenEmployees(TemporaryDatabase database, int count)
    {
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);

            await database.InScopeAsync<IConfigurationRepository>(configuration =>
                configuration.SaveEmployeeAsync(new Employee
                {
                    Id = id,

                    // Obviously invented, and numbered so a failure names the row.
                    FullName = $"Person {i + 1:00} Fictional",
                }));
        }

        return ids;
    }

    private static Task GivenEveryoneWorking(TemporaryDatabase database, IEnumerable<Guid> employees)
    {
        var availability = employees
            .SelectMany(employee => Enumerable.Range(0, 7).Select(offset => new Availability
            {
                EmployeeId = employee,
                Date = Monday.AddDays(offset),
                Status = AvailabilityStatus.Working,
            }))
            .ToList();

        return database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.ReplaceAsync(Monday, Monday.AddDays(7), availability));
    }
}
