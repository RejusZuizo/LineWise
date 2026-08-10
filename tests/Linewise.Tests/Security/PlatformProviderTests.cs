using Linewise.Application.Abstractions;
using Linewise.Infrastructure;
using Linewise.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Linewise.Tests.Security;

/// <summary>
/// Which key provider the container hands back, and what the refusing one says.
/// </summary>
public sealed class PlatformProviderTests
{
    /// <summary>
    /// The whole point of the release-build provider: a message that answers the question
    /// it raises, rather than "unable to resolve service for type IDatabaseKeyProvider".
    /// </summary>
    [Fact]
    public void The_unsupported_provider_explains_itself()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new UnsupportedPlatformKeyProvider().GetKey());

        Assert.Contains("DPAPI", error.Message, StringComparison.Ordinal);
        Assert.Contains("Debug", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Windows gets the shipped provider. This is the assertion that matters most in the
    /// file, because it is the one that fails if somebody ever makes the development
    /// provider the default.
    /// </summary>
    [Fact]
    public void Windows_gets_the_shipped_provider()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.IsType<DpapiDatabaseKeyProvider>(Resolve<IDatabaseKeyProvider>());
    }

    /// <summary>
    /// A debug build elsewhere gets the development provider; a release build gets the one
    /// that refuses. The condition is compiled rather than checked, so this test is too.
    /// </summary>
    [Fact]
    public void Elsewhere_the_provider_depends_on_the_build()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var provider = Resolve<IDatabaseKeyProvider>();

#if DEBUG
        Assert.IsType<DevelopmentKeyFileProvider>(provider);
#else
        Assert.IsType<UnsupportedPlatformKeyProvider>(provider);
#endif
    }

    /// <summary>
    /// The audit trail's idea of who is at the keyboard needs no platform split, and used
    /// to claim one.
    /// </summary>
    [Fact]
    public void The_current_user_is_the_same_implementation_everywhere()
    {
        Assert.IsType<EnvironmentCurrentUser>(Resolve<ICurrentUser>());
    }

    /// <summary>
    /// Registration only. Nothing here resolves anything that opens the database, so no
    /// key is ever asked for and no file is written.
    /// </summary>
    private static TService Resolve<TService>()
        where TService : notnull
    {
        using var services = new ServiceCollection()
            .AddLinewiseInfrastructure(options =>
                options.DatabasePath = Path.Combine(Path.GetTempPath(), "linewise-unused", "linewise.db"))
            .BuildServiceProvider();

        return services.GetRequiredService<TService>();
    }
}
