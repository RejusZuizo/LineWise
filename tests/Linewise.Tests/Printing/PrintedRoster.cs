using System.Text;
using System.Text.RegularExpressions;

namespace Linewise.Tests.Printing;

/// <summary>
/// Just enough understanding of a PDF to assert something useful about one, without taking
/// a dependency on a reader to test a writer.
/// </summary>
internal static partial class PrintedRoster
{
    public static bool IsAPdf(byte[] document) =>
        document.Length > 4 && Encoding.ASCII.GetString(document, 0, 5) == "%PDF-";

    /// <summary>
    /// Counts page objects. Every page in a PDF is a dictionary carrying <c>/Type /Page</c>,
    /// and the pages tree that holds them says <c>/Pages</c>, so the trailing character
    /// matters and the pattern excludes it.
    /// </summary>
    public static int PageCount(byte[] document)
    {
        var text = Encoding.Latin1.GetString(document);

        return PageObject().Matches(text).Count;
    }

    /// <summary>
    /// Writes the document somewhere a person can open it. Producing a valid PDF and
    /// producing one that can be read across a factory are different claims, and only the
    /// first can be asserted in a test.
    /// </summary>
    public static string Save(byte[] document, string name)
    {
        var directory = Path.Combine(RepositoryRoot(), "artifacts", "print-preview");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, document);

        return path;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Linewise.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    [GeneratedRegex(@"/Type\s*/Page[^s]")]
    private static partial Regex PageObject();
}
