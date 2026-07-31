using Linewise.Application.Rostering;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Validation;

/// <summary>
/// Checks a configuration for combinations of rules that no roster could satisfy.
/// </summary>
/// <remarks>
/// Run before generation. Finding out on Monday morning that a line requires a skill
/// nobody holds is a great deal worse than being told on the day the rule was written.
/// </remarks>
public interface IRosterRuleValidator
{
    IReadOnlyList<RosterWarning> Validate(RosterConfiguration configuration);
}
