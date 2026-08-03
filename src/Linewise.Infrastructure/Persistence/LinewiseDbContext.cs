using Linewise.Domain.Auditing;
using Linewise.Domain.Entities;
using Linewise.Domain.Rostering;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence;

/// <summary>
/// The one context. Entity configuration lives in explicit configuration classes rather
/// than attributes, so the domain records carry no persistence concerns at all.
/// </summary>
public sealed class LinewiseDbContext : DbContext
{
    public LinewiseDbContext(DbContextOptions<LinewiseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<ProductionLine> Lines => Set<ProductionLine>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<Availability> Availabilities => Set<Availability>();

    public DbSet<LinePreference> Preferences => Set<LinePreference>();

    public DbSet<LeaderEligibility> LeaderEligibilities => Set<LeaderEligibility>();

    public DbSet<RosterVersion> RosterVersions => Set<RosterVersion>();

    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<RosterWarning> RosterWarnings => Set<RosterWarning>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditLog();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        GuardAuditLog();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LinewiseDbContext).Assembly);
    }

    /// <summary>
    /// The audit log is append only, and this is where that stops being a convention and
    /// starts being enforced. Throwing is right here: reaching this point is a bug in the
    /// calling code, not something an operator did wrong.
    /// </summary>
    private void GuardAuditLog()
    {
        foreach (var entry in ChangeTracker.Entries<AuditEntry>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "The audit log is append only. Entries cannot be updated or deleted.");
            }
        }
    }
}
