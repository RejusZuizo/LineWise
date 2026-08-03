using Linewise.Application.Rostering;
using Linewise.Domain.Entities;

namespace Linewise.Application.Persistence;

/// <summary>
/// Everything the manager configures: who exists, what the lines are, and the rules tying
/// them together.
/// </summary>
public interface IConfigurationRepository
{
    /// <summary>
    /// The whole configuration in one read, which is what the engine wants and what the
    /// validator checks.
    /// </summary>
    Task<RosterConfiguration> GetAsync(CancellationToken cancellationToken = default);

    Task SaveEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);

    Task SaveLineAsync(ProductionLine line, CancellationToken cancellationToken = default);

    Task SaveSkillAsync(Skill skill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces this employee's preferences wholesale. A ranked list is edited as a list,
    /// not one row at a time, and replacing it avoids leaving orphaned ranks behind.
    /// </summary>
    Task ReplacePreferencesAsync(
        Guid employeeId,
        IReadOnlyList<LinePreference> preferences,
        CancellationToken cancellationToken = default);

    Task ReplaceLeaderEligibilityAsync(
        Guid employeeId,
        IReadOnlyList<LeaderEligibility> eligibilities,
        CancellationToken cancellationToken = default);
}
