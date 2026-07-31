namespace Linewise.Domain.Entities;

/// <summary>
/// Somebody who can be rostered. Never hard deleted, only deactivated, so historic
/// rosters stay intact.
/// </summary>
public sealed record Employee
{
    public required Guid Id { get; init; }

    /// <summary>Personal data. Never written to a log or an export without sanitising.</summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Other spellings this person appears under on the imported sheet. Matched against
    /// as well as <see cref="FullName"/>.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public bool IsActive { get; init; } = true;

    /// <summary>Agency or temporary. Added during import review rather than up front.</summary>
    public bool IsTemporary { get; init; }

    /// <summary>Skills held, by <see cref="Skill"/> identifier. A hard eligibility filter.</summary>
    public IReadOnlySet<Guid> SkillIds { get; init; } = new HashSet<Guid>();
}
