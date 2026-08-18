using System.Globalization;
using Linewise.Domain.Entities;

namespace Linewise.Application.Import;

/// <summary>
/// Works out where the names and the dates are on a sheet nobody has described yet.
/// </summary>
public interface IImportLayoutDetector
{
    DetectedLayout Detect(RawSheet sheet);
}

/// <param name="DateColumnCount">
/// How many dates were found on the header row. The measure of how sure this is: one date
/// is a coincidence, seven in a row is a week.
/// </param>
public sealed record DetectedLayout(
    int HeaderRowIndex,
    int NameColumnIndex,
    int FirstDateColumnIndex,
    int DateColumnCount)
{
    /// <summary>Nothing that reads as a week of dates. Nothing to import.</summary>
    public static DetectedLayout None { get; } = new(0, 0, 0, 0);

    /// <summary>
    /// Two dates rather than one. A single date on a row is as likely to be a "printed on"
    /// stamp in a corner as it is to be the start of a week.
    /// </summary>
    public bool Found => DateColumnCount >= 2;

    /// <summary>The same shape, expressed as the template the rest of the importer reads.</summary>
    public ImportTemplate ApplyTo(ImportTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return template with
        {
            HeaderRowIndex = HeaderRowIndex,
            NameColumnIndex = NameColumnIndex,
            FirstDateColumnIndex = FirstDateColumnIndex,
        };
    }
}

/// <inheritdoc cref="IImportLayoutDetector"/>
/// <remarks>
/// Reads the sheet rather than being told about it. A template still describes the shape and
/// is still what the importer parses against — this only works out what to put in one when
/// nobody has. ADR 0017.
/// <para>
/// Deliberately dumb, and dumb in a direction that fails loudly. It looks for a row holding
/// several dates and a column holding several names, and if it cannot find both it says so
/// rather than guessing. A wrong layout that half works would produce a plausible roster
/// built from the wrong cells, which is far worse than an import that refuses.
/// </para>
/// </remarks>
public sealed class ImportLayoutDetector : IImportLayoutDetector
{
    /// <summary>
    /// How far down to look for the header. A sheet with a title block, a company logo and a
    /// blank line above the dates is ordinary; a sheet with thirty is somebody else's
    /// problem.
    /// </summary>
    private const int RowsToSearch = 30;

    public DetectedLayout Detect(RawSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var best = DetectedLayout.None;

        foreach (var row in sheet.Rows.Take(RowsToSearch))
        {
            var dates = row.Cells
                .Where(cell => cell.DateValue is not null)
                .OrderBy(cell => cell.ColumnIndex)
                .ToList();

            // The most dates wins. A header row is the one with a week on it, and any other
            // row holding a stray date holds one or two.
            if (dates.Count <= best.DateColumnCount)
            {
                continue;
            }

            var firstDateColumn = dates[0].ColumnIndex;
            var nameColumn = NameColumnLeftOf(sheet, row.RowIndex, firstDateColumn);

            if (nameColumn is null)
            {
                continue;
            }

            best = new DetectedLayout(row.RowIndex, nameColumn.Value, firstDateColumn, dates.Count);
        }

        return best;
    }

    /// <summary>
    /// The column the names are in: the one to the left of the dates holding the most text
    /// that reads like a name.
    /// </summary>
    /// <remarks>
    /// Counting non-empty cells is not enough, and the first version of this got it wrong.
    /// A sheet with a payroll number in column A and the name in column B fills both columns
    /// completely, so "the fuller column" ties and the tie-break took the numbers.
    /// <para>
    /// A number is not a name. Cells whose whole text parses as a number are not counted,
    /// which separates 1001 from Ada Fictional without needing to know anything about what
    /// names look like. Dates are not counted either: a column of them to the left of the
    /// header's dates is a different thing entirely, and reading it as names would be worse
    /// than finding nothing.
    /// </para>
    /// </remarks>
    private static int? NameColumnLeftOf(RawSheet sheet, int headerRowIndex, int firstDateColumn)
    {
        var counts = new Dictionary<int, int>();

        foreach (var row in sheet.Rows.Where(row => row.RowIndex > headerRowIndex))
        {
            foreach (var cell in row.Cells.Where(cell => cell.ColumnIndex < firstDateColumn))
            {
                if (string.IsNullOrWhiteSpace(cell.Text)
                    || cell.DateValue is not null
                    || IsANumber(cell.Text))
                {
                    continue;
                }

                counts[cell.ColumnIndex] = counts.GetValueOrDefault(cell.ColumnIndex) + 1;
            }
        }

        if (counts.Count == 0)
        {
            return null;
        }

        // Ties still go to the leftmost, which is where a name sits on every sheet seen so
        // far. The tie is a genuine one by this point: two columns of comparable text.
        return counts
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key)
            .First()
            .Key;
    }

    /// <summary>
    /// Whether the whole cell is a number, which is what a payroll or clock number looks
    /// like. Invariant culture, because the sheet may have been typed anywhere.
    /// </summary>
    private static bool IsANumber(string text) =>
        double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
}
