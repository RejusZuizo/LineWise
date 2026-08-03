namespace Linewise.Domain.Enums;

/// <summary>
/// Whether an employee can be rostered on a given date. Read from the imported
/// availability sheet.
/// </summary>
/// <remarks>
/// Which mark on the sheet means which status is configured per import template, never
/// hardcoded. A site that writes nothing for a day off and a site that colours it red both
/// have to work, and neither should need a rebuild.
/// </remarks>
public enum AvailabilityStatus
{
    /// <summary>
    /// Not available, and no reason recorded. What an unmarked cell means: silence is read
    /// as absence rather than as availability.
    /// </summary>
    Off = 0,

    /// <summary>Available on their normal pattern.</summary>
    Working = 1,

    /// <summary>
    /// Available, but working beyond their normal pattern. The only status that permits a
    /// mandatory line preference to be broken.
    /// </summary>
    Overtime = 2,

    /// <summary>
    /// Booked off. Not available, and distinct from <see cref="Off"/> because the manager
    /// needs to tell a planned absence from a blank on the sheet when a line comes up short.
    /// </summary>
    /// <remarks>
    /// The status is as far as this goes. No reason for the absence beyond this is stored,
    /// which is a data protection position and not an oversight.
    /// </remarks>
    Holiday = 3,
}
