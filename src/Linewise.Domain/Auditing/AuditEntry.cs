using Linewise.Domain.Enums;

namespace Linewise.Domain.Auditing;

/// <summary>
/// One line in the tamper evident history. Append only: there is no path in the
/// application that updates or deletes one of these.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>Position in the chain. Assigned by the store on append.</summary>
    public long Sequence { get; init; }

    public required DateTime OccurredAtUtc { get; init; }

    /// <summary>The Windows account that made the change. A record, not a permission check.</summary>
    public required string UserName { get; init; }

    public required AuditAction Action { get; init; }

    /// <summary>
    /// What changed, written in terms of identifiers. Never an employee name: the audit log
    /// is one of the places personal data would otherwise accumulate quietly for years.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>Why, in the operator's own words. Empty when none was given.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>The hash of the entry before this one, or the genesis hash for the first.</summary>
    public required string PreviousHash { get; init; }

    /// <summary>SHA-256 over this entry's contents and <see cref="PreviousHash"/>.</summary>
    public required string Hash { get; init; }
}
