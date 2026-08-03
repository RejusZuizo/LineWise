namespace Linewise.Domain.Enums;

/// <summary>How an audit chain failed verification.</summary>
public enum AuditChainBreak
{
    /// <summary>The chain is intact.</summary>
    None = 0,

    /// <summary>An entry's contents no longer produce the hash stored against it: it was edited.</summary>
    HashMismatch = 1,

    /// <summary>
    /// An entry does not point at the hash of the entry before it: something was removed,
    /// inserted or reordered.
    /// </summary>
    BrokenLink = 2,
}
