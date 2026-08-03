namespace Linewise.Infrastructure;

/// <summary>
/// Where the database and its backups live, and how many copies to keep.
/// </summary>
/// <remarks>
/// Paths come from configuration but default to the per user application data folder.
/// Never beside the executable: that directory is often read only, gets replaced wholesale
/// by an update, and is the first place a backup routine forgets to look.
/// </remarks>
public sealed class LinewiseDatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Full path to the database file. Defaults under the application data folder.</summary>
    public string? DatabasePath { get; set; }

    /// <summary>Directory for rolling backups. Defaults under the application data folder.</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>How many backups to keep before the oldest is pruned.</summary>
    public int BackupsToKeep { get; set; } = 10;

    /// <summary>Whether to apply pending migrations on startup.</summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Whether to take a backup on startup.</summary>
    public bool BackupOnStartup { get; set; } = true;

    public string ResolvedDatabasePath =>
        DatabasePath ?? Path.Combine(DefaultDirectory, "linewise.db");

    public string ResolvedBackupDirectory =>
        BackupDirectory ?? Path.Combine(DefaultDirectory, "backups");

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Linewise");
}
