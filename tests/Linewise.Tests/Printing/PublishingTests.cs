using Linewise.Application.Persistence;
using Linewise.Application.Printing;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Builders;
using Linewise.Tests.Persistence;
using Xunit;

namespace Linewise.Tests.Printing;

/// <summary>
/// Publishing a week, and knowing what it replaced.
/// </summary>
/// <remarks>
/// The amendment slip is measured against the sheet that is on the wall, which is the last
/// version published — not the last version saved, because a draft is saved on every
/// keystroke.
/// </remarks>
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class PublishingTests
{
    private static readonly DateOnly Monday = RosterScenarioBuilder.DefaultWeekStart;

    [Fact]
    public async Task Publishing_a_week_for_the_first_time_has_nothing_to_amend()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await GivenAGeneratedWeekAsync(database);

        var result = await PublishAsync(database);

        Assert.Equal(RosterStatus.Published, result.Version.Status);

        // Nothing was on the wall before, so a slip would have nothing to describe and a
        // full sheet is the only honest answer.
        Assert.Null(result.PreviouslyPublished);
        Assert.False(result.CanAmend);
    }

    /// <summary>
    /// The ordering that decides whether a slip tells the truth. Publishing is what makes
    /// the new version the published one, so reading afterwards returns the sheet that has
    /// just gone up rather than the one it replaced — and a slip comparing a roster with
    /// itself would report no changes, confidently.
    /// </summary>
    [Fact]
    public async Task Publishing_again_reports_the_sheet_it_replaced()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var world = await GivenAGeneratedWeekAsync(database);

        var first = await PublishAsync(database);

        // A change, then a second publish.
        await database.InScopeAsync<IAbsenceService>(service =>
            service.MarkAbsentAsync(new MarkAbsentRequest
            {
                EmployeeId = world.Employee,
                Date = Monday,
                Reason = AbsenceReason.NotInToday,
            }));

        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        var second = await PublishAsync(database);

        Assert.True(second.CanAmend);
        Assert.NotNull(second.PreviouslyPublished);
        Assert.True(second.Version.VersionNumber > first.Version.VersionNumber);

        // What it replaced, not what it became. The absent person is still on the earlier
        // sheet, which is the whole point of keeping it.
        Assert.Contains(
            second.PreviouslyPublished!.AllAssignments,
            assignment => assignment.EmployeeId == world.Employee && assignment.Date == Monday);
    }

    /// <summary>
    /// A draft saved after publishing must not be mistaken for the sheet on the wall.
    /// </summary>
    [Fact]
    public async Task A_later_draft_does_not_become_the_published_version()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await GivenAGeneratedWeekAsync(database);

        await PublishAsync(database);

        // Generating again writes a new draft over the top.
        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        var published = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetPublishedAsync(Monday));

        var latest = await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetLatestAsync(Monday));

        Assert.Equal(RosterStatus.Published, published!.Version.Status);
        Assert.Equal(RosterStatus.Draft, latest!.Version.Status);
        Assert.NotEqual(published.Version.Id, latest.Version.Id);
    }

    [Fact]
    public async Task A_week_that_was_never_published_has_no_published_version()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        await GivenAGeneratedWeekAsync(database);

        Assert.Null(await database.InScopeAsync<IRosterRepository, StoredRoster?>(
            repository => repository.GetPublishedAsync(Monday)));
    }

    private static Task<PublishResult> PublishAsync(TemporaryDatabase database) =>
        database.InScopeAsync<IPublishingService, PublishResult>(
            service => service.PublishAsync(Monday));

    private static async Task<(Guid Employee, Guid Line)> GivenAGeneratedWeekAsync(TemporaryDatabase database)
    {
        var line = Guid.NewGuid();
        var employee = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveLineAsync(new ProductionLine
            {
                Id = line,
                Name = "Ovens",
                RequiredHeadcount = 1,
                DisplayOrder = 1,
            }));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SaveEmployeeAsync(new Employee { Id = employee, FullName = "Ada Fictional" }));

        await database.InScopeAsync<IAvailabilityRepository>(repository =>
            repository.ReplaceImportedAsync(
                Monday,
                Monday.AddDays(7),
                [
                    new Availability
                    {
                        EmployeeId = employee,
                        Date = Monday,
                        Status = AvailabilityStatus.Working,
                    },
                ]));

        await database.InScopeAsync<IRosterGenerationService, StoredRoster>(
            generation => generation.GenerateAsync(Monday));

        return (employee, line);
    }
}
