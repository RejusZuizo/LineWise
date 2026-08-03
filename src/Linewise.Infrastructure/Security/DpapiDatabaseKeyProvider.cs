using System.Security.Cryptography;
using Linewise.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Linewise.Infrastructure.Security;

/// <summary>
/// Keeps the database key in a file that only this Windows account on this machine can
/// read, using DPAPI. The key itself is never written to configuration, never logged, and
/// never leaves this class as anything but the passphrase handed to SQLCipher.
/// </summary>
/// <remarks>
/// The consequence is worth stating plainly, because it is the sharp edge of this design:
/// losing the Windows profile means losing the database. Backups are encrypted with the
/// same key, so they do not save you either. This is why the restore procedure is
/// documented and tested rather than assumed.
/// </remarks>
public sealed class DpapiDatabaseKeyProvider : IDatabaseKeyProvider
{
    private const string KeyFileName = "database.key";

    /// <summary>
    /// Extra entropy mixed into the protection. Not a secret and not doing much on its own;
    /// it means a blob lifted from this machine cannot be unprotected by another
    /// application running as the same user without knowing to supply this too.
    /// </summary>
    private static readonly byte[] Entropy = "Linewise.Database.Key.v1"u8.ToArray();

    private readonly Lazy<string> _key;

    public DpapiDatabaseKeyProvider(IOptions<LinewiseDatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(options.Value.ResolvedDatabasePath)
            ?? LinewiseDatabaseOptions.DefaultDirectory;

        // Lazy, so the file is touched once on first use and then never again. This is the
        // one piece of synchronous I/O in the stack: it happens during startup composition,
        // not on the UI thread, and making it async would push async plumbing through every
        // context construction for a single cached read.
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
            var protectedKey = File.ReadAllBytes(keyFilePath);
            return Convert.ToBase64String(
                ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.CurrentUser));
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(
            keyFilePath,
            ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));

        return Convert.ToBase64String(key);
    }
}
