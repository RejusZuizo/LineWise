namespace Linewise.Domain.Entities;

/// <summary>A production line to be staffed.</summary>
public sealed record ProductionLine
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Order the line appears in on screen and on the printed sheet.</summary>
    public int DisplayOrder { get; init; }

    /// <summary>How many people the line needs, the line leader included.</summary>
    public required int RequiredHeadcount { get; init; }

    /// <summary>
    /// How many operating assistants this line wants, out of its headcount rather than on
    /// top of it. Zero means the line does not use them, which keeps every existing line
    /// behaving exactly as it did.
    /// </summary>
    public int RequiredOperatingAssistants { get; init; }

    /// <summary>
    /// Skills every person on this line must hold, by <see cref="Skill"/> identifier.
    /// Absolute: never violated by the engine.
    /// </summary>
    public IReadOnlySet<Guid> RequiredSkillIds { get; init; } = new HashSet<Guid>();

    /// <summary>
    /// What the line is for, in the manager's own words. Optional, and shown wherever the
    /// line is being chosen rather than merely listed.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// How the line is physically arranged: who stands where, which end the product comes
    /// off, anything somebody arriving at it needs to be told.
    /// </summary>
    /// <remarks>
    /// Printed on that line's own sheet rather than on the full roster. It is written for
    /// the people standing at the line, and putting it on the sheet in the office would be
    /// putting it where nobody who needs it is looking.
    /// </remarks>
    public string LayoutNotes { get; init; } = string.Empty;

    /// <summary>
    /// Hex colour used to distinguish the line on screen and in print. Never the only
    /// thing carrying a meaning; always paired with a label.
    /// </summary>
    public string AccentColour { get; init; } = "#5B6670";
}
