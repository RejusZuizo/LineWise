using Linewise.Desktop.Services;
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

        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        // Transient: a dialog opened twice should be a fresh window with freshly loaded
        // lines, not the one that was closed with half an edit in it.
        services.AddTransient<LineEditorViewModel>();
        services.AddTransient<LineEditorWindow>();

        services.AddSingleton<IFilePicker, FilePicker>();
        services.AddTransient<ImportViewModel>();
        services.AddTransient<ImportWindow>();

        return services;
    }
}
