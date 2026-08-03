using ClosedXML.Excel;
using Linewise.Application.Import;
using Linewise.Domain.Rostering;

namespace Linewise.Infrastructure.Import;

/// <inheritdoc cref="IAvailabilitySheetReader"/>
public sealed class ClosedXmlSheetReader : IAvailabilitySheetReader
{
    public Task<SheetReadResult> ReadAsync(
        byte[] workbook,
        string? worksheetName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return Task.FromResult(Read(workbook, worksheetName, cancellationToken));
    }

    private static SheetReadResult Read(byte[] workbook, string? worksheetName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A view over the caller's array rather than a copy of it. The safety check reads the
        // archive first and the library then reads it again from the beginning.
        using var buffered = new MemoryStream(workbook, writable: false);

        var safety = WorkbookSafety.Check(buffered);

        if (safety != WorkbookSafetyResult.Acceptable)
        {
            return Failed(safety == WorkbookSafetyResult.TooLarge
                ? ImportWarnings.FileTooLarge()
                : ImportWarnings.NotAWorkbook());
        }

        try
        {
            using var document = new XLWorkbook(buffered);

            var sheet = worksheetName is { Length: > 0 }
                ? document.Worksheets.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, worksheetName, StringComparison.OrdinalIgnoreCase))
                : document.Worksheets.FirstOrDefault();

            if (sheet is null)
            {
                return Failed(worksheetName is { Length: > 0 }
                    ? ImportWarnings.WorksheetMissing(worksheetName)
                    : ImportWarnings.NotAWorkbook());
            }

            return new SheetReadResult(ReadWorksheet(sheet, document.Theme), []);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Anything the library throws on a malformed file stops here. A parse failure is
            // something the operator needs told about, not a stack trace.
            return Failed(ImportWarnings.NotAWorkbook());
        }
    }

    private static RawSheet ReadWorksheet(IXLWorksheet sheet, IXLTheme theme)
    {
        var rows = new List<RawRow>();

        foreach (var row in sheet.RowsUsed())
        {
            var cells = new List<RawCell>();

            foreach (var cell in row.CellsUsed(XLCellsUsedOptions.All))
            {
                cells.Add(ReadCell(cell, theme));
            }

            if (cells.Count > 0)
            {
                rows.Add(new RawRow(row.RowNumber(), cells));
            }
        }

        return new RawSheet
        {
            WorksheetName = sheet.Name,
            Rows = rows,
        };
    }

    private static RawCell ReadCell(IXLCell cell, IXLTheme theme)
    {
        var (colour, unreadable) = ReadFill(cell, theme);

        return new RawCell
        {
            ColumnIndex = cell.Address.ColumnNumber,
            Text = ReadText(cell),
            DateValue = ReadDate(cell),
            FillColourHex = colour,
            FillUnreadable = unreadable,
        };
    }

    /// <summary>
    /// Never evaluates a formula. A cell holding one gives up its cached value, which is what
    /// the sheet last showed a human, and calculating it here would mean running expressions
    /// that arrived from outside.
    /// </summary>
    private static string ReadText(IXLCell cell)
    {
        try
        {
            if (!cell.HasFormula)
            {
                return cell.GetString().Trim();
            }

            var cached = cell.CachedValue;

            return cached.IsBlank ? string.Empty : cached.ToString().Trim();
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException)
        {
            return string.Empty;
        }
    }

    private static DateOnly? ReadDate(IXLCell cell)
    {
        try
        {
            if (cell.DataType == XLDataType.DateTime && !cell.HasFormula)
            {
                return DateOnly.FromDateTime(cell.GetDateTime());
            }
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException)
        {
            // Falls through to null, and the importer reports the header as unreadable.
        }

        return null;
    }

    /// <summary>
    /// Resolves the fill to a plain hex colour where possible.
    /// </summary>
    /// <remarks>
    /// Cell colour is the least reliable thing in a spreadsheet. It arrives as a plain value,
    /// as an index into a palette, or as a theme reference plus a tint, and the theme needs a
    /// part of the package that is not always present. Anything that cannot be resolved is
    /// reported as unreadable rather than guessed at or thrown over.
    /// </remarks>
    private static (string? Colour, bool Unreadable) ReadFill(IXLCell cell, IXLTheme theme)
    {
        try
        {
            var fill = cell.Style.Fill;

            if (fill.PatternType == XLFillPatternValues.None)
            {
                return (null, false);
            }

            var background = fill.BackgroundColor;

            if (background is null)
            {
                return (null, true);
            }

            var colour = background.ColorType switch
            {
                // An index into the legacy palette.
                XLColorType.Indexed => XLColor.FromIndex(background.Indexed).Color,

                // A reference into the workbook's theme plus a lightening or darkening. This
                // is what the standard Excel colour picker produces, so treating it as
                // unreadable would fail on the majority of coloured sheets.
                XLColorType.Theme => ApplyTint(
                    theme.ResolveThemeColor(background.ThemeColor).Color,
                    background.ThemeTint),

                _ => background.Color,
            };

            // Fully transparent is what an untouched cell reports on some sheets. Treated as
            // no fill, so a blank cell does not have to be configured as a colour rule.
            if (colour.A == 0)
            {
                return (null, false);
            }

            return ($"#{colour.R:X2}{colour.G:X2}{colour.B:X2}", false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A theme part that is missing, an index outside the palette, or anything else
            // the library objects to. Reported as unreadable rather than guessed at.
            return (null, true);
        }
    }

    /// <summary>
    /// Applies a theme tint. A positive tint blends toward white and a negative one toward
    /// black, which is how Excel builds the lighter and darker variants of a theme colour.
    /// </summary>
    private static System.Drawing.Color ApplyTint(System.Drawing.Color colour, double tint)
    {
        if (Math.Abs(tint) < 0.0001)
        {
            return colour;
        }

        return System.Drawing.Color.FromArgb(colour.A, Blend(colour.R), Blend(colour.G), Blend(colour.B));

        int Blend(byte channel) => tint > 0
            ? (int)Math.Round(channel + ((255 - channel) * tint))
            : (int)Math.Round(channel * (1 + tint));
    }

    private static SheetReadResult Failed(RosterWarning warning) => new(null, [warning]);
}
