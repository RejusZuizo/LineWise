namespace Linewise.Infrastructure.Logging;

/// <summary>
/// Strips the user profile out of a path before it is logged. Threat T6.
/// </summary>
/// <remarks>
/// The paths this application handles are the database, its backups, and whichever
/// spreadsheet somebody just imported. All three sit under a home directory, and a home
/// directory is named after a person. A log line reading
/// <c>C:\Users\j.smith\Downloads\week 32.xlsx</c> has identified an employee, named a file
/// that lists every other employee, and done it in a file that is not encrypted.
/// <para>
/// The filename is kept. Which sheet failed to parse is the question the log exists to
/// answer, and a sheet is named after a week rather than a person.
/// </para>
/// </remarks>
public static class SafePath
{
    public const string ProfilePlaceholder = "~";

    /// <summary>
    /// Replaces the leading user profile directory with a placeholder. Anything outside a
    /// profile is left alone, because a shared drive path is operational detail rather than
    /// personal data.
    /// </summary>
    public static string ForLog(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrEmpty(profile) || !path.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var remainder = path[profile.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return remainder.Length == 0
            ? ProfilePlaceholder
            : ProfilePlaceholder + Path.DirectorySeparatorChar + remainder;
    }
}
