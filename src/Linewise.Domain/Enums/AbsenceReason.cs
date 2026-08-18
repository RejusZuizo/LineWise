namespace Linewise.Domain.Enums;

/// <summary>
/// Why the roster changed when somebody was marked absent. An operational vocabulary,
/// deliberately: these describe what happened to the roster, never what happened to the
/// person.
/// </summary>
/// <remarks>
/// The data protection position is that no reason for an absence beyond the status is
/// held — "Holiday" is recorded, "hospital appointment" is not — and it calls that the
/// single most important minimisation decision in the design, because a reason for
/// absence drags health data into the application.
/// <para>
/// A picker offering "Sick" would have written special category data about a named
/// employee into the audit chain, which is append only and has no delete path, so it could
/// never be corrected or erased afterwards. These words answer the audit's question
/// without answering that one.
/// </para>
/// <para>
/// There is deliberately no free text option. It would need a modal, and it would be the
/// one place somebody could type the reason the rest of this exists to avoid holding.
/// </para>
/// </remarks>
public enum AbsenceReason
{
    /// <summary>Not in, and the roster needs covering. The ordinary Monday morning case.</summary>
    NotInToday = 0,

    /// <summary>Booked off. The one absence the sheet itself already records.</summary>
    Holiday = 1,

    /// <summary>Started the shift and left it. The manager's action, not the cause.</summary>
    SentHome = 2,
}
