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
    // A new instance each time rather than a cached one. These read like constants and were
    // written as constants, but an explanation belongs to exactly one assignment: sharing a
    // single instance across every backfilled placement in a week leaves the store able to
    // attach it to only the first of them. Value equality means nothing else notices.
    public static AssignmentExplanation ManualOverride => new(PlacementRule.ManualOverride);

    public static AssignmentExplanation LeaderSelection => new(PlacementRule.LeaderSelection);

    public static AssignmentExplanation Backfill => new(PlacementRule.Backfill);

    public static AssignmentExplanation OvertimeCover => new(PlacementRule.OvertimeCover);

    public static AssignmentExplanation Mandatory(int rank) =>
        new(PlacementRule.MandatoryPreference, rank);

    public static AssignmentExplanation Preference(int rank) =>
        new(PlacementRule.PreferenceRank, rank);
}
