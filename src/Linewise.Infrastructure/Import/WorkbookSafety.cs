using System.IO.Compression;

namespace Linewise.Infrastructure.Import;

/// <summary>
/// Checks a file looks like an availability sheet before any spreadsheet library is handed
/// it.
/// </summary>
/// <remarks>
/// An .xlsx is a zip archive, and a zip archive from outside is hostile until shown
/// otherwise. A few kilobytes of cleverly nested entries expand to gigabytes and take the
/// machine down with them, so the limits are checked against the declared sizes before a
/// single entry is opened.
/// </remarks>
internal static class WorkbookSafety
{
    /// <summary>Generous for a sheet of 150 names, nowhere near enough to hurt.</summary>
    public const long MaximumCompressedBytes = 32L * 1024 * 1024;

    public const long MaximumUncompressedBytes = 256L * 1024 * 1024;

    public const int MaximumEntryCount = 1024;

    /// <summary>
    /// How much larger the contents may be than the file. Ordinary spreadsheet XML
    /// compresses perhaps twenty to one; a decompression bomb aims for thousands to one.
    /// </summary>
    public const int MaximumExpansionRatio = 200;

    public static WorkbookSafetyResult Check(Stream workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (workbook.CanSeek && workbook.Length > MaximumCompressedBytes)
        {
            return WorkbookSafetyResult.TooLarge;
        }

        try
        {
            using var archive = new ZipArchive(workbook, ZipArchiveMode.Read, leaveOpen: true);

            if (archive.Entries.Count > MaximumEntryCount)
            {
                return WorkbookSafetyResult.TooLarge;
            }

            long uncompressed = 0;
            long compressed = 0;
            var hasContentTypes = false;
            var hasWorkbook = false;

            foreach (var entry in archive.Entries)
            {
                uncompressed += entry.Length;
                compressed += entry.CompressedLength;

                if (uncompressed > MaximumUncompressedBytes)
                {
                    return WorkbookSafetyResult.TooLarge;
                }

                if (entry.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    hasContentTypes = true;
                }

                if (entry.FullName.StartsWith("xl/workbook.", StringComparison.OrdinalIgnoreCase))
                {
                    hasWorkbook = true;
                }
            }

            if (compressed > 0 && uncompressed / compressed > MaximumExpansionRatio)
            {
                return WorkbookSafetyResult.TooLarge;
            }

            // The layout every real .xlsx has. A zip full of something else is not a
            // spreadsheet, whatever it has been renamed to.
            return hasContentTypes && hasWorkbook
                ? WorkbookSafetyResult.Acceptable
                : WorkbookSafetyResult.NotAWorkbook;
        }
        catch (InvalidDataException)
        {
            return WorkbookSafetyResult.NotAWorkbook;
        }
        finally
        {
            if (workbook.CanSeek)
            {
                workbook.Position = 0;
            }
        }
    }
}

internal enum WorkbookSafetyResult
{
    Acceptable = 0,
    NotAWorkbook = 1,
    TooLarge = 2,
}
