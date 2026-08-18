using Linewise.Application.Import;
using Linewise.Application.Printing;
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

        // All stateless and holding nothing between calls, so a singleton is safe.
        services.TryAddSingleton<IAssignmentEngine, AssignmentEngine>();
        services.TryAddSingleton<IRosterRuleValidator, RosterRuleValidator>();
        services.TryAddSingleton<IAvailabilityImportBuilder, AvailabilityImportBuilder>();
        services.TryAddSingleton<IImportLayoutDetector, ImportLayoutDetector>();

        // Scoped, because these reach repositories that share a database context.
        services.TryAddScoped<IAvailabilityImportService, AvailabilityImportService>();
        services.TryAddScoped<IEmployeeSheetImporter, EmployeeSheetImporter>();
        services.TryAddScoped<IRosterGenerationService, RosterGenerationService>();
        services.TryAddScoped<IAbsenceService, AbsenceService>();
        services.TryAddScoped<ILineDayService, LineDayService>();
        services.TryAddScoped<IReplacementFinder, ReplacementFinder>();
        services.TryAddScoped<IGapFiller, GapFiller>();
        services.TryAddScoped<IPublishingService, PublishingService>();

        return services;
    }
}
