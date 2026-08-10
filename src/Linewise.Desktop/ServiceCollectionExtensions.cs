using Linewise.Desktop.ViewModels;
using Linewise.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Linewise.Desktop;

/// <summary>
/// Registers the windows and view models. Views take their view model through the
/// constructor, so nothing in this layer ever news up a dependency.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
