using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Linewise.Tests.Persistence;

/// <summary>
/// Saving the same thing twice through one context.
/// </summary>
/// <remarks>
/// Reported as "I cannot save rules sometimes". Sometimes meant the second time.
/// <para>
/// Every replace in this layer is <c>ExecuteDelete</c> followed by an insert of the same
/// keys. <c>ExecuteDelete</c> goes straight to the database and does not touch the change
/// tracker, so the entities the first save added were still tracked when the second save
/// tried to add their replacements, and EF Core refused.
/// </para>
/// <para>
/// Every test in this file re-uses one scope deliberately. The suite's other tests take a
/// fresh scope per call, which is good hygiene and is exactly why none of them saw this.
/// </para>
/// </remarks>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class SavingTwiceTests
{
    private static readonly DateOnly Monday = RosterScenarioBuilder.DefaultWeekStart;

    [Fact]
    public async Task Preferences_can_be_saved_twice()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await using var scope = database.Services.CreateAsyncScope();
        var configuration = Resolve<IConfigurationRepository>(scope);

        LinePreference[] rules =
        [
            new() { EmployeeId = world.Employee, LineId = world.Line, Rank = 1, Type = PreferenceType.Preferred },
        ];

        await configuration.ReplacePreferencesAsync(world.Employee, rules);

        // The one that used to throw.
        await configuration.ReplacePreferencesAsync(world.Employee, rules);

        var stored = await configuration.GetAsync();
        Assert.Single(stored.Preferences);
    }

    [Fact]
    public async Task Leader_eligibility_can_be_saved_twice()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await using var scope = database.Services.CreateAsyncScope();
        var configuration = Resolve<IConfigurationRepository>(scope);

        LeaderEligibility[] rules = [new() { EmployeeId = world.Employee, LineId = world.Line }];

        await configuration.ReplaceLeaderEligibilityAsync(world.Employee, rules);
        await configuration.ReplaceLeaderEligibilityAsync(world.Employee, rules);

        Assert.Single((await configuration.GetAsync()).LeaderEligibilities);
    }

    [Fact]
    public async Task Assistant_eligibility_can_be_saved_twice()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await using var scope = database.Services.CreateAsyncScope();
        var configuration = Resolve<IConfigurationRepository>(scope);

        OperatingAssistantEligibility[] rules = [new() { EmployeeId = world.Employee, LineId = world.Line }];

        await configuration.ReplaceOperatingAssistantEligibilityAsync(world.Employee, rules);
        await configuration.ReplaceOperatingAssistantEligibilityAsync(world.Employee, rules);

        Assert.Single((await configuration.GetAsync()).OperatingAssistantEligibilities);
    }

    /// <summary>
    /// The alias learning path from the adaptive import writes the same employee twice as a
    /// matter of course, once per name it recognises.
    /// </summary>
    [Fact]
    public async Task An_employee_with_aliases_can_be_saved_twice()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await using var scope = database.Services.CreateAsyncScope();
        var configuration = Resolve<IConfigurationRepository>(scope);

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Ada Fictional",
            Aliases = ["A. Fictional"],
        };

        await configuration.SaveEmployeeAsync(employee);
        await configuration.SaveEmployeeAsync(employee with { Aliases = ["A. Fictional", "Ada F"] });

        var stored = Assert.Single((await configuration.GetAsync()).Employees);
        Assert.Equal(2, stored.Aliases.Count);
    }

    [Fact]
    public async Task Availability_can_be_imported_twice()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await using var scope = database.Services.CreateAsyncScope();
        var availability = Resolve<IAvailabilityRepository>(scope);

        Availability[] week =
        [
            new() { EmployeeId = world.Employee, Date = Monday, Status = AvailabilityStatus.Working },
        ];

        await availability.ReplaceImportedAsync(Monday, Monday.AddDays(7), week);
        await availability.ReplaceImportedAsync(Monday, Monday.AddDays(7), week);

        Assert.Single(await availability.GetAsync(Monday, Monday.AddDays(7)));
    }

    /// <summary>
    /// The draft is rewritten on every autosave, so this is the path that runs most often.
    /// </summary>
    [Fact]
    public async Task A_draft_can_be_saved_twice()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAFactoryAsync(database);

        await using var scope = database.Services.CreateAsyncScope();
        var rosters = Resolve<IRosterRepository>(scope);

        // One shift identifier, shared by the day and the people on it. The reader groups
        // assignments back into days by it, so two different ones would silently lose them.
        var shiftId = Guid.NewGuid();

        var roster = new Domain.Rostering.RosterWeek
        {
            WeekStart = Monday,
            Days =
            [
                new Domain.Rostering.RosterDay
                {
                    Date = Monday,
                    ShiftId = shiftId,
                    Assignments =
                    [
                        new Assignment
                        {
                            Date = Monday,
                            ShiftId = shiftId,
                            LineId = world.Line,
                            EmployeeId = world.Employee,
                            Role = AssignmentRole.Worker,
                        },
                    ],
                },
            ],
        };

        await rosters.SaveDraftAsync(Monday, roster);
        await rosters.SaveDraftAsync(Monday, roster);

        var stored = await rosters.GetLatestAsync(Monday);
        Assert.Single(stored!.Roster.AllAssignments);
    }

    private static TService Resolve<TService>(IServiceScope scope)
        where TService : notnull =>
        scope.ServiceProvider.GetRequiredService<TService>();

    private static async Task<(Guid Employee, Guid Line)> GivenAFactoryAsync(TemporaryDatabase database)
    {
        var employee = Guid.NewGuid();
        var line = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = employee, FullName = "Ada Fictional" }));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveLineAsync(new ProductionLine
            {
                Id = line,
                Name = "Ovens",
                RequiredHeadcount = 2,
            }));

        return (employee, line);
    }
}
