using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Rostering;

/// <inheritdoc cref="IGapFiller"/>
public sealed class GapFiller : IGapFiller
{
    private readonly IConfigurationRepository _configuration;
    private readonly IAvailabilityRepository _availability;
    private readonly ILineDemandRepository _demands;
    private readonly IRosterRepository _rosters;

    public GapFiller(
        IConfigurationRepository configuration,
        IAvailabilityRepository availability,
        ILineDemandRepository demands,
        IRosterRepository rosters)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(demands);
        ArgumentNullException.ThrowIfNull(rosters);

        _configuration = configuration;
        _availability = availability;
        _demands = demands;
        _rosters = rosters;
    }

    public async Task<int> FillAsync(DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        var stored = await _rosters.GetLatestAsync(weekStart, cancellationToken).ConfigureAwait(false);

        if (stored is null)
        {
            // No week to top up. Generating one is the operator's move.
            return 0;
        }

        var weekEnd = weekStart.AddDays(7);

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);
        var availability = await _availability.GetAsync(weekStart, weekEnd, cancellationToken).ConfigureAwait(false);
        var demands = await _demands.GetAsync(weekStart, weekEnd, cancellationToken).ConfigureAwait(false);

        var lineNames = configuration.Lines.ToDictionary(line => line.Id, line => line.Name);
        var filled = 0;
        var days = new List<RosterDay>(stored.Roster.Days.Count);

        foreach (var day in stored.Roster.Days)
        {
            var statuses = availability
                .Where(record => record.Date == day.Date)
                .ToDictionary(record => record.EmployeeId, record => record.Status);

            var assignments = day.Assignments.ToList();

            foreach (var line in configuration.Lines.OrderBy(line => line.DisplayOrder))
            {
                var demand = demands.FirstOrDefault(d => d.LineId == line.Id && d.Date == day.Date);

                // A line that is not running is not short of anybody.
                if (demand?.IsClosed == true)
                {
                    continue;
                }

                filled += Fill(line, day.Date, demand, configuration, statuses, lineNames, assignments);
            }

            days.Add(day with { Assignments = assignments });
        }

        if (filled > 0)
        {
            await _rosters
                .SaveDraftAsync(weekStart, stored.Roster with { Days = days }, cancellationToken)
                .ConfigureAwait(false);
        }

        return filled;
    }

    /// <summary>
    /// One line on one day: a leader if it has none, the assistants it asks for, then bodies
    /// up to its headcount. In that order, because a line that can run without its full
    /// complement cannot run without somebody in charge of it.
    /// </summary>
    private static int Fill(
        ProductionLine line,
        DateOnly date,
        LineDemand? demand,
        RosterConfiguration configuration,
        IReadOnlyDictionary<Guid, AvailabilityStatus> statuses,
        IReadOnlyDictionary<Guid, string> lineNames,
        List<Assignment> assignments)
    {
        var filled = 0;
        var wanted = demand?.RequiredHeadcount ?? line.RequiredHeadcount;

        bool Place(AssignmentRole role, Func<ReplacementCandidate, bool> suitable, AssignmentExplanation why)
        {
            var onLine = assignments.Where(a => a.LineId == line.Id).ToList();

            if (onLine.Count >= wanted)
            {
                return false;
            }

            var placedToday = assignments.ToDictionary(a => a.EmployeeId, a => a.LineId);

            var candidate = ReplacementRanking
                .For(line, date, configuration, statuses, placedToday, lineNames)

                // Never taken off another line. Filling one hole by opening another is a
                // decision for the manager, through a picker that names the line it empties.
                .FirstOrDefault(c => !c.LeavesAHoleElsewhere && suitable(c));

            if (candidate is null)
            {
                return false;
            }

            assignments.Add(new Assignment
            {
                Date = date,
                ShiftId = assignments.Count > 0 ? assignments[0].ShiftId : Guid.Empty,
                LineId = line.Id,
                EmployeeId = candidate.EmployeeId,
                Role = role,

                // Auto and unlocked. The rules chose this, not the manager, so a full
                // regenerate is free to arrange it better. Marking it manual would be
                // claiming a decision nobody made.
                Source = AssignmentSource.Auto,
                Explanation = why,
            });

            return true;
        }

        if (!assignments.Any(a => a.LineId == line.Id && a.Role == AssignmentRole.LineLeader)
            && Place(AssignmentRole.LineLeader, c => c.CanLead, AssignmentExplanation.LeaderSelection))
        {
            filled++;
        }

        while (assignments.Count(a => a.LineId == line.Id && a.Role == AssignmentRole.OperatingAssistant)
                   < line.RequiredOperatingAssistants
               && Place(AssignmentRole.OperatingAssistant, c => c.CanAssist, AssignmentExplanation.LeaderSelection))
        {
            filled++;
        }

        while (Place(AssignmentRole.Worker, _ => true, AssignmentExplanation.Backfill))
        {
            filled++;
        }

        return filled;
    }
}
