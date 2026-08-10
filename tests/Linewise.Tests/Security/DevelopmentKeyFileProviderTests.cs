using System.Runtime.Versioning;
using Linewise.Infrastructure;
using Linewise.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Linewise.Tests.Security;

/// <summary>
/// The development key provider, which stands in for DPAPI on a platform that has none.
/// </summary>
/// <remarks>
/// Marked unsupported on Windows for the same reason the provider itself is: this assembly
/// targets net8.0-windows, so the platform analyser assumes Windows and would reject the
/// file mode assertions. Every test here also skips at runtime on Windows, where the
/// provider refuses to be constructed at all.
/// </remarks>
[UnsupportedOSPlatform("windows")]
[Trait(TestCategories.Key, TestCategories.Integration)]
public sealed class DevelopmentKeyFileProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "linewise-tests",
        Guid.NewGuid().ToString("N"));

    private string KeyFile => Path.Combine(_root, "database.development.key");

    [PosixFact]
    public void Creates_a_key_on_first_use()
    {
        var key = Provider().GetKey();

        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.True(File.Exists(KeyFile));

        // 32 random bytes, base64. Anything shorter means the generator was not asked for
        // what this claims to store.
        Assert.Equal(32, Convert.FromBase64String(key).Length);
    }

    [PosixFact]
    public void Returns_the_same_key_on_a_later_run()
    {
        var first = Provider().GetKey();
        var second = Provider().GetKey();

        Assert.Equal(first, second);
    }

    [PosixFact]
    public void Writes_the_key_readable_only_by_its_owner()
    {
        Provider().GetKey();

        var mode = File.GetUnixFileMode(KeyFile);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    /// <summary>
    /// The interesting case is a file that was correct when written and has been loosened
    /// since. Leaving it that way because it already existed would be the wrong half of the
    /// job.
    /// </summary>
    [PosixFact]
    public void Tightens_permissions_that_have_been_loosened()
    {
        Provider().GetKey();

        File.SetUnixFileMode(
            KeyFile,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        Provider().GetKey();

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(KeyFile));
    }

    /// <summary>
    /// Named differently from the shipped provider's file on purpose, so that neither a
    /// person reading a directory listing nor a restore routine can mistake one for the
    /// other.
    /// </summary>
    [PosixFact]
    public void Does_not_write_over_the_shipped_providers_key_file()
    {
        Provider().GetKey();

        Assert.False(File.Exists(Path.Combine(_root, "database.key")));
    }

    private DevelopmentKeyFileProvider Provider() =>
        new(Options.Create(new LinewiseDatabaseOptions
        {
            DatabasePath = Path.Combine(_root, "linewise.db"),
        }));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
