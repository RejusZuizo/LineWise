namespace Linewise.Application.Printing;

/// <summary>
/// Defuses a value that a spreadsheet would treat as a formula.
/// </summary>
/// <remarks>
/// An employee called <c>=cmd|' /c calc'!A1</c> is not a joke somebody will make once. Any
/// value starting with an equals, plus, minus or at sign is executed by Excel when the file
/// is opened, on whichever machine opens it, which turns a name field into remote code
/// execution on somebody else's computer.
/// <para>
/// This is applied to comma separated and workbook exports, where the risk is real. It is
/// deliberately not applied to the printed roster: a PDF is never evaluated by a
/// spreadsheet, and prefixing an apostrophe would put a stray mark next to somebody's name
/// on a sheet pinned to a wall. The rule protects the reader of an export, not the reader of
/// a page.
/// </para>
/// </remarks>
public static class ExportSanitiser
{
    private static readonly char[] Dangerous = ['=', '+', '-', '@'];

    /// <summary>
    /// Returns the value with a leading apostrophe where one is needed, which spreadsheets
    /// read as "this is text" and do not display.
    /// </summary>
    public static string Sanitise(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // Leading whitespace does not save you: spreadsheets trim before deciding whether
        // something is a formula, so the check has to trim too.
        var trimmed = value.TrimStart();

        return trimmed.Length > 0 && Dangerous.Contains(trimmed[0])
            ? "'" + value
            : value;
    }
}
