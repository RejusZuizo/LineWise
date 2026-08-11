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
    /// Hex colour used to distinguish the line on screen and in print. Never the only
    /// thing carrying a meaning; always paired with a label.
    /// </summary>
    public string AccentColour { get; init; } = "#5B6670";
}
