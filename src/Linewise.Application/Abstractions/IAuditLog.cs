using Linewise.Domain.Auditing;
using Linewise.Domain.Enums;

namespace Linewise.Application.Abstractions;

/// <summary>
/// The tamper evident history. Append and read only: there is deliberately no update and no
/// delete on this interface, because the whole point of the chain is that history does not
/// change quietly.
/// </summary>
public interface IAuditLog
{
    Task<AuditEntry> AppendAsync(
        AuditAction action,
        string summary,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEntry>> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Walks the chain and reports the first link that does not check out.</summary>
    Task<AuditChainVerification> VerifyAsync(CancellationToken cancellationToken = default);
}
