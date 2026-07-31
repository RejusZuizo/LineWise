using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Tests.Builders;

internal static class TestEngine
{
    /// <summary>The engine as it ships, with the fairness tie break.</summary>
    public static IAssignmentEngine Default() => new AssignmentEngine(new FairnessTieBreakStrategy());

    public static IAssignmentEngine With(ITieBreakStrategy tieBreak) => new AssignmentEngine(tieBreak);

    /// <summary>Turns a generated week into the history a later week would be given.</summary>
    public static List<HistoricAssignment> AsHistory(this RosterWeek week) =>
        week.AllAssignments
            .Select(assignment => new HistoricAssignment
            {
                Date = assignment.Date,
                EmployeeId = assignment.EmployeeId,
                LineId = assignment.LineId,
                Role = assignment.Role,
            })
            .ToList();

    public static IEnumerable<Assignment> OnLine(this RosterDay day, Guid lineId) =>
        day.Assignments.Where(assignment => assignment.LineId == lineId);

    public static Assignment For(this RosterDay day, Guid employeeId) =>
        day.Assignments.Single(assignment => assignment.EmployeeId == employeeId);

    public static bool Has(this RosterDay day, Guid employeeId) =>
        day.Assignments.Any(assignment => assignment.EmployeeId == employeeId);

    public static IEnumerable<RosterWarning> Coded(this RosterDay day, WarningCode code) =>
        day.Warnings.Where(warning => warning.Code == code);
}
