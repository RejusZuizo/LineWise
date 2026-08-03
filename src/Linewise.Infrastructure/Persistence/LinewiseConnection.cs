using Microsoft.Data.Sqlite;

namespace Linewise.Infrastructure.Persistence;

/// <summary>Builds the connection string, key included.</summary>
internal static class LinewiseConnection
{
    /// <summary>
    /// The key is passed as the connection password, which Microsoft.Data.Sqlite turns into
    /// a PRAGMA key against the SQLCipher provider. It is never written to a configuration
    /// file, and this string is never logged.
    /// </summary>
    public static string BuildConnectionString(string databasePath, string key) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = key,
            Pooling = true,
        }.ToString();
}
