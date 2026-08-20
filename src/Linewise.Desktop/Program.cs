using Avalonia;
using Linewise.Application;
using Linewise.Infrastructure;
using Linewise.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Velopack;

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
        // Before everything, including the logger.
        //
        // On an installed build this call is how the application handles being installed,
        // updated and uninstalled: the installer runs the new executable with a hook
        // argument, and this is what answers it and exits. Anything done before it — opening
        // a log file, building a container, touching the database — happens during a hook
        // that is not supposed to be running an application at all, and on an update that
        // means writing to a folder that is about to be replaced.
        //
        // It does nothing at all when the application is run normally or from source, which
        // is why it is safe to have it here on a build nobody has installed yet.
        VelopackApp.Build().Run();

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

            InitialiseDatabase();

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

    /// <summary>
    /// Backs up and migrates before the window opens.
    /// </summary>
    /// <remarks>
    /// Blocking, and deliberately so. Every screen in this application reads from the
    /// database, so a window shown before the schema is ready is a window whose first act
    /// is to fail. On a first run this creates the file; afterwards it is a version check
    /// and a copy, which is well inside the cold start target.
    /// <para>
    /// Missing entirely until the grid was run against a real empty database, which is the
    /// argument for running the thing rather than only testing it: every test that touches
    /// the database calls this itself, so nothing noticed that the application did not.
    /// </para>
    /// </remarks>
    private static void InitialiseDatabase()
    {
        using var scope = Services.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<DatabaseInitialiser>()
            .InitialiseAsync()
            .GetAwaiter()
            .GetResult();

        Log.Information("Database ready.");
    }

    /// <summary>Used by the Avalonia designer as well as by <see cref="Main"/>.</summary>
    /// <remarks>
    /// Inter, embedded in the application rather than fetched from anywhere. ADR 0013
    /// records why this reverses the design document's system font rule: a roster grid is
    /// dense numerals and short names at small sizes, and the system font is whatever
    /// Windows happens to be set to.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static string Version =>
        typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion
        ?? "unknown";
}
