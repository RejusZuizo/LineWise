using Linewise.Domain.Enums;

namespace Linewise.Domain.Rostering;

/// <summary>
/// Why an assignment was made. Carried on every assignment so the manager can point at a
/// name on the wall sheet and get a reason rather than a shrug.
/// </summary>
/// <param name="Rule">The rule that placed the employee.</param>
/// <param name="PreferenceRank">
/// The rank of the preference used, where 1 is first choice. Null when no preference was
/// involved.
/// </param>
public sealed record AssignmentExplanation(PlacementRule Rule, int? PreferenceRank = null)
{
    public static AssignmentExplanation ManualOverride { get; } = new(PlacementRule.ManualOverride);

    public static AssignmentExplanation LeaderSelection { get; } = new(PlacementRule.LeaderSelection);

    public static AssignmentExplanation Backfill { get; } = new(PlacementRule.Backfill);

    public static AssignmentExplanation Mandatory(int rank) =>
        new(PlacementRule.MandatoryPreference, rank);

    public static AssignmentExplanation Preference(int rank) =>
        new(PlacementRule.PreferenceRank, rank);
}
