using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Rostering;

/// <summary>
/// Places people on lines by applying ten rules in a fixed order. Greedy, not optimal:
/// it produces a good roster in well under a second and can explain every placement,
/// which is worth more to the manager than mathematical optimality nobody can audit.
/// </summary>
/// <remarks>
/// Pure. No I/O, no logging, no static state, and the same request always produces the
/// same week.
/// </remarks>
public sealed class AssignmentEngine : IAssignmentEngine
{
    private readonly ITieBreakStrategy _tieBreak;

    public AssignmentEngine(ITieBreakStrategy tieBreak)
    {
        ArgumentNullException.ThrowIfNull(tieBreak);

        _tieBreak = tieBreak;
    }

    public RosterWeek Generate(AssignmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new GenerationContext(request);
        var days = new List<RosterDay>(context.Shifts.Count);

        foreach (var shift in context.Shifts)
        {
            days.Add(GenerateShift(shift, context));
        }

        return new RosterWeek
        {
            WeekStart = request.WeekStart,
            Days = days,
        };
    }

    private RosterDay GenerateShift(Shift shift, GenerationContext context)
    {
        var warnings = new List<RosterWarning>();
        var placements = context.Lines.ToDictionary(line => line.Id, _ => new List<Assignment>());
        var assignedToday = context.AssignedOn(shift.Date);

        void Place(ProductionLine line, Guid employeeId, AssignmentRole role, AssignmentExplanation explanation)
        {
            placements[line.Id].Add(new Assignment
            {
                Date = shift.Date,
                ShiftId = shift.Id,
                LineId = line.Id,
                EmployeeId = employeeId,
                Role = role,
                Source = AssignmentSource.Auto,
                Explanation = explanation,
            });

            assignedToday.Add(employeeId);
            context.Ledger.RecordPlacement(employeeId, context.IsPreferredLine(employeeId, line.Id));
        }

        TieBreakContext TieBreakFor(ProductionLine line) => new()
        {
            Date = shift.Date,
            LineId = line.Id,
            Ledger = context.Ledger,
        };

        // Rule 1. Locked assignments are placed first and never moved. They are the
        // manager's decisions and outrank everything below, including eligibility.
        foreach (var locked in context.LockedFor(shift))
        {
            var line = context.LineById(locked.LineId);

            if (line is null)
            {
                continue;
            }

            placements[line.Id].Add(locked);
            assignedToday.Add(locked.EmployeeId);
            context.Ledger.RecordPlacement(locked.EmployeeId, context.IsPreferredLine(locked.EmployeeId, line.Id));

            if (locked.Role == AssignmentRole.LineLeader)
            {
                context.Ledger.RecordLead(locked.EmployeeId, shift.Date);
            }

            // Kept, but said out loud. A lock that contradicts availability or eligibility is
            // usually a mistake somebody wants to hear about.
            if (!context.CanBeRostered(locked.EmployeeId, shift.Date))
            {
                warnings.Add(RosterWarnings.LockedAssignmentConflictsWithAvailability(line, locked.EmployeeId, shift.Date));
            }

            if (!context.IsEligibleFor(locked.EmployeeId, line))
            {
                warnings.Add(RosterWarnings.LockedAssignmentViolatesEligibility(line, locked.EmployeeId, shift.Date));
            }

            // Kept, and said out loud, the same as the other two. Closing a line and locking
            // somebody onto it are both the manager's decisions and only one of them can
            // have been the one they meant.
            if (context.IsClosed(line, shift.Date))
            {
                warnings.Add(RosterWarnings.LockedAssignmentOnAClosedLine(line, locked.EmployeeId, shift.Date));
            }
        }

        // Rule 2. Anyone whose status today is not Working or Overtime is out of scope.
        var availableToday = context.ActiveEmployeeIds
            .Where(employeeId => context.CanBeRostered(employeeId, shift.Date))
            .ToList();

        // Rule 3. Mandatory preferences, before anybody else competes for the places.
        foreach (var preference in context.MandatoryPreferences)
        {
            var line = context.LineById(preference.LineId);

            if (line is null || context.IsClosed(line, shift.Date)
                || !context.CanBeRostered(preference.EmployeeId, shift.Date))
            {
                continue;
            }

            if (assignedToday.Contains(preference.EmployeeId))
            {
                // A lock already placed them. Rule 1 wins.
                continue;
            }

            var onOvertime = context.StatusOn(preference.EmployeeId, shift.Date) == AvailabilityStatus.Overtime;

            if (!context.IsEligibleFor(preference.EmployeeId, line))
            {
                // Blocked from the line, or short of a skill it requires. Rule 7 is absolute,
                // so the mandatory preference loses and the manager hears about it.
                warnings.Add(RosterWarnings.MandatoryPreferenceNotHonoured(line, preference.EmployeeId, shift.Date));
                continue;
            }

            if (placements[line.Id].Count >= context.HeadcountFor(line, shift.Date))
            {
                // No room left, so the preference has to break. Overtime is the one status
                // under which breaking it is allowed rather than wrong.
                warnings.Add(onOvertime
                    ? RosterWarnings.MandatoryPreferenceBrokenForOvertime(line, preference.EmployeeId, shift.Date)
                    : RosterWarnings.MandatoryPreferenceNotHonoured(line, preference.EmployeeId, shift.Date));
                continue;
            }

            Place(line, preference.EmployeeId, AssignmentRole.Worker, AssignmentExplanation.Mandatory(preference.Rank));
        }

        // Rule 4. One leader per line, preferring whoever has led least recently. A leader
        // counts toward the line headcount like anybody else.
        foreach (var line in context.Lines.Where(line => !context.IsClosed(line, shift.Date)))
        {
            if (placements[line.Id].Exists(assignment => assignment.Role == AssignmentRole.LineLeader))
            {
                continue;
            }

            var alreadyOnLine = placements[line.Id].Select(assignment => assignment.EmployeeId).ToHashSet();
            var hasRoom = placements[line.Id].Count < context.HeadcountFor(line, shift.Date);

            var candidates = availableToday
                .Where(employeeId => context.CanLead(employeeId, line.Id))
                .Where(employeeId => alreadyOnLine.Contains(employeeId)
                    || (hasRoom && !assignedToday.Contains(employeeId) && context.IsEligibleFor(employeeId, line)))
                .ToList();

            if (candidates.Count == 0)
            {
                // Rule 9 raises the warning once the day is settled.
                continue;
            }

            var leader = candidates
                .OrderBy(employeeId => context.Ledger.LastLedOn(employeeId) ?? DateOnly.MinValue)
                .ThenBy(employeeId => context.Ledger.LeadCount(employeeId))
                .ThenBy(employeeId => employeeId)
                .First();

            if (alreadyOnLine.Contains(leader))
            {
                // Promote in place and keep the explanation that put them on the line, which
                // is a better answer to "why are they here" than "because they lead it".
                var index = placements[line.Id].FindIndex(assignment => assignment.EmployeeId == leader);
                placements[line.Id][index] = placements[line.Id][index] with { Role = AssignmentRole.LineLeader };
            }
            else
            {
                Place(line, leader, AssignmentRole.LineLeader, AssignmentExplanation.LeaderSelection);
            }

            context.Ledger.RecordLead(leader, shift.Date);
        }

        // Rule 4b. Operating assistants, chosen the same way and immediately after, because
        // a line's second in charge is picked from the same pool the leader came out of and
        // choosing them before ranked preferences is what stops the pool being empty by the
        // time we get here.
        //
        // A line asks for a number rather than exactly one, and asking for none is the
        // ordinary case, so this loop does nothing at all for a site that does not use them.
        foreach (var line in context.Lines.Where(line => !context.IsClosed(line, shift.Date)))
        {
            var wanted = line.RequiredOperatingAssistants;

            if (wanted <= 0)
            {
                continue;
            }

            for (var placed = CountAssistants(placements[line.Id]); placed < wanted; placed++)
            {
                var alreadyOnLine = placements[line.Id].Select(assignment => assignment.EmployeeId).ToHashSet();
                var hasRoom = placements[line.Id].Count < context.HeadcountFor(line, shift.Date);

                var candidates = availableToday
                    .Where(employeeId => context.CanAssist(employeeId, line.Id))
                    .Where(employeeId => !IsAlreadyRanked(placements[line.Id], employeeId))
                    .Where(employeeId => alreadyOnLine.Contains(employeeId)
                        || (hasRoom && !assignedToday.Contains(employeeId) && context.IsEligibleFor(employeeId, line)))
                    .ToList();

                if (candidates.Count == 0)
                {
                    // Rule 9 raises the warning once the day is settled, the same as a line
                    // that could not find a leader.
                    break;
                }

                var assistant = candidates
                    .OrderBy(employeeId => context.Ledger.LastLedOn(employeeId) ?? DateOnly.MinValue)
                    .ThenBy(employeeId => employeeId)
                    .First();

                if (alreadyOnLine.Contains(assistant))
                {
                    // Promoted in place, keeping the explanation that put them on the line.
                    var index = placements[line.Id].FindIndex(a => a.EmployeeId == assistant);
                    placements[line.Id][index] =
                        placements[line.Id][index] with { Role = AssignmentRole.OperatingAssistant };
                }
                else
                {
                    Place(line, assistant, AssignmentRole.OperatingAssistant, AssignmentExplanation.LeaderSelection);
                }
            }
        }

        // Rule 5. Overtime goes to the lines running above their usual headcount, because
        // that is what overtime is being paid for. This has to happen before ranked
        // preferences rather than at the backfill: an overtime worker with a first choice
        // elsewhere would already be standing on it by then, and would never have been
        // available to cover the busy line at all.
        foreach (var line in context.Lines.Where(line => context.IsRunningHot(line, shift.Date)))
        {
            var room = context.HeadcountFor(line, shift.Date) - placements[line.Id].Count;

            if (room <= 0)
            {
                continue;
            }

            var candidates = availableToday
                .Where(employeeId => !assignedToday.Contains(employeeId))
                .Where(employeeId => context.StatusOn(employeeId, shift.Date) == AvailabilityStatus.Overtime)
                .Where(employeeId => context.IsEligibleFor(employeeId, line))
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            foreach (var employeeId in _tieBreak.Prioritise(candidates, TieBreakFor(line)).Take(room))
            {
                Place(line, employeeId, AssignmentRole.Worker, AssignmentExplanation.OvertimeCover);
            }
        }

        // Rules 6 and 7. Every first choice across the whole workforce, then every second
        // choice, and so on. Never employee by employee, which starves whoever sorts last.
        for (var rank = 1; rank <= context.HighestPreferenceRank; rank++)
        {
            foreach (var line in context.Lines.Where(line => !context.IsClosed(line, shift.Date)))
            {
                var room = context.HeadcountFor(line, shift.Date) - placements[line.Id].Count;

                if (room <= 0)
                {
                    continue;
                }

                var candidates = availableToday
                    .Where(employeeId => !assignedToday.Contains(employeeId))
                    .Where(employeeId => context.PreferenceFor(employeeId, line.Id) is
                    { Type: PreferenceType.Preferred } preference && preference.Rank == rank)
                    .Where(employeeId => context.IsEligibleFor(employeeId, line))
                    .ToList();

                if (candidates.Count == 0)
                {
                    continue;
                }

                foreach (var employeeId in _tieBreak.Prioritise(candidates, TieBreakFor(line)).Take(room))
                {
                    Place(line, employeeId, AssignmentRole.Worker, AssignmentExplanation.Preference(rank));
                }
            }
        }

        // Rule 9. Whatever is still empty gets filled from whoever is still free.
        foreach (var line in context.Lines.Where(line => !context.IsClosed(line, shift.Date)))
        {
            var room = context.HeadcountFor(line, shift.Date) - placements[line.Id].Count;

            if (room <= 0)
            {
                continue;
            }

            var candidates = availableToday
                .Where(employeeId => !assignedToday.Contains(employeeId))
                .Where(employeeId => context.IsEligibleFor(employeeId, line))
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            foreach (var employeeId in _tieBreak.Prioritise(candidates, TieBreakFor(line)).Take(room))
            {
                Place(line, employeeId, AssignmentRole.Worker, AssignmentExplanation.Backfill);
            }
        }

        // Rule 10. Say what is wrong with the result rather than refusing to produce one.
        //
        // A closed line is skipped entirely. It is not short of people, it has no leader
        // because it needs none, and saying so on a day the line is not running would bury
        // the lines that are.
        foreach (var line in context.Lines.Where(line => !context.IsClosed(line, shift.Date)))
        {
            var onLine = placements[line.Id];
            var wanted = context.HeadcountFor(line, shift.Date);

            // Short handed means below the smaller of the two: a line the manager has quietened
            // for the day is not short handed for being at the number they asked for.
            var mustHave = Math.Min(line.RequiredHeadcount, wanted);

            if (onLine.Count < mustHave)
            {
                warnings.Add(RosterWarnings.LineUnderHeadcount(line, onLine.Count, mustHave, shift.Date));
            }
            else if (onLine.Count < wanted)
            {
                // It will run, but without the extra cover that was asked for. A different
                // problem from being short handed, and worth saying differently.
                warnings.Add(RosterWarnings.LineDemandNotCovered(line, wanted, onLine.Count, shift.Date));
            }

            if (!onLine.Exists(assignment => assignment.Role == AssignmentRole.LineLeader))
            {
                warnings.Add(RosterWarnings.LineHasNoLeader(line, shift.Date));
            }

            if (line.RequiredOperatingAssistants > 0)
            {
                var assistants = onLine.Count(a => a.Role == AssignmentRole.OperatingAssistant);

                if (assistants < line.RequiredOperatingAssistants)
                {
                    warnings.Add(RosterWarnings.LineShortOfOperatingAssistants(
                        line,
                        assistants,
                        line.RequiredOperatingAssistants,
                        shift.Date));
                }
            }

            // Somebody on overtime standing on a quiet line. Not wrong, and not worth
            // undoing, but the manager is paying a premium for it.
            if (!context.IsRunningHot(line, shift.Date))
            {
                foreach (var assignment in onLine.Where(assignment =>
                    context.StatusOn(assignment.EmployeeId, shift.Date) == AvailabilityStatus.Overtime))
                {
                    warnings.Add(RosterWarnings.OvertimeNotOnABusyLine(line, assignment.EmployeeId, shift.Date));
                }
            }
        }

        foreach (var employeeId in availableToday.Where(employeeId => !assignedToday.Contains(employeeId)))
        {
            warnings.Add(RosterWarnings.EmployeeUnassigned(employeeId, shift.Date));
        }

        var assignments = context.Lines
            .SelectMany(line => placements[line.Id]
                .OrderByDescending(assignment => assignment.Role == AssignmentRole.LineLeader)
                .ThenBy(assignment => assignment.EmployeeId))
            .ToList();

        return new RosterDay
        {
            Date = shift.Date,
            ShiftId = shift.Id,
            Assignments = assignments,
            Warnings = warnings,
        };
    }

    private static int CountAssistants(IEnumerable<Assignment> onLine) =>
        onLine.Count(assignment => assignment.Role == AssignmentRole.OperatingAssistant);

    /// <summary>
    /// Already the leader or already an assistant. Somebody cannot hold two ranks on one
    /// line, and promoting the leader again would quietly cost the line its leader.
    /// </summary>
    private static bool IsAlreadyRanked(IEnumerable<Assignment> onLine, Guid employeeId) =>
        onLine.Any(assignment => assignment.EmployeeId == employeeId
            && assignment.Role != AssignmentRole.Worker);
}
