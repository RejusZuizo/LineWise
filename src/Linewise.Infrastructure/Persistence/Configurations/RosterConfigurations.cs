using Linewise.Domain.Auditing;
using Linewise.Domain.Entities;
using Linewise.Domain.Rostering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Linewise.Infrastructure.Persistence.Configurations;

internal sealed class RosterVersionConfiguration : IEntityTypeConfiguration<RosterVersion>
{
    public void Configure(EntityTypeBuilder<RosterVersion> builder)
    {
        builder.ToTable("RosterVersions");
        builder.HasKey(version => version.Id);

        builder.Property(version => version.WeekStart);
        builder.Property(version => version.VersionNumber);
        builder.Property(version => version.Status);
        builder.Property(version => version.CreatedAtUtc).HasConversion(UtcDateTime.Converter);
        builder.Property(version => version.PublishedAtUtc).HasConversion(UtcDateTime.NullableConverter);
        builder.Property(version => version.PublishedBy).HasMaxLength(200);

        builder.HasIndex(version => new { version.WeekStart, version.VersionNumber }).IsUnique();
    }
}

/// <summary>
/// Assignments hang off a roster version through a shadow foreign key.
/// </summary>
/// <remarks>
/// The domain <see cref="Assignment"/> is what the engine produces and what the tests
/// compare, so it carries no identifier and no parent reference. Putting both in shadow
/// state keeps the record clean and the table properly related. The repository sets the
/// foreign key explicitly on insert.
/// </remarks>
internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments");

        builder.Property<long>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property<Guid>("RosterVersionId");
        builder.HasOne<RosterVersion>()
            .WithMany()
            .HasForeignKey("RosterVersionId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(assignment => assignment.Date);
        builder.Property(assignment => assignment.ShiftId);
        builder.Property(assignment => assignment.LineId);
        builder.Property(assignment => assignment.EmployeeId);
        builder.Property(assignment => assignment.Role);
        builder.Property(assignment => assignment.IsLocked);
        builder.Property(assignment => assignment.Source);

        // Why the placement was made, stored alongside it. Without this the manager cannot
        // be told why somebody is on a line once the roster has been reloaded.
        builder.OwnsOne(assignment => assignment.Explanation, explanation =>
        {
            explanation.Property(value => value.Rule).HasColumnName("PlacementRule");
            explanation.Property(value => value.PreferenceRank).HasColumnName("PreferenceRank");
        });

        builder.Navigation(assignment => assignment.Explanation).IsRequired();

        builder.HasIndex("RosterVersionId");
        builder.HasIndex(assignment => assignment.Date);
        builder.HasIndex(assignment => assignment.EmployeeId);
    }
}

internal sealed class RosterWarningConfiguration : IEntityTypeConfiguration<RosterWarning>
{
    public void Configure(EntityTypeBuilder<RosterWarning> builder)
    {
        builder.ToTable("RosterWarnings");

        builder.Property<long>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property<Guid>("RosterVersionId");
        builder.HasOne<RosterVersion>()
            .WithMany()
            .HasForeignKey("RosterVersionId")
            .OnDelete(DeleteBehavior.Cascade);

        // The shift the warning belongs to. A warning is raised per date and shift, and
        // without this a reloaded roster could not put it back on the right day.
        builder.Property<Guid>("ShiftId");

        builder.Property(warning => warning.Severity);
        builder.Property(warning => warning.Code);
        builder.Property(warning => warning.Message).IsRequired();
        builder.Property(warning => warning.LineId);
        builder.Property(warning => warning.EmployeeId);
        builder.Property(warning => warning.Date);

        builder.HasIndex("RosterVersionId");
    }
}

internal sealed class RosterDayConfiguration : IEntityTypeConfiguration<RosterDayRow>
{
    public void Configure(EntityTypeBuilder<RosterDayRow> builder)
    {
        builder.ToTable("RosterDays");
        builder.HasKey(day => new { day.RosterVersionId, day.Date, day.ShiftId });

        builder.Property(day => day.Ordinal);

        builder.HasOne<RosterVersion>()
            .WithMany()
            .HasForeignKey(day => day.RosterVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");

        builder.HasKey(entry => entry.Sequence);
        builder.Property(entry => entry.Sequence).ValueGeneratedOnAdd();

        builder.Property(entry => entry.OccurredAtUtc).HasConversion(UtcDateTime.Converter);
        builder.Property(entry => entry.UserName).IsRequired().HasMaxLength(200);
        builder.Property(entry => entry.Action);
        builder.Property(entry => entry.Summary).IsRequired().HasMaxLength(1000);
        builder.Property(entry => entry.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(entry => entry.PreviousHash).IsRequired().HasMaxLength(64);
        builder.Property(entry => entry.Hash).IsRequired().HasMaxLength(64);

        // Two entries cannot claim the same predecessor, so the chain cannot fork into two
        // plausible histories. The database enforces this rather than the code hoping for it.
        builder.HasIndex(entry => entry.PreviousHash).IsUnique();
    }
}
