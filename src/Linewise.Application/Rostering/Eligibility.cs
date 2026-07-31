using Linewise.Domain.Entities;

namespace Linewise.Application.Rostering;

/// <summary>
/// The hard filter. Shared by the engine and the validator so the two can never disagree
/// about who is allowed on a line.
/// </summary>
internal static class Eligibility
{
    public static bool HasRequiredSkills(Employee employee, ProductionLine line) =>
        line.RequiredSkillIds.Count == 0 || line.RequiredSkillIds.All(employee.SkillIds.Contains);
}
