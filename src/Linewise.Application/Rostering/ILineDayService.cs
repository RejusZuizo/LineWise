using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <summary>
/// What one line is doing on one day: running, running at a different number, or not
/// running at all.
/// </summary>
public interface ILineDayService
{
    /// <summary>
    /// Shuts a line for a date. Nobody is placed on it, nothing is warned about it, and its
    /// usual crew are free for the lines that are running.
    /// </summary>
    Task CloseAsync(Guid lineId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Puts a closed line back into service for that date.</summary>
    Task ReopenAsync(Guid lineId, DateOnly date, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILineDayService"/>
public sealed class LineDayService : ILineDayService
{
    private readonly ILineDemandRepository _demands;
    private readonly IConfigurationRepository _configuration;
    private readonly IAuditLog _auditLog;

    public LineDayService(
        ILineDemandRepository demands,
        IConfigurationRepository configuration,
        IAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(demands);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(auditLog);

        _demands = demands;
        _configuration = configuration;
        _auditLog = auditLog;
    }

    public Task CloseAsync(Guid lineId, DateOnly date, CancellationToken cancellationToken = default) =>
        SetAsync(lineId, date, closed: true, cancellationToken);

    public Task ReopenAsync(Guid lineId, DateOnly date, CancellationToken cancellationToken = default) =>
        SetAsync(lineId, date, closed: false, cancellationToken);

    private async Task SetAsync(
        Guid lineId,
        DateOnly date,
        bool closed,
        CancellationToken cancellationToken)
    {
        var existing = await _demands
            .GetAsync(date, date.AddDays(1), cancellationToken)
            .ConfigureAwait(false);

        var current = existing.FirstOrDefault(demand => demand.LineId == lineId);

        // The headcount is carried through rather than cleared, so reopening restores what
        // the line was set to run at that day rather than resetting it to standard.
        var headcount = current?.RequiredHeadcount
            ?? await StandardHeadcountAsync(lineId, cancellationToken).ConfigureAwait(false);

        await _demands.SetAsync(
            new LineDemand
            {
                LineId = lineId,
                Date = date,
                RequiredHeadcount = headcount,
                IsClosed = closed,
            },
            cancellationToken).ConfigureAwait(false);

        await _auditLog.AppendAsync(
            AuditAction.RosterEdited,
            $"Line {lineId} {(closed ? "closed" : "reopened")} for {date:yyyy-MM-dd}.",
            closed ? "Line not running." : "Line running again.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What the line normally runs at, used when no entry exists for the day yet. Closing a
    /// line has to write a headcount alongside the flag, and inventing one would change what
    /// the line runs at when it reopens.
    /// </summary>
    private async Task<int> StandardHeadcountAsync(Guid lineId, CancellationToken cancellationToken)
    {
        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);

        return configuration.Lines.FirstOrDefault(line => line.Id == lineId)?.RequiredHeadcount ?? 0;
    }
}
