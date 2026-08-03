using Linewise.Application;
using Linewise.Application.Abstractions;
using Linewise.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Linewise.Tests.Persistence;

/// <summary>
/// A real encrypted database in a temporary folder, wired through the real container.
/// </summary>
/// <remarks>
/// Deliberately not an in-memory provider. Everything worth testing here, the encryption,
/// the migrations, VACUUM INTO, is a property of the file on disk, and an in-memory
/// provider would prove none of it.
/// <para>
/// The key comes from a fixed test provider rather than DPAPI, so a test run never touches
/// the developer's own protected store.
/// </para>
/// </remarks>
internal sealed class TemporaryDatabase : IAsyncDisposable
{
    public const string Key = "not-a-real-key-only-for-tests";

    private TemporaryDatabase(string root, ServiceProvider services, TestClock clock)
    {
        Root = root;
        Services = services;
        Clock = clock;
    }

    public string Root { get; }

    public ServiceProvider Services { get; }

    public TestClock Clock { get; }

    public string DatabasePath => Path.Combine(Root, "linewise.db");

    public string BackupDirectory => Path.Combine(Root, "backups");

    public static async Task<TemporaryDatabase> CreateAsync(bool backupOnStartup = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "linewise-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var clock = new TestClock();

        var services = new ServiceCollection();

        // Registered before the infrastructure, which uses TryAdd, so these win.
        services.AddSingleton<IDatabaseKeyProvider>(new FixedDatabaseKeyProvider(Key));
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<ICurrentUser>(new TestCurrentUser("test.operator"));

        services.AddLinewiseApplication();
        services.AddLinewiseInfrastructure(options =>
        {
            options.DatabasePath = Path.Combine(root, "linewise.db");
            options.BackupDirectory = Path.Combine(root, "backups");
            options.BackupOnStartup = backupOnStartup;
            options.BackupsToKeep = 10;
        });

        var database = new TemporaryDatabase(root, services.BuildServiceProvider(), clock);
        await database.InitialiseAsync().ConfigureAwait(false);

        return database;
    }

    /// <summary>Runs the startup path: back up if there is anything to back up, then migrate.</summary>
    public async Task InitialiseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<DatabaseInitialiser>()
            .InitialiseAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a service in its own scope, the way a use case would. Repositories are
    /// scoped because the context they share is, and reusing one across a whole test would
    /// let the change tracker paper over a round trip that does not really work.
    /// </summary>
    public async Task<TResult> InScopeAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>()).ConfigureAwait(false);
    }

    public async Task InScopeAsync<TService>(Func<TService, Task> action)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>()).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync().ConfigureAwait(false);

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A file handle outliving the test is not a test failure. The temp folder gets
            // cleaned up by the operating system eventually.
        }
    }
}
