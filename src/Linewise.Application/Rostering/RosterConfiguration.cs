using Linewise.Domain.Entities;

namespace Linewise.Application.Rostering;

/// <summary>
/// Everything the manager has configured: who exists, what the lines are, and the rules
/// tying them together. Free of dates, so it can be validated on its own before any week
/// is generated.
/// </summary>
public sealed record RosterConfiguration
{
    public IReadOnlyList<Employee> Employees { get; init; } = [];

    public IReadOnlyList<ProductionLine> Lines { get; init; } = [];

    public IReadOnlyList<Skill> Skills { get; init; } = [];

    public IReadOnlyList<LinePreference> Preferences { get; init; } = [];

    public IReadOnlyList<LeaderEligibility> LeaderEligibilities { get; init; } = [];

    public IReadOnlyList<OperatingAssistantEligibility> OperatingAssistantEligibilities { get; init; } = [];
}
