namespace Linewise.Domain.Entities;

/// <summary>
/// A qualification such as a machine ticket or a food hygiene certificate. Used only as
/// a hard eligibility filter, never as a preference.
/// </summary>
public sealed record Skill
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }
}
