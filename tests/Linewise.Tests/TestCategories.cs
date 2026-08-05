namespace Linewise.Tests;

/// <summary>
/// Categories for filtering a test run.
/// </summary>
/// <remarks>
/// The line is drawn at what a test touches, not at what it proves. A test carrying
/// <see cref="Integration"/> reaches a database, the file system, or a third party engine
/// that does real work — SQLite, ClosedXML, QuestPDF. Everything else runs entirely in
/// memory against code written in this repository.
/// <para>
/// The point is a fast loop while working on rules:
/// </para>
/// <code>
/// dotnet test --filter Category!=Integration
/// </code>
/// <para>
/// Uncategorised means unit, deliberately. A new test is fast until somebody makes it slow,
/// and that person is in a position to say so. The alternative, requiring every test to
/// declare itself, means the filter silently stops covering whatever was forgotten.
/// </para>
/// </remarks>
internal static class TestCategories
{
    public const string Key = "Category";

    public const string Integration = "Integration";
}
