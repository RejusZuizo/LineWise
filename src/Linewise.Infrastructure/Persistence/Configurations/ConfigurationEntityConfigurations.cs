using Linewise.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Linewise.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.FullName).IsRequired().HasMaxLength(200);
        builder.Property(employee => employee.IsActive);
        builder.Property(employee => employee.IsTemporary);

        // Kept as tables of their own rather than as lists hidden in a column.
        builder.Ignore(employee => employee.Aliases);
        builder.Ignore(employee => employee.SkillIds);
    }
}

internal sealed class ProductionLineConfiguration : IEntityTypeConfiguration<ProductionLine>
{
    public void Configure(EntityTypeBuilder<ProductionLine> builder)
    {
        builder.ToTable("Lines");
        builder.HasKey(line => line.Id);

        builder.Property(line => line.Name).IsRequired().HasMaxLength(100);
        builder.Property(line => line.DisplayOrder);
        builder.Property(line => line.RequiredHeadcount);
        builder.Property(line => line.AccentColour).IsRequired().HasMaxLength(9);

        builder.Ignore(line => line.RequiredSkillIds);
    }
}

internal sealed class PrintSettingsConfiguration : IEntityTypeConfiguration<PrintSettings>
{
    public void Configure(EntityTypeBuilder<PrintSettings> builder)
    {
        builder.ToTable("PrintSettings");
        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(settings => settings.LogoPng);
        builder.Property(settings => settings.PaperSize);
        builder.Property(settings => settings.Orientation);
        builder.Property(settings => settings.BaseFontPoints);
        builder.Property(settings => settings.UseAccentColours);
    }
}

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Name).IsRequired().HasMaxLength(100);
    }
}

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");
        builder.HasKey(shift => shift.Id);

        builder.Property(shift => shift.Date);
        builder.Property(shift => shift.Name);

        builder.HasIndex(shift => new { shift.Date, shift.Name }).IsUnique();
    }
}

internal sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> builder)
    {
        builder.ToTable("Availabilities");
        builder.HasKey(availability => new { availability.EmployeeId, availability.Date });

        builder.Property(availability => availability.Status);
        builder.Property(availability => availability.Source);
        builder.HasIndex(availability => availability.Date);

        // An import deletes by date and source together, and marking somebody absent reads
        // one person's day. Both go through the date index; this one keeps the import's
        // delete from reading every row in the range to find out which are its own.
        builder.HasIndex(availability => new { availability.Date, availability.Source });
    }
}

internal sealed class LineDemandConfiguration : IEntityTypeConfiguration<LineDemand>
{
    public void Configure(EntityTypeBuilder<LineDemand> builder)
    {
        builder.ToTable("LineDemands");

        // One number per line per day. The key makes a contradictory second entry impossible
        // rather than merely unlikely.
        builder.HasKey(demand => new { demand.LineId, demand.Date });

        builder.Property(demand => demand.RequiredHeadcount);
        builder.Property(demand => demand.IsClosed);
        builder.HasIndex(demand => demand.Date);

        builder.HasOne<ProductionLine>()
            .WithMany()
            .HasForeignKey(demand => demand.LineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LinePreferenceConfiguration : IEntityTypeConfiguration<LinePreference>
{
    public void Configure(EntityTypeBuilder<LinePreference> builder)
    {
        builder.ToTable("LinePreferences");
        builder.HasKey(preference => new { preference.EmployeeId, preference.LineId });

        builder.Property(preference => preference.Rank);
        builder.Property(preference => preference.Type);
    }
}

internal sealed class LeaderEligibilityConfiguration : IEntityTypeConfiguration<LeaderEligibility>
{
    public void Configure(EntityTypeBuilder<LeaderEligibility> builder)
    {
        builder.ToTable("LeaderEligibilities");
        builder.HasKey(eligibility => new { eligibility.EmployeeId, eligibility.LineId });
    }
}

internal sealed class OperatingAssistantEligibilityConfiguration
    : IEntityTypeConfiguration<OperatingAssistantEligibility>
{
    public void Configure(EntityTypeBuilder<OperatingAssistantEligibility> builder)
    {
        builder.ToTable("OperatingAssistantEligibilities");
        builder.HasKey(eligibility => new { eligibility.EmployeeId, eligibility.LineId });
    }
}

internal sealed class EmployeeAliasConfiguration : IEntityTypeConfiguration<EmployeeAliasRow>
{
    public void Configure(EntityTypeBuilder<EmployeeAliasRow> builder)
    {
        builder.ToTable("EmployeeAliases");
        builder.HasKey(alias => new { alias.EmployeeId, alias.Alias });
        builder.Property(alias => alias.Alias).HasMaxLength(200);
    }
}

internal sealed class EmployeeSkillConfiguration : IEntityTypeConfiguration<EmployeeSkillRow>
{
    public void Configure(EntityTypeBuilder<EmployeeSkillRow> builder)
    {
        builder.ToTable("EmployeeSkills");
        builder.HasKey(skill => new { skill.EmployeeId, skill.SkillId });
    }
}

internal sealed class LineSkillConfiguration : IEntityTypeConfiguration<LineSkillRow>
{
    public void Configure(EntityTypeBuilder<LineSkillRow> builder)
    {
        builder.ToTable("LineRequiredSkills");
        builder.HasKey(skill => new { skill.LineId, skill.SkillId });
    }
}
