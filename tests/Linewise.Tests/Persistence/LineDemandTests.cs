using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Tests.Builders;
using Xunit;

namespace Linewise.Tests.Persistence;

public sealed class LineDemandTests
{
    private static readonly DateOnly Monday = RosterScenarioBuilder.DefaultWeekStart;

    [Fact]
    public async Task DemandsRoundTrip()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var lineId = await AddLineAsync(database);

        await ReplaceAsync(database, [Demand(lineId, day: 0, headcount: 6)]);

        var stored = await GetAsync(database);

        var demand = Assert.Single(stored);
        Assert.Equal(lineId, demand.LineId);
        Assert.Equal(Monday, demand.Date);
        Assert.Equal(6, demand.RequiredHeadcount);
    }

    [Fact]
    public async Task SettingAWeeksDemandReplacesWhatWasThere()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var lineId = await AddLineAsync(database);

        await ReplaceAsync(database, [Demand(lineId, 0, 6), Demand(lineId, 1, 5)]);
        await ReplaceAsync(database, [Demand(lineId, 0, 4)]);

        // Setting a week's demand is one decision about that week, not an addition to
        // whatever was decided last time.
        var demand = Assert.Single(await GetAsync(database));
        Assert.Equal(4, demand.RequiredHeadcount);
    }

    [Fact]
    public async Task DemandsOutsideTheRangeAreLeftAlone()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var lineId = await AddLineAsync(database);

        await ReplaceAsync(database, [Demand(lineId, 0, 6)]);

        await database.InScopeAsync<ILineDemandRepository>(repository =>
            repository.ReplaceAsync(Monday.AddDays(7), Monday.AddDays(14), [Demand(lineId, 7, 3)]));

        var stored = await database.InScopeAsync<ILineDemandRepository, IReadOnlyList<LineDemand>>(
            repository => repository.GetAsync(Monday, Monday.AddDays(14)));

        Assert.Equal(2, stored.Count);
    }

    private static LineDemand Demand(Guid lineId, int day, int headcount) => new()
    {
        LineId = lineId,
        Date = Monday.AddDays(day),
        RequiredHeadcount = headcount,
    };

    private static async Task<Guid> AddLineAsync(TemporaryDatabase database)
    {
        var lineId = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveLineAsync(new ProductionLine
            {
                Id = lineId,
                Name = "Packing",
                RequiredHeadcount = 3,
            }));

        return lineId;
    }

    private static Task ReplaceAsync(TemporaryDatabase database, IReadOnlyList<LineDemand> demands) =>
        database.InScopeAsync<ILineDemandRepository>(repository =>
            repository.ReplaceAsync(Monday, Monday.AddDays(7), demands));

    private static Task<IReadOnlyList<LineDemand>> GetAsync(TemporaryDatabase database) =>
        database.InScopeAsync<ILineDemandRepository, IReadOnlyList<LineDemand>>(
            repository => repository.GetAsync(Monday, Monday.AddDays(7)));
}
