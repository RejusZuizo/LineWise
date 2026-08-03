using Linewise.Application.Import;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IImportRepository"/>
public sealed class SqliteImportRepository : IImportRepository
{
    private readonly LinewiseDbContext _context;

    public SqliteImportRepository(LinewiseDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<ImportTemplate?> GetTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.ImportTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == templateId, cancellationToken)
            .ConfigureAwait(false);

        return template is null
            ? null
            : template with { StatusRules = await ReadRulesAsync(templateId, cancellationToken).ConfigureAwait(false) };
    }

    public async Task<IReadOnlyList<ImportTemplate>> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var templates = await _context.ImportTemplates
            .AsNoTracking()
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rules = await _context.Set<ImportTemplateRuleRow>()
            .AsNoTracking()
            .OrderBy(rule => rule.Ordinal)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byTemplate = rules
            .GroupBy(rule => rule.ImportTemplateId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CellStatusRule>)group.Select(ToRule).ToList());

        return templates
            .Select(template => template with
            {
                StatusRules = byTemplate.GetValueOrDefault(template.Id, []),
            })
            .ToList();
    }

    public async Task SaveTemplateAsync(ImportTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var existing = await _context.ImportTemplates
            .FirstOrDefaultAsync(candidate => candidate.Id == template.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _context.ImportTemplates.Add(template);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(template);
        }

        // Rules are edited as a list, so they are replaced as a list. Patching them row by
        // row leaves orphaned ordinals behind.
        await _context.Set<ImportTemplateRuleRow>()
            .Where(rule => rule.ImportTemplateId == template.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        for (var ordinal = 0; ordinal < template.StatusRules.Count; ordinal++)
        {
            var rule = template.StatusRules[ordinal];

            _context.Set<ImportTemplateRuleRow>().Add(new ImportTemplateRuleRow
            {
                ImportTemplateId = template.Id,
                Ordinal = ordinal,
                Kind = (int)rule.Kind,
                Value = rule.Value,
                Status = (int)rule.Status,
            });
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordAsync(
        CommittedImport import,
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(fileContent);

        _context.CommittedImports.Add(import);

        _context.Set<ImportFileRow>().Add(new ImportFileRow
        {
            CommittedImportId = import.Id,
            Content = fileContent,
        });

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> GetFileAsync(Guid importId, CancellationToken cancellationToken = default) =>
        await _context.Set<ImportFileRow>()
            .AsNoTracking()
            .Where(file => file.CommittedImportId == importId)
            .Select(file => file.Content)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<CommittedImport>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default) =>
        await _context.CommittedImports
            .AsNoTracking()
            .OrderByDescending(import => import.ImportedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<IReadOnlyList<CellStatusRule>> ReadRulesAsync(
        Guid templateId,
        CancellationToken cancellationToken) =>
        (await _context.Set<ImportTemplateRuleRow>()
            .AsNoTracking()
            .Where(rule => rule.ImportTemplateId == templateId)
            .OrderBy(rule => rule.Ordinal)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .Select(ToRule)
        .ToList();

    private static CellStatusRule ToRule(ImportTemplateRuleRow rule) =>
        new((CellMatchKind)rule.Kind, rule.Value, (AvailabilityStatus)rule.Status);
}
