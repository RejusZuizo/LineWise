using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Linewise.Domain.Enums;

namespace Linewise.Domain.Auditing;

/// <summary>
/// Hashes and verifies the audit chain. Each entry carries the hash of the one before it,
/// so editing a historic entry, deleting one, or reordering them stops being silent and
/// starts being detectable.
/// </summary>
/// <remarks>
/// Pure, and deliberately in the domain rather than in the database layer: the chain is a
/// rule about what an audit log is, not a detail of how one is stored.
/// </remarks>
public static class AuditChain
{
    /// <summary>What the first entry points at, there being nothing before it.</summary>
    public static string GenesisHash { get; } = new string('0', 64);

    /// <summary>
    /// Unit separator. A control character rather than a comma or a pipe, so that a reason
    /// containing punctuation cannot be crafted to collide with a different entry.
    /// </summary>
    private static readonly char FieldSeparator = (char)0x1F;

    /// <summary>Builds the next entry in the chain and seals it with its hash.</summary>
    public static AuditEntry Link(
        DateTime occurredAtUtc,
        string userName,
        AuditAction action,
        string summary,
        string reason,
        string previousHash)
    {
        var unsealed = new AuditEntry
        {
            OccurredAtUtc = occurredAtUtc,
            UserName = userName,
            Action = action,
            Summary = summary,
            Reason = reason,
            PreviousHash = previousHash,
            Hash = string.Empty,
        };

        return unsealed with { Hash = ComputeHash(unsealed) };
    }

    public static string ComputeHash(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonicalise(entry))));
    }

    /// <summary>
    /// Walks the chain in order and reports the first link that does not check out.
    /// </summary>
    public static AuditChainVerification Verify(IEnumerable<AuditEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var expectedPrevious = GenesisHash;

        foreach (var entry in entries.OrderBy(entry => entry.Sequence))
        {
            // Points at the wrong entry: something was removed, inserted or reordered.
            if (!string.Equals(entry.PreviousHash, expectedPrevious, StringComparison.Ordinal))
            {
                return new AuditChainVerification(AuditChainBreak.BrokenLink, entry.Sequence);
            }

            // Contents no longer produce the hash stored against them: this one was edited.
            if (!string.Equals(entry.Hash, ComputeHash(entry), StringComparison.Ordinal))
            {
                return new AuditChainVerification(AuditChainBreak.HashMismatch, entry.Sequence);
            }

            expectedPrevious = entry.Hash;
        }

        return AuditChainVerification.Intact;
    }

    /// <summary>
    /// The exact bytes that get hashed.
    /// </summary>
    /// <remarks>
    /// The timestamp is formatted without reference to <see cref="DateTimeKind"/>. SQLite
    /// hands back an Unspecified kind, and anything that converted on the way would produce
    /// a different hash after a round trip than it did before one.
    /// <para>
    /// <see cref="AuditEntry.Sequence"/> is excluded because the store assigns it after the
    /// entry is sealed. Order is still protected: verification walks in sequence order and
    /// checks each entry points at the one before it.
    /// </para>
    /// </remarks>
    private static string Canonicalise(AuditEntry entry) => string.Join(
        FieldSeparator,
        entry.OccurredAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        entry.UserName,
        ((int)entry.Action).ToString(CultureInfo.InvariantCulture),
        entry.Summary,
        entry.Reason,
        entry.PreviousHash);
}
