using Linewise.Application;
using Linewise.Application.Abstractions;
using Linewise.Desktop;
using Linewise.Desktop.ViewModels;
using Linewise.Infrastructure;
using Linewise.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>
/// The container can build the window the application opens with.
/// </summary>
/// <remarks>
/// A service that nothing registers is invisible to every other test in this suite. The view
/// models are constructed by hand in their own tests, precisely so they need no container,
/// and the engine and repository tests resolve one service at a time. Nothing until now
/// asked the question the application asks at startup: can this whole graph be built.
/// <para>
/// This is the fourth defect on this project that a green suite could not see, after an
/// uninitialised database, a week with no shifts and a duplicate enum value. It is the
/// cheapest of them to have caught.
/// </para>
/// </remarks>
public sealed class ContainerTests
{
    [Fact]
    public void The_main_window_view_model_can_be_built()
    {
        using var services = Container();

        var built = services.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(built);

        // Resolving is not enough. This class carries a parameterless constructor for the
        // designer, and the container will quietly choose it when it cannot satisfy the
        // real one — producing a window whose every dependency is null and whose every
        // command silently does nothing. The fields are what prove which constructor ran.
        Assert.Empty(UnsatisfiedDependencies(built));
    }

    /// <summary>
    /// The other screens, resolved in a scope the way a dialog is opened.
    /// </summary>
    [Theory]
    [InlineData(typeof(PeopleViewModel))]
    [InlineData(typeof(LineEditorViewModel))]
    [InlineData(typeof(ImportViewModel))]
    public void Every_screen_can_be_built(Type viewModel)
    {
        using var services = Container();
        using var scope = services.CreateScope();

        var built = scope.ServiceProvider.GetRequiredService(viewModel);

        Assert.NotNull(built);
        Assert.Empty(UnsatisfiedDependencies(built));
    }

    /// <summary>
    /// Interface fields left null, which is how a view model looks when the container could
    /// not satisfy its real constructor and quietly chose the designer's instead.
    /// </summary>
    private static IReadOnlyList<string> UnsatisfiedDependencies(object viewModel) =>
    [
        .. viewModel.GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Where(field => field.FieldType.IsInterface)
            .Where(field => field.GetValue(viewModel) is null)
            .Select(field => field.FieldType.Name),
    ];

    private static ServiceProvider Container()
    {
        var root = Path.Combine(Path.GetTempPath(), "linewise-container", Guid.NewGuid().ToString("N"));

        var services = new ServiceCollection();

        // The real registrations, in the order the application applies them. Only the key
        // provider is substituted, so a test run never touches a protected store.
        services.AddSingleton<IDatabaseKeyProvider>(new FixedDatabaseKeyProvider(TemporaryDatabase.Key));
        services.AddLinewiseApplication();
        services.AddLinewiseInfrastructure(options =>
        {
            options.DatabasePath = Path.Combine(root, "linewise.db");
            options.BackupDirectory = Path.Combine(root, "backups");
            options.BackupOnStartup = false;
        });
        services.AddDesktop();

        // Built the way the application builds it. Strict scope validation is deliberately
        // off: the main window is a singleton holding scoped repositories, which is a real
        // captive dependency and a real thing to fix, but it is not what this test is
        // guarding and turning it on here would make every registration failure look like
        // that one problem.
        return services.BuildServiceProvider();
    }
}
