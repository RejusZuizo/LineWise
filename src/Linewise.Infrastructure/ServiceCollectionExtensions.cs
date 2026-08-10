using Linewise.Application.Abstractions;
using Linewise.Application.Import;
using Linewise.Application.Persistence;
using Linewise.Application.Printing;
using Linewise.Infrastructure.Auditing;
using Linewise.Infrastructure.Backup;
using Linewise.Infrastructure.Import;
using Linewise.Infrastructure.Persistence;
using Linewise.Infrastructure.Persistence.Repositories;
using Linewise.Infrastructure.Printing;
using Linewise.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Linewise.Infrastructure;

/// <summary>
/// Registers persistence. Everything is behind an interface declared in the application
/// layer, so nothing above this line knows EF Core exists.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLinewiseInfrastructure(
        this IServiceCollection services,
        Action<LinewiseDatabaseOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Loads the SQLCipher build of SQLite. Exactly one provider may be registered, which
        // is why the project references Sqlite.Core rather than Sqlite.
        SQLitePCL.Batteries_V2.Init();

        // QuestPDF requires the licence to be declared before it will render anything, and
        // it is declared here rather than left to a host to remember. Community is free, and
        // choosing it is an assertion about the licensee's revenue rather than a technical
        // setting: whoever ships this has to have checked that the threshold is met.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        services.AddOptions<LinewiseDatabaseOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<ICurrentUser, EnvironmentCurrentUser>();
        services.TryAddSingleton<IBackupService, SqliteBackupService>();

        // The one genuinely platform specific service. DPAPI is how the key is kept on the
        // machine this ships to; everything else in this assembly runs anywhere.
        //
        // The alternative on a development machine is weaker by construction, so a release
        // build does not carry one: the enforcement is that the registration below is
        // compiled out, not that somebody remembers. See ADR 0011.
        if (OperatingSystem.IsWindows())
        {
            services.TryAddSingleton<IDatabaseKeyProvider, DpapiDatabaseKeyProvider>();
        }
        else
        {
#if DEBUG
            services.TryAddSingleton<IDatabaseKeyProvider, DevelopmentKeyFileProvider>();
#else
            services.TryAddSingleton<IDatabaseKeyProvider, UnsupportedPlatformKeyProvider>();
#endif
        }

        services.AddDbContext<LinewiseDbContext>((provider, builder) =>
        {
            var options = provider.GetRequiredService<IOptions<LinewiseDatabaseOptions>>().Value;
            var databasePath = options.ResolvedDatabasePath;
            var directory = Path.GetDirectoryName(databasePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            builder.UseSqlite(LinewiseConnection.BuildConnectionString(
                databasePath,
                provider.GetRequiredService<IDatabaseKeyProvider>().GetKey()));
        });

        services.TryAddSingleton<IAvailabilitySheetReader, ClosedXmlSheetReader>();
        services.TryAddSingleton<IRosterPrinter, QuestPdfRosterPrinter>();

        services.TryAddScoped<IAuditLog, SqliteAuditLog>();
        services.TryAddScoped<IImportRepository, SqliteImportRepository>();
        services.TryAddScoped<IRosterRepository, SqliteRosterRepository>();
        services.TryAddScoped<IConfigurationRepository, SqliteConfigurationRepository>();
        services.TryAddScoped<IAvailabilityRepository, SqliteAvailabilityRepository>();
        services.TryAddScoped<ILineDemandRepository, SqliteLineDemandRepository>();
        services.TryAddScoped<IShiftRepository, SqliteShiftRepository>();
        services.TryAddScoped<DatabaseInitialiser>();

        return services;
    }
}
