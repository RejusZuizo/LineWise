namespace Linewise.Domain.Enums;

/// <summary>
/// The rule that put an employee on a line. Recorded on every assignment so the manager
/// can ask why somebody is where they are and get an answer.
/// </summary>
public enum PlacementRule
{
    /// <summary>Placed by hand and locked. The engine did not choose this.</summary>
    ManualOverride = 0,

    /// <summary>Placed by a mandatory line preference.</summary>
    MandatoryPreference = 1,

    /// <summary>Placed by leader selection, having led least recently.</summary>
    LeaderSelection = 2,

    /// <summary>
    /// Placed on a line running above its usual headcount because they are on overtime,
    /// which is what the overtime is covering.
    /// </summary>
    OvertimeCover = 5,

    /// <summary>Placed by a ranked line preference. The rank is recorded alongside.</summary>
    PreferenceRank = 3,

    /// <summary>Placed to fill a remaining empty slot. No preference applied.</summary>
    Backfill = 4,
}
