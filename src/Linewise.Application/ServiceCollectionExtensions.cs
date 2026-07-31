using Linewise.Application.Rostering;
using Linewise.Application.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Linewise.Application;

/// <summary>
/// Registers the application services. Everything is behind an interface and resolved from
/// the container; nothing is ever constructed by hand further up the stack.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLinewiseApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd, so a host that has already chosen seniority over fairness keeps its choice.
        services.TryAddSingleton<ITieBreakStrategy, FairnessTieBreakStrategy>();

        // Both are stateless and hold nothing between calls, so a singleton is safe.
        services.TryAddSingleton<IAssignmentEngine, AssignmentEngine>();
        services.TryAddSingleton<IRosterRuleValidator, RosterRuleValidator>();

        return services;
    }
}
