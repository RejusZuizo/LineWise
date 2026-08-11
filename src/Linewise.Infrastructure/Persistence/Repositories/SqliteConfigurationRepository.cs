using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Linewise.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IConfigurationRepository"/>
public sealed class SqliteConfigurationRepository : IConfigurationRepository
{
    private readonly LinewiseDbContext _context;

    public SqliteConfigurationRepository(LinewiseDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>
    /// Reads the whole configuration in one go. At the sizing target of 150 employees and 8
    /// lines this is a few hundred rows, and the engine wants all of it anyway, so there is
    /// nothing to be gained by fetching it in pieces.
    /// </summary>
    public async Task<RosterConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        var employees = await _context.Employees.AsNoTracking()
            .OrderBy(employee => employee.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var lines = await _context.Lines.AsNoTracking()
            .OrderBy(line => line.DisplayOrder).ToListAsync(cancellationToken).ConfigureAwait(false);
        var skills = await _context.Skills.AsNoTracking()
            .OrderBy(skill => skill.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        var preferences = await _context.Preferences.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var eligibilities = await _context.LeaderEligibilities.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var assistantEligibilities = await _context.OperatingAssistantEligibilities.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var aliases = await _context.Set<EmployeeAliasRow>().AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var employeeSkills = await _context.Set<EmployeeSkillRow>().AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var lineSkills = await _context.Set<LineSkillRow>().AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var aliasesByEmployee = aliases
            .GroupBy(alias => alias.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(alias => alias.Alias)
                    .OrderBy(alias => alias, StringComparer.Ordinal)
                    .ToList());

        var skillsByEmployee = employeeSkills
            .GroupBy(skill => skill.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<Guid>)group.Select(skill => skill.SkillId).ToHashSet());

        var skillsByLine = lineSkills
            .GroupBy(skill => skill.LineId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<Guid>)group.Select(skill => skill.SkillId).ToHashSet());

        return new RosterConfiguration
        {
            Employees = employees
                .Select(employee => employee with
                {
                    Aliases = aliasesByEmployee.GetValueOrDefault(employee.Id, []),
                    SkillIds = skillsByEmployee.GetValueOrDefault(employee.Id, new HashSet<Guid>()),
                })
                .ToList(),
            Lines = lines
                .Select(line => line with
                {
                    RequiredSkillIds = skillsByLine.GetValueOrDefault(line.Id, new HashSet<Guid>()),
                })
                .ToList(),
            Skills = skills,
            Preferences = preferences,
            LeaderEligibilities = eligibilities,
            OperatingAssistantEligibilities = assistantEligibilities,
        };
    }

    public async Task SaveEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employee);

        await UpsertAsync(_context.Employees, employee, employee.Id, cancellationToken).ConfigureAwait(false);

        await _context.Set<EmployeeAliasRow>()
            .Where(alias => alias.EmployeeId == employee.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await _context.Set<EmployeeSkillRow>()
            .Where(skill => skill.EmployeeId == employee.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        foreach (var alias in employee.Aliases.Distinct(StringComparer.Ordinal))
        {
            _context.Set<EmployeeAliasRow>().Add(new EmployeeAliasRow
            {
                EmployeeId = employee.Id,
                Alias = alias,
            });
        }

        foreach (var skillId in employee.SkillIds)
        {
            _context.Set<EmployeeSkillRow>().Add(new EmployeeSkillRow
            {
                EmployeeId = employee.Id,
                SkillId = skillId,
            });
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveLineAsync(ProductionLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        await UpsertAsync(_context.Lines, line, line.Id, cancellationToken).ConfigureAwait(false);

        await _context.Set<LineSkillRow>()
            .Where(skill => skill.LineId == line.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        foreach (var skillId in line.RequiredSkillIds)
        {
            _context.Set<LineSkillRow>().Add(new LineSkillRow
            {
                LineId = line.Id,
                SkillId = skillId,
            });
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveSkillAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        await UpsertAsync(_context.Skills, skill, skill.Id, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplacePreferencesAsync(
        Guid employeeId,
        IReadOnlyList<LinePreference> preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        await _context.Preferences
            .Where(preference => preference.EmployeeId == employeeId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        _context.Preferences.AddRange(preferences);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceLeaderEligibilityAsync(
        Guid employeeId,
        IReadOnlyList<LeaderEligibility> eligibilities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eligibilities);

        await _context.LeaderEligibilities
            .Where(eligibility => eligibility.EmployeeId == employeeId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        _context.LeaderEligibilities.AddRange(eligibilities);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceOperatingAssistantEligibilityAsync(
        Guid employeeId,
        IReadOnlyList<OperatingAssistantEligibility> eligibilities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eligibilities);

        await _context.OperatingAssistantEligibilities
            .Where(eligibility => eligibility.EmployeeId == employeeId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        _context.OperatingAssistantEligibilities.AddRange(eligibilities);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// There is one set of print settings, and the first row is it. A table rather than a
    /// configuration file so the settings travel with a backup.
    /// </summary>
    public async Task<PrintSettings> GetPrintSettingsAsync(CancellationToken cancellationToken = default) =>
        await _context.PrintSettings
            .AsNoTracking()
            .OrderBy(settings => settings.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
        ?? new PrintSettings { Id = Guid.Empty };

    public async Task SavePrintSettingsAsync(
        PrintSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var stored = settings.Id == Guid.Empty
            ? settings with { Id = Guid.NewGuid() }
            : settings;

        await UpsertAsync(_context.PrintSettings, stored, stored.Id, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts or overwrites by key. The domain records are immutable, so an update is a
    /// matter of pushing the new values onto the tracked entry rather than assigning to
    /// properties that have no setters.
    /// </summary>
    private async Task UpsertAsync<TEntity>(
        DbSet<TEntity> set,
        TEntity entity,
        Guid id,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var existing = await set.FindAsync([id], cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            set.Add(entity);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }
    }
}
