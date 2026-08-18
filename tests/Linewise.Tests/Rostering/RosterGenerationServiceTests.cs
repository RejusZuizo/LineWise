using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Xunit;

namespace Linewise.Tests.Rostering;

/// <summary>
/// Assembling a week's inputs, running the engine, and storing the draft.
/// </summary>
/// <remarks>
/// The engine is a stub here. What is under test is what the service reads, what it hands
/// over, and what it does with the answer — not the rules, which have their own suite.
/// </remarks>
public sealed class RosterGenerationServiceTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);
    private static readonly Guid Ovens = Guid.NewGuid();
    private static readonly Guid Ada = Guid.NewGuid();

    [Fact]
    public async Task It_generates_the_week_asked_for()
    {
        var engine = new RecordingEngine();
        var service = Service(engine);

        await service.GenerateAsync(Monday);

        Assert.Equal(Monday, engine.Request!.WeekStart);
    }

    /// <summary>
    /// The property that makes "generate again" safe to press. A manual placement survives
    /// because the service reads it back out of the stored draft and tells the engine about
    /// it, rather than leaving the engine to rediscover a decision somebody already made.
    /// </summary>
    [Fact]
    public async Task Manual_placements_are_carried_into_the_next_generation()
    {
        var locked = Assign(Ada, locked: true);
        var loose = Assign(Guid.NewGuid(), locked: false);

        var engine = new RecordingEngine();
        var service = Service(engine, existing: Roster(locked, loose));

        await service.GenerateAsync(Monday);

        var carried = Assert.Single(engine.Request!.LockedAssignments);
        Assert.Equal(Ada, carried.EmployeeId);
    }

    [Fact]
    public async Task A_first_run_carries_nothing()
    {
        var engine = new RecordingEngine();
        var service = Service(engine, existing: null);

        await service.GenerateAsync(Monday);

        Assert.Empty(engine.Request!.LockedAssignments);
    }

    /// <summary>
    /// Fairness is answered from more than the week just gone. Four weeks is long enough to
    /// mean something and short enough that a month of absence is not still being
    /// compensated for.
    /// </summary>
    [Fact]
    public async Task History_reaches_back_four_weeks()
    {
        var rosters = new FakeRosterRepository();
        await Service(new RecordingEngine(), rosters: rosters).GenerateAsync(Monday);

        Assert.Equal(Monday.AddDays(-28), rosters.HistoryFrom);
        Assert.Equal(Monday, rosters.HistoryTo);
    }

    [Fact]
    public async Task The_result_is_saved_as_the_draft()
    {
        var rosters = new FakeRosterRepository();

        var stored = await Service(new RecordingEngine(), rosters: rosters).GenerateAsync(Monday);

        Assert.True(rosters.Saved);
        Assert.Equal(RosterStatus.Draft, stored.Version.Status);
    }

    [Fact]
    public async Task Everything_is_read_for_the_week_and_no_wider()
    {
        var availability = new FakeAvailabilityRepository();

        await Service(new RecordingEngine(), availability: availability).GenerateAsync(Monday);

        Assert.Equal(Monday, availability.From);
        Assert.Equal(Monday.AddDays(7), availability.To);
    }

    private static RosterGenerationService Service(
        IAssignmentEngine engine,
        RosterWeek? existing = null,
        FakeRosterRepository? rosters = null,
        FakeAvailabilityRepository? availability = null)
    {
        rosters ??= new FakeRosterRepository();
        rosters.Existing = existing;

        return new RosterGenerationService(
            engine,
            new FakeConfigurationRepository(),
            availability ?? new FakeAvailabilityRepository(),
            new FakeShiftRepository(),
            new FakeLineDemandRepository(),
            rosters);
    }

    private static Assignment Assign(Guid employeeId, bool locked) =>
        new()
        {
            Date = Monday,
            ShiftId = Guid.Empty,
            LineId = Ovens,
            EmployeeId = employeeId,
            Role = AssignmentRole.Worker,
            IsLocked = locked,
            Source = locked ? AssignmentSource.Manual : AssignmentSource.Auto,
        };

    private static RosterWeek Roster(params Assignment[] assignments) =>
        new()
        {
            WeekStart = Monday,
            Days = [new RosterDay { Date = Monday, ShiftId = Guid.Empty, Assignments = assignments }],
        };

    private sealed class RecordingEngine : IAssignmentEngine
    {
        public AssignmentRequest? Request { get; private set; }

        public RosterWeek Generate(AssignmentRequest request)
        {
            Request = request;
            return new RosterWeek { WeekStart = request.WeekStart };
        }
    }

    private sealed class FakeRosterRepository : IRosterRepository
    {
        public RosterWeek? Existing { get; set; }

        public bool Saved { get; private set; }

        public DateOnly HistoryFrom { get; private set; }

        public DateOnly HistoryTo { get; private set; }

        public Task<RosterVersion> SaveDraftAsync(
            DateOnly weekStart,
            RosterWeek roster,
            CancellationToken cancellationToken = default)
        {
            Saved = true;

            return Task.FromResult(new RosterVersion
            {
                Id = Guid.NewGuid(),
                WeekStart = weekStart,
                VersionNumber = 1,
                Status = RosterStatus.Draft,
                CreatedAtUtc = DateTime.UnixEpoch,
            });
        }

        public Task<RosterVersion> PublishAsync(
            DateOnly weekStart,
            string reason,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredRoster?> GetLatestAsync(
            DateOnly weekStart,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Existing is null
                ? null
                : new StoredRoster(
                    new RosterVersion
                    {
                        Id = Guid.NewGuid(),
                        WeekStart = weekStart,
                        VersionNumber = 1,
                        Status = RosterStatus.Draft,
                        CreatedAtUtc = DateTime.UnixEpoch,
                    },
                    Existing));

        /// <summary>Never published. Generation does not care, and nothing here reads it.</summary>
        public Task<StoredRoster?> GetPublishedAsync(
            DateOnly weekStart,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredRoster?>(null);

        public Task<IReadOnlyList<RosterVersion>> GetVersionsAsync(
            DateOnly weekStart,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RosterVersion>>([]);

        public Task<IReadOnlyList<HistoricAssignment>> GetHistoryAsync(
            DateOnly fromInclusive,
            DateOnly toExclusive,
            CancellationToken cancellationToken = default)
        {
            HistoryFrom = fromInclusive;
            HistoryTo = toExclusive;
            return Task.FromResult<IReadOnlyList<HistoricAssignment>>([]);
        }
    }

    private sealed class FakeAvailabilityRepository : IAvailabilityRepository
    {
        public DateOnly From { get; private set; }

        public DateOnly To { get; private set; }

        public Task<IReadOnlyList<Availability>> GetAsync(
            DateOnly fromInclusive,
            DateOnly toExclusive,
            CancellationToken cancellationToken = default)
        {
            From = fromInclusive;
            To = toExclusive;
            return Task.FromResult<IReadOnlyList<Availability>>([]);
        }

        public Task<IReadOnlyList<Availability>> ReplaceImportedAsync(
            DateOnly fromInclusive,
            DateOnly toExclusive,
            IReadOnlyList<Availability> availabilities,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Availability>>([]);

        public Task SetManualAsync(
            Guid employeeId,
            DateOnly date,
            AvailabilityStatus status,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeConfigurationRepository : IConfigurationRepository
    {
        public Task<RosterConfiguration> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RosterConfiguration());

        public Task SaveEmployeeAsync(Employee employee, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLineAsync(ProductionLine line, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveSkillAsync(Skill skill, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplacePreferencesAsync(
            Guid employeeId,
            IReadOnlyList<LinePreference> preferences,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplaceLeaderEligibilityAsync(
            Guid employeeId,
            IReadOnlyList<LeaderEligibility> eligibilities,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplaceOperatingAssistantEligibilityAsync(
            Guid employeeId,
            IReadOnlyList<OperatingAssistantEligibility> eligibilities,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PrintSettings> GetPrintSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PrintSettings { Id = Guid.Empty });

        public Task SavePrintSettingsAsync(
            PrintSettings settings,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeShiftRepository : IShiftRepository
    {
        public Task<IReadOnlyList<Shift>> GetAsync(
            DateOnly fromInclusive,
            DateOnly toExclusive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Shift>>([]);

        public Task<IReadOnlyList<Shift>> EnsureAsync(
            IReadOnlyList<Shift> shifts,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(shifts);
    }

    private sealed class FakeLineDemandRepository : ILineDemandRepository
    {
        public Task<IReadOnlyList<LineDemand>> GetAsync(
            DateOnly fromInclusive,
            DateOnly toExclusive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LineDemand>>([]);

        public Task ReplaceAsync(
            DateOnly fromInclusive,
            DateOnly toExclusive,
            IReadOnlyList<LineDemand> demands,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAsync(LineDemand demand, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
