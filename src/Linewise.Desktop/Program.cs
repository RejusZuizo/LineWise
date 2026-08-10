using Avalonia;
using Linewise.Application;
using Linewise.Infrastructure;
using Linewise.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Linewise.Desktop;

internal static class Program
{
    /// <summary>
    /// Composition happens here and nowhere else. Every service the application uses is
    /// resolved from this container, so a view model never constructs one for itself.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static int Main(string[] args)
    {
        Log.Logger = LinewiseLogging.Create();

        // Registered before anything can throw on a background thread, and before the
        // window exists. An unhandled exception during startup is exactly the one a user
        // would otherwise see as a silent failure to launch.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception. The application is closing.");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "A task faulted and nobody looked at the result.");
            e.SetObserved();
        };

        try
        {
            Log.Information("Linewise starting. Version {Version}.", Version);

            Services = new ServiceCollection()
                .AddLinewiseApplication()
                .AddLinewiseInfrastructure()
                .AddDesktop()
                .BuildServiceProvider();

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            // Nothing above this line has a window to show a dialog in. The log is the only
            // record a user can be asked for, which is why it is opened first.
            Log.Fatal(exception, "Linewise failed to start.");
            return 1;
        }
        finally
        {
            Log.Information("Linewise closing.");
            Log.CloseAndFlush();
        }
    }

    /// <summary>Used by the Avalonia designer as well as by <see cref="Main"/>.</summary>
    /// <remarks>
    /// No embedded font. The design document's rule is the system font and no imported
    /// webfonts, and on the machine this ships to that means the font every other
    /// application on the manager's desktop is already using.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static string Version =>
        typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion
        ?? "unknown";
}
