using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IRosterRepository"/>
public sealed class SqliteRosterRepository : IRosterRepository
{
    private const string RosterVersionId = "RosterVersionId";
    private const string ShiftId = "ShiftId";
    private const string RowId = "Id";

    private readonly LinewiseDbContext _context;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLog _auditLog;

    public SqliteRosterRepository(
        LinewiseDbContext context,
        IClock clock,
        ICurrentUser currentUser,
        IAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(auditLog);

        _context = context;
        _clock = clock;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<RosterVersion> SaveDraftAsync(
        DateOnly weekStart,
        RosterWeek roster,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roster);

        var draft = await _context.RosterVersions
            .FirstOrDefaultAsync(
                version => version.WeekStart == weekStart && version.Status == RosterStatus.Draft,
                cancellationToken)
            .ConfigureAwait(false);

        if (draft is null)
        {
            var highestVersion = await _context.RosterVersions
                .Where(version => version.WeekStart == weekStart)
                .MaxAsync(version => (int?)version.VersionNumber, cancellationToken)
                .ConfigureAwait(false) ?? 0;

            draft = new RosterVersion
            {
                Id = Guid.NewGuid(),
                WeekStart = weekStart,
                VersionNumber = highestVersion + 1,
                Status = RosterStatus.Draft,
                CreatedAtUtc = _clock.UtcNow,
            };

            _context.RosterVersions.Add(draft);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // The draft is rewritten wholesale on every autosave. Deleting through the
            // database rather than the change tracker keeps this cheap enough to run
            // continuously while somebody drags chips around.
            await ClearChildrenAsync(draft.Id, cancellationToken).ConfigureAwait(false);
        }

        WriteChildren(draft.Id, roster);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return draft;
    }

    public async Task<RosterVersion> PublishAsync(
        DateOnly weekStart,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var draft = await _context.RosterVersions
            .FirstOrDefaultAsync(
                version => version.WeekStart == weekStart && version.Status == RosterStatus.Draft,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"There is no draft roster for the week beginning {weekStart:yyyy-MM-dd}.");

        var published = draft with
        {
            Status = RosterStatus.Published,
            PublishedAtUtc = _clock.UtcNow,
            PublishedBy = _currentUser.Name,
        };

        _context.Entry(draft).CurrentValues.SetValues(published);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLog.AppendAsync(
            AuditAction.RosterPublished,
            $"Roster version {published.VersionNumber} for week {weekStart:yyyy-MM-dd} published.",
            reason,
            cancellationToken).ConfigureAwait(false);

        return published;
    }

