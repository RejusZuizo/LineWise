using Linewise.Domain.Enums;

namespace Linewise.Domain.Auditing;

/// <summary>The result of walking the audit chain.</summary>
/// <param name="Break">How the chain failed, or None.</param>
/// <param name="FirstBrokenSequence">
/// The first entry that did not check out. Null when the chain is intact. Only the first is
/// reported: once a link is broken everything after it is suspect anyway.
/// </param>
public sealed record AuditChainVerification(AuditChainBreak Break, long? FirstBrokenSequence)
{
    public static AuditChainVerification Intact { get; } = new(AuditChainBreak.None, null);

    public bool IsIntact => Break == AuditChainBreak.None;
}
