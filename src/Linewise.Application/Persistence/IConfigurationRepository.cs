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

    /// <summary>Which lines this employee may be an operating assistant on.</summary>
    Task ReplaceOperatingAssistantEligibilityAsync(
        Guid employeeId,
        IReadOnlyList<OperatingAssistantEligibility> eligibilities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How the printed roster should look. Returns sensible defaults when nothing has been
    /// configured, so a first run can print before anybody has been near a settings screen.
    /// </summary>
    Task<PrintSettings> GetPrintSettingsAsync(CancellationToken cancellationToken = default);

    Task SavePrintSettingsAsync(PrintSettings settings, CancellationToken cancellationToken = default);
}
