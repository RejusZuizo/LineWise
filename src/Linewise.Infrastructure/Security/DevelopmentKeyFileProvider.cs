using System.Runtime.Versioning;
using System.Security.Cryptography;
using Linewise.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Linewise.Infrastructure.Security;

/// <summary>
/// Keeps the database key in a file readable only by the owning user, for development on a
/// platform that has no DPAPI.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is weaker than the shipped provider and is not a substitute for it.</b> DPAPI
/// binds the key to a Windows account on a machine, so a copied file is useless elsewhere.
/// This binds it to nothing: any process running as this user can read the file, and a
/// backup of the home directory carries the key alongside the database it opens.
/// </para>
/// <para>
/// That is an acceptable trade on a development machine holding invented names, and
/// unacceptable anywhere else. It is registered only on a non-Windows platform and only in
/// a debug build — see <c>ServiceCollectionExtensions</c>, and ADR 0011 for why the
/// enforcement sits at registration rather than in this constructor.
/// </para>
/// <para>
/// The file is named differently from the shipped one on purpose. The two must never be
/// mistaken for each other by anybody reading a directory listing, or by a restore routine.
/// </para>
/// </remarks>
/// <remarks>
/// The attribute is not decoration. This assembly targets <c>net8.0-windows</c> (ADR 0006),
/// so the platform analyser assumes Windows everywhere and rejects the Unix file mode calls
/// below as unsupported. Declaring the class unsupported on Windows is the same move ADR
/// 0006 made in the other direction: say what is true and let the analyser agree, rather
/// than suppress it.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class DevelopmentKeyFileProvider : IDatabaseKeyProvider
{
    private const string KeyFileName = "database.development.key";

    /// <summary>Owner read and write, nothing for anybody else.</summary>
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly Lazy<string> _key;

    public DevelopmentKeyFileProvider(IOptions<LinewiseDatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The development key provider exists because Windows data protection does "
                + "not, and must never be used on Windows, where DPAPI is available.");
        }

        var directory = Path.GetDirectoryName(options.Value.ResolvedDatabasePath)
            ?? LinewiseDatabaseOptions.DefaultDirectory;

        // Lazy for the same reason the shipped provider is: the file is touched once during
        // startup composition and then never again.
        _key = new Lazy<string>(() => LoadOrCreate(Path.Combine(directory, KeyFileName)));
    }

    public string GetKey() => _key.Value;

    private static string LoadOrCreate(string keyFilePath)
    {
        var directory = Path.GetDirectoryName(keyFilePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(keyFilePath))
        {
            // Re-apply the mode on every read. A file that has been made group readable
            // since it was written is the interesting case, and leaving it that way because
            // it already existed would be the wrong half of the job.
            File.SetUnixFileMode(keyFilePath, OwnerOnly);

            return File.ReadAllText(keyFilePath).Trim();
        }

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Created before writing, so the key is never briefly on disk under the default
        // mode. A window of a few milliseconds is still a window.
        using (var stream = new FileStream(
            keyFilePath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = OwnerOnly,
            }))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(key);
        }

        return key;
    }
}
