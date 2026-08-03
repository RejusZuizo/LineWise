using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Printing;

/// <summary>What happened to one person on one day since the last print.</summary>
public enum RosterChangeKind
{
    /// <summary>Was not on the roster that day and now is.</summary>
    Added = 0,

    /// <summary>Was on the roster that day and now is not. Usually somebody ringing in sick.</summary>
    Removed = 1,

    /// <summary>Same day, different line.</summary>
    Moved = 2,

    /// <summary>Same line, but now leading it, or no longer leading it.</summary>
    RoleChanged = 3,
}

/// <param name="Date">The day affected.</param>
/// <param name="ShiftId">The shift affected.</param>
/// <param name="EmployeeId">Who moved. Resolved to a name only at the point of printing.</param>
/// <param name="FromLineId">Where they were, or null when they were not on that day.</param>
/// <param name="ToLineId">Where they are now, or null when they have come off.</param>
/// <param name="Kind">What sort of change it is.</param>
/// <param name="NowLeading">Whether they are leading the line after the change.</param>
public sealed record RosterChange(
    DateOnly Date,
    Guid ShiftId,
    Guid EmployeeId,
    Guid? FromLineId,
    Guid? ToLineId,
    RosterChangeKind Kind,
    bool NowLeading);

/// <summary>
/// Works out what changed between the roster on the wall and the roster now.
/// </summary>
/// <remarks>
/// Pure, and separate from the printing, because "what changed" is a question worth being
/// able to answer on screen as well as on paper, and because comparing two rosters is much
/// easier to test than a PDF.
/// </remarks>
public static class RosterDiff
{
    public static IReadOnlyList<RosterChange> Between(RosterWeek previous, RosterWeek current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var before = Index(previous);
        var after = Index(current);
        var changes = new List<RosterChange>();

        foreach (var (key, now) in after)
        {
            if (!before.TryGetValue(key, out var was))
            {
                changes.Add(new RosterChange(
                    key.Date,
                    key.ShiftId,
                    key.EmployeeId,
                    null,
                    now.LineId,
                    RosterChangeKind.Added,
                    now.Role == AssignmentRole.LineLeader));

                continue;
            }

            if (was.LineId != now.LineId)
            {
                changes.Add(new RosterChange(
                    key.Date,
                    key.ShiftId,
                    key.EmployeeId,
                    was.LineId,
                    now.LineId,
                    RosterChangeKind.Moved,
                    now.Role == AssignmentRole.LineLeader));

                continue;
            }

            // Same line, but somebody else is running it now, or they are. Worth a slip of
            // its own: the people at the line need to know who to ask.
            if (was.Role != now.Role)
            {
                changes.Add(new RosterChange(
                    key.Date,
                    key.ShiftId,
                    key.EmployeeId,
                    was.LineId,
                    now.LineId,
                    RosterChangeKind.RoleChanged,
                    now.Role == AssignmentRole.LineLeader));
            }
        }

        foreach (var (key, was) in before.Where(entry => !after.ContainsKey(entry.Key)))
        {
            changes.Add(new RosterChange(
                key.Date,
                key.ShiftId,
                key.EmployeeId,
                was.LineId,
                null,
                RosterChangeKind.Removed,
                false));
        }

        return changes
            .OrderBy(change => change.Date)
            .ThenBy(change => change.ShiftId)
            .ThenBy(change => change.Kind)
            .ThenBy(change => change.EmployeeId)
            .ToList();
    }

    /// <summary>
    /// One entry per person per day and shift. The engine already refuses to roster somebody
    /// twice in a day, so the last one wins if a hand edit ever managed it.
    /// </summary>
    private static Dictionary<(DateOnly Date, Guid ShiftId, Guid EmployeeId), (Guid LineId, AssignmentRole Role)> Index(
        RosterWeek week)
    {
        var index = new Dictionary<(DateOnly, Guid, Guid), (Guid, AssignmentRole)>();

        foreach (var day in week.Days)
        {
            foreach (var assignment in day.Assignments)
            {
                index[(day.Date, day.ShiftId, assignment.EmployeeId)] = (assignment.LineId, assignment.Role);
            }
        }

        return index;
    }
}
