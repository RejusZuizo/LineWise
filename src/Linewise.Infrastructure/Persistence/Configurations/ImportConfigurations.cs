using Linewise.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Linewise.Infrastructure.Persistence.Configurations;

internal sealed class ImportTemplateConfiguration : IEntityTypeConfiguration<ImportTemplate>
{
    public void Configure(EntityTypeBuilder<ImportTemplate> builder)
    {
        builder.ToTable("ImportTemplates");
        builder.HasKey(template => template.Id);

        builder.Property(template => template.Name).IsRequired().HasMaxLength(100);
        builder.Property(template => template.WorksheetName).HasMaxLength(100);
        builder.Property(template => template.HeaderRowIndex);
        builder.Property(template => template.NameColumnIndex);
        builder.Property(template => template.FirstDateColumnIndex);
        builder.Property(template => template.DateFormat).HasMaxLength(40);
        builder.Property(template => template.EmptyCellStatus);
        builder.Property(template => template.ReportUnrecognisedCells);

        // Rules are a table of their own, composed back by the repository. Same reasoning as
        // the collections on Employee: the domain record stays a record, and the database
        // stays queryable.
        builder.Ignore(template => template.StatusRules);
    }
}

internal sealed class ImportTemplateRuleConfiguration : IEntityTypeConfiguration<ImportTemplateRuleRow>
{
    public void Configure(EntityTypeBuilder<ImportTemplateRuleRow> builder)
    {
        builder.ToTable("ImportTemplateRules");
        builder.HasKey(rule => new { rule.ImportTemplateId, rule.Ordinal });

        builder.Property(rule => rule.Kind);
        builder.Property(rule => rule.Value).IsRequired().HasMaxLength(100);
        builder.Property(rule => rule.Status);

        builder.HasOne<ImportTemplate>()
            .WithMany()
            .HasForeignKey(rule => rule.ImportTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CommittedImportConfiguration : IEntityTypeConfiguration<CommittedImport>
{
    public void Configure(EntityTypeBuilder<CommittedImport> builder)
    {
        builder.ToTable("CommittedImports");
        builder.HasKey(import => import.Id);

        builder.Property(import => import.FileName).IsRequired().HasMaxLength(260);
        builder.Property(import => import.ImportedAtUtc).HasConversion(UtcDateTime.Converter);
        builder.Property(import => import.ImportedBy).IsRequired().HasMaxLength(200);
        builder.Property(import => import.FirstDate);
        builder.Property(import => import.EndDateExclusive);
        builder.Property(import => import.RowsImported);
        builder.Property(import => import.RowsResolvedManually);

        builder.HasIndex(import => import.ImportedAtUtc);
    }
}

internal sealed class ImportFileConfiguration : IEntityTypeConfiguration<ImportFileRow>
{
    public void Configure(EntityTypeBuilder<ImportFileRow> builder)
    {
        builder.ToTable("ImportFiles");
        builder.HasKey(file => file.CommittedImportId);

        builder.Property(file => file.Content).IsRequired();

        builder.HasOne<CommittedImport>()
            .WithMany()
            .HasForeignKey(file => file.CommittedImportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
