using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Linewise.Infrastructure.Logging;

/// <summary>
/// Builds the logger the application runs on.
/// </summary>
/// <remarks>
/// Configured in code rather than from a settings file. A logging configuration that can be
/// edited without a build is a redaction policy that can be removed without a build, and
/// this one is a stated control rather than a convenience. Threat T6.
/// </remarks>
public static class LinewiseLogging
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>Where logs go. Beside the database, never beside the executable.</summary>
    public static string DefaultDirectory => Path.Combine(
        LinewiseDatabaseOptions.DefaultDirectory,
        "logs");

    public static Logger Create(string? directory = null)
    {
        var logDirectory = directory ?? DefaultDirectory;
        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Destructure.With<PersonalDataDestructuringPolicy>()
            .WriteTo.File(
                Path.Combine(logDirectory, "linewise-.log"),
                rollingInterval: RollingInterval.Day,

                // Seven files, not unlimited. Logs of a rostering application describe who
                // was moved where, and keeping a year of that quietly builds a second record
                // of employee movements outside the retention policy that governs the first.
                retainedFileCountLimit: 7,
                outputTemplate: OutputTemplate,
                restrictedToMinimumLevel: LogEventLevel.Information)
            .CreateLogger();
    }
}
