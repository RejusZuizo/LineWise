using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence;

/// <summary>
/// Tells the change tracker about rows deleted behind its back.
/// </summary>
/// <remarks>
/// <c>ExecuteDelete</c> issues a DELETE straight to the database and does not touch the
/// change tracker. Anything the context is still holding from an earlier call therefore
/// survives as a tracked entity for a row that no longer exists, and adding a replacement
/// with the same key throws: "another instance with the same key value is already being
/// tracked".
/// <para>
/// This surfaced as "I cannot save rules sometimes". Sometimes meant the second time: the
/// first save added and tracked the preferences, and every save after it collided with what
/// the first one left behind. Reads are all <c>AsNoTracking</c>, so the tracked copies came
/// from the previous write rather than from a query.
/// </para>
/// <para>
/// Every replace in this layer is a delete followed by an insert of the same keys, so every
/// one of them needs this. It is one method rather than nine copies for the same reason the
/// eligibility rules are one method: nine copies is eight chances to forget.
/// </para>
/// </remarks>
internal static class TrackedEntities
{
    public static void Forget<TEntity>(this DbContext context, Func<TEntity, bool> deleted)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(deleted);

        // Materialised before detaching. Changing entry state while enumerating the
        // tracker's own collection is how this fix would become its own bug.
        var stale = context.ChangeTracker
            .Entries<TEntity>()
            .Where(entry => deleted(entry.Entity))
            .ToList();

        foreach (var entry in stale)
        {
            entry.State = EntityState.Detached;
        }
    }
}