    public Task<StoredRoster?> GetLatestAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default) =>
        ReadAsync(weekStart, publishedOnly: false, cancellationToken);

    public Task<StoredRoster?> GetPublishedAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default) =>
        ReadAsync(weekStart, publishedOnly: true, cancellationToken);

    /// <param name="publishedOnly">
    /// The sheet on the wall rather than the newest one. A draft is saved on every
    /// keystroke, so the latest version is almost never the one somebody printed.
    /// </param>
    private async Task<StoredRoster?> ReadAsync(
        DateOnly weekStart,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        var version = await _context.RosterVersions
            .AsNoTracking()
            .Where(candidate => candidate.WeekStart == weekStart)
            .Where(candidate => !publishedOnly || candidate.Status == RosterStatus.Published)
            .OrderByDescending(candidate => candidate.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (version is null)
        {
            return null;
        }

        var days = await _context.Set<RosterDayRow>()
            .AsNoTracking()
            .Where(day => day.RosterVersionId == version.Id)
            .OrderBy(day => day.Ordinal)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ordered by the insert key, so the roster comes back in exactly the order the
        // engine produced rather than in an order re-derived from the data.
        var assignments = await _context.Assignments
            .AsNoTracking()
            .Where(assignment => EF.Property<Guid>(assignment, RosterVersionId) == version.Id)
            .OrderBy(assignment => EF.Property<long>(assignment, RowId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var warnings = await _context.RosterWarnings
            .AsNoTracking()
            .Where(warning => EF.Property<Guid>(warning, RosterVersionId) == version.Id)
            .OrderBy(warning => EF.Property<long>(warning, RowId))
            .Select(warning => new WarningRow(warning, EF.Property<Guid>(warning, ShiftId)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roster = new RosterWeek
        {
            WeekStart = version.WeekStart,
            Days = days
                .Select(day => new RosterDay
                {
                    Date = day.Date,
                    ShiftId = day.ShiftId,
                    // Matched on the shift alone. A shift identifier already pins down one
                    // date and one shift, and pairing it with the date would quietly drop a
                    // warning that happens to carry no date of its own.
                    Assignments = assignments
                        .Where(assignment => assignment.ShiftId == day.ShiftId)
                        .ToList(),
                    Warnings = warnings
                        .Where(row => row.ShiftId == day.ShiftId)
                        .Select(row => row.Warning)
                        .ToList(),
                })
                .ToList(),
        };

        return new StoredRoster(version, roster);
    }

    public async Task<IReadOnlyList<RosterVersion>> GetVersionsAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default) =>
        await _context.RosterVersions
            .AsNoTracking()
            .Where(version => version.WeekStart == weekStart)
            .OrderByDescending(version => version.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<HistoricAssignment>> GetHistoryAsync(
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default)
    {
        // Published rosters only. A draft is a work in progress and should not be teaching
        // the fairness ledger who has already had their turn.
        var publishedVersionIds = _context.RosterVersions
            .Where(version => version.Status == RosterStatus.Published)
            .Select(version => version.Id);

        return await _context.Assignments
            .AsNoTracking()
            .Where(assignment => assignment.Date >= fromInclusive && assignment.Date < toExclusive)
            .Where(assignment => publishedVersionIds.Contains(EF.Property<Guid>(assignment, RosterVersionId)))
            .OrderBy(assignment => assignment.Date)
            .ThenBy(assignment => assignment.EmployeeId)
            .Select(assignment => new HistoricAssignment
            {
                Date = assignment.Date,
                EmployeeId = assignment.EmployeeId,
                LineId = assignment.LineId,
                Role = assignment.Role,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ClearChildrenAsync(Guid versionId, CancellationToken cancellationToken)
    {
        await _context.Assignments
            .Where(assignment => EF.Property<Guid>(assignment, RosterVersionId) == versionId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await _context.RosterWarnings
            .Where(warning => EF.Property<Guid>(warning, RosterVersionId) == versionId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await _context.Set<RosterDayRow>()
            .Where(day => day.RosterVersionId == versionId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // Every tracked child, not the ones for this version alone. The parent key lives in
        // shadow state, which a predicate over the entity cannot read, and this repository
        // reads everything AsNoTracking — so nothing tracked here is anything but the
        // leftovers of a previous write.
        //
        // The draft is rewritten on every autosave. Without this the tracker accumulates
        // every assignment the application has ever written, which is a leak on its own and
        // eventually a key collision.
        _context.Forget<Assignment>(_ => true);
        _context.Forget<RosterWarning>(_ => true);
        _context.Forget<RosterDayRow>(_ => true);
    }

    private void WriteChildren(Guid versionId, RosterWeek roster)
    {
        for (var ordinal = 0; ordinal < roster.Days.Count; ordinal++)
        {
            var day = roster.Days[ordinal];

            _context.Set<RosterDayRow>().Add(new RosterDayRow
            {
                RosterVersionId = versionId,
                Date = day.Date,
                ShiftId = day.ShiftId,
                Ordinal = ordinal,
            });

            foreach (var assignment in day.Assignments)
            {
                var entry = _context.Assignments.Add(assignment);
                entry.Property<Guid>(RosterVersionId).CurrentValue = versionId;
            }

            foreach (var warning in day.Warnings)
            {
                var entry = _context.RosterWarnings.Add(warning);
                entry.Property<Guid>(RosterVersionId).CurrentValue = versionId;
                entry.Property<Guid>(ShiftId).CurrentValue = day.ShiftId;
            }
        }
    }

    private sealed record WarningRow(RosterWarning Warning, Guid ShiftId);
}
