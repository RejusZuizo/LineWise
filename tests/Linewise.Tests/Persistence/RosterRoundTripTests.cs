using Linewise.Application.Persistence;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Persistence;

public sealed class RosterRoundTripTests
{
    [Fact]
    public async Task ARosterRoundTripsUnchanged()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var scenario = BusyWeek();
        var generated = TestEngine.Default().Generate(scenario.Build());

        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.SaveDraftAsync(RosterScenarioBuilder.DefaultWeekStart, generated));

        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(repository =>
            repository.GetLatestAsync(RosterScenarioBuilder.DefaultWeekStart));

        Assert.NotNull(stored);
        Assert.Equal(generated.WeekStart, stored.Roster.WeekStart);
        Assert.Equal(generated.Days.Count, stored.Roster.Days.Count);

        // Full record equality, explanations and warning text included. If the placement
        // reason did not survive, the manager could no longer be told why somebody is on a
        // line once the roster has been reloaded.
        Assert.Equal(generated.AllAssignments.ToList(), stored.Roster.AllAssignments.ToList());
        Assert.Equal(generated.AllWarnings.ToList(), stored.Roster.AllWarnings.ToList());
    }

    [Fact]
    public async Task SavingTheDraftAgainReplacesItRatherThanAccumulating()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var scenario = BusyWeek();
        var generated = TestEngine.Default().Generate(scenario.Build());

        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.SaveDraftAsync(RosterScenarioBuilder.DefaultWeekStart, generated));
        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.SaveDraftAsync(RosterScenarioBuilder.DefaultWeekStart, generated));

        var versions = await database.InScopeAsync<IRosterRepository, IReadOnlyList<Domain.Entities.RosterVersion>>(
            repository => repository.GetVersionsAsync(RosterScenarioBuilder.DefaultWeekStart));

        // Autosave runs constantly while somebody edits. It must not leave a version behind
        // every time it fires.
        var version = Assert.Single(versions);
        Assert.Equal(1, version.VersionNumber);

        var stored = await database.InScopeAsync<IRosterRepository, StoredRoster?>(repository =>
            repository.GetLatestAsync(RosterScenarioBuilder.DefaultWeekStart));

        Assert.NotNull(stored);
        Assert.Equal(generated.AllAssignments.Count(), stored.Roster.AllAssignments.Count());
    }

    [Fact]
    public async Task PublishingStampsTheVersionAndRecordsWhoDidIt()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var generated = TestEngine.Default().Generate(BusyWeek().Build());

        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.SaveDraftAsync(RosterScenarioBuilder.DefaultWeekStart, generated));

        var published = await database.InScopeAsync<IRosterRepository, Domain.Entities.RosterVersion>(
            repository => repository.PublishAsync(RosterScenarioBuilder.DefaultWeekStart, "Week signed off."));

        Assert.Equal(RosterStatus.Published, published.Status);
        Assert.Equal("test.operator", published.PublishedBy);
        Assert.NotNull(published.PublishedAtUtc);
        Assert.Equal(DateTimeKind.Utc, published.PublishedAtUtc!.Value.Kind);
    }

    [Fact]
    public async Task OnlyPublishedRostersFeedTheFairnessHistory()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var weekStart = RosterScenarioBuilder.DefaultWeekStart;
        var generated = TestEngine.Default().Generate(BusyWeek().Build());

        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.SaveDraftAsync(weekStart, generated));

        var whileDraft = await database.InScopeAsync<IRosterRepository, IReadOnlyList<Application.Rostering.HistoricAssignment>>(
            repository => repository.GetHistoryAsync(weekStart, weekStart.AddDays(7)));

        // A draft is a work in progress. It should not be teaching the fairness ledger who
        // has already had their turn on the good line.
        Assert.Empty(whileDraft);

        await database.InScopeAsync<IRosterRepository>(repository =>
            repository.PublishAsync(weekStart, "Signed off."));

        var afterPublish = await database.InScopeAsync<IRosterRepository, IReadOnlyList<Application.Rostering.HistoricAssignment>>(
            repository => repository.GetHistoryAsync(weekStart, weekStart.AddDays(7)));

        Assert.NotEmpty(afterPublish);
    }

    private static RosterScenarioBuilder BusyWeek() =>
        new RosterScenarioBuilder()
            .Line("Pastry", headcount: 3)
            .Line("Packing", headcount: 2)
            .Employee("Ada Fictional")
            .Employee("Bram Invented")
            .Employee("Cleo Notreal")
            .Employee("Dara Madeup")
            .Employee("Eli Pretend")
            .CanLead("Ada Fictional", "Pastry")
            .CanLead("Bram Invented", "Packing")
            .Prefers("Cleo Notreal", "Pastry", rank: 1)
            .Prefers("Dara Madeup", "Packing", rank: 1)
            .MustWork("Eli Pretend", "Pastry")
            .Off("Cleo Notreal", day: 2);
}
