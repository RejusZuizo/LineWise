using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <summary>
/// Who could take a place on a line, best first, from data already in hand.
/// </summary>
/// <remarks>
/// Pure, and shared by the picker and the gap filler so the two cannot disagree about who
/// is suitable. The picker asks about one line on one day and can afford to read the
/// database first; the filler asks about every line on every day of a week and cannot.
/// <para>
/// The same absolutes the engine applies: blocked and unskilled are not ranked low, they
/// are not returned. Anything that offers them invites the one click that puts somebody
/// where they may not be.
/// </para>
/// </remarks>
internal static class ReplacementRanking
{
    public static IReadOnlyList<ReplacementCandidate> For(
        ProductionLine line,
        DateOnly date,
        RosterConfiguration configuration,
        IReadOnlyDictionary<Guid, AvailabilityStatus> statuses,
        IReadOnlyDictionary<Guid, Guid> placedToday,
        IReadOnlyDictionary<Guid, string> lineNames)
    {
        var candidates = new List<ReplacementCandidate>();

        foreach (var employee in configuration.Employees.Where(employee => employee.IsActive))
        {
            // Silence is never read as availability, the same rule the engine applies.
            if (!statuses.TryGetValue(employee.Id, out var today)
                || today is not (AvailabilityStatus.Working or AvailabilityStatus.Overtime))
            {
                continue;
            }

            var preference = configuration.Preferences.FirstOrDefault(rule =>
                rule.EmployeeId == employee.Id && rule.LineId == line.Id);

            if (preference?.Type == PreferenceType.Blocked
                || !line.RequiredSkillIds.All(employee.SkillIds.Contains))
            {
                continue;
            }

            var placedOn = placedToday.TryGetValue(employee.Id, out var on) ? on : (Guid?)null;

            // Already here. Not a replacement for a place on this line.
            if (placedOn == line.Id)
            {
                continue;
            }

            candidates.Add(new ReplacementCandidate
            {
                EmployeeId = employee.Id,
                DisplayName = employee.FullName,
                Status = today,
                PreferenceRank = preference?.Type is PreferenceType.Preferred or PreferenceType.Mandatory
                    ? preference.Rank
                    : null,
                IsRequiredHere = preference?.Type == PreferenceType.Mandatory,
                CanLead = configuration.LeaderEligibilities.Any(eligibility =>
                    eligibility.EmployeeId == employee.Id && eligibility.LineId == line.Id),
                CanAssist = configuration.OperatingAssistantEligibilities.Any(eligibility =>
                    eligibility.EmployeeId == employee.Id && eligibility.LineId == line.Id),
                AlreadyOnLineId = placedOn,
                AlreadyOnLineName = placedOn is { } other ? lineNames.GetValueOrDefault(other) : null,
            });
        }

        return [.. candidates.OrderBy(Rank).ThenBy(c => c.DisplayName, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// Somebody standing about before somebody already on a line; a person required here
    /// before one who merely chose it; a higher choice before a lower; plain availability
    /// before overtime. The name is the last thing consulted rather than the first.
    /// </summary>
    private static (int Placed, int Required, int Rank, int Premium) Rank(ReplacementCandidate candidate) =>
        (candidate.LeavesAHoleElsewhere ? 1 : 0,
            candidate.IsRequiredHere ? 0 : 1,
            candidate.PreferenceRank ?? int.MaxValue,
            candidate.Status == AvailabilityStatus.Overtime ? 1 : 0);
}
