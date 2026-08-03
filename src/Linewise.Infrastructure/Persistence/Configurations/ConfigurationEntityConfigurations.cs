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
        builder.HasIndex(availability => availability.Date);
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
