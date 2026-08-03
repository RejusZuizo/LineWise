using System.Globalization;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <inheritdoc cref="IAvailabilityImportBuilder"/>
public sealed class AvailabilityImportBuilder : IAvailabilityImportBuilder
{
    public AvailabilityImportResult Build(
        RawSheet sheet,
        ImportTemplate template,
        IReadOnlyList<Employee> employees)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(employees);

        var warnings = new List<RosterWarning>();
        var dateColumns = ReadDateColumns(sheet, template, warnings);

        if (dateColumns.Count == 0)
        {
            warnings.Add(ImportWarnings.NoDateColumns(template.HeaderRowIndex));
            return new AvailabilityImportResult { Warnings = warnings };
        }

        var matcher = new EmployeeNameMatcher(employees);
        var rows = new List<ImportedRow>();

        foreach (var row in sheet.Rows
            .Where(row => row.RowIndex > template.HeaderRowIndex)
            .OrderBy(row => row.RowIndex))
        {
            var name = CellAt(row, template.NameColumnIndex)?.Text ?? string.Empty;

            // Blank rows are how people space out a sheet. Not an error, not a row.
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var match = matcher.Match(name);
            RecordMatchOutcome(match, row.RowIndex, warnings);

            var cells = new List<ImportedCell>();

            foreach (var (columnIndex, date) in dateColumns.OrderBy(column => column.Key))
            {
                var reading = ReadCell(CellAt(row, columnIndex), template);

                if (reading.Problem is { } problem)
                {
                    warnings.Add(problem == WarningCode.ImportCellColourUnreadable
                        ? ImportWarnings.CellColourUnreadable(row.RowIndex, columnIndex)
                        : ImportWarnings.CellUnrecognised(row.RowIndex, columnIndex));
                }

                cells.Add(new ImportedCell(date, reading.Status, reading.Problem is null));
            }

            rows.Add(new ImportedRow
            {
                RowIndex = row.RowIndex,
                SheetName = name.Trim(),
                Match = match,
                Cells = cells,
            });
        }

        if (rows.Count == 0)
        {
            warnings.Add(ImportWarnings.NoRows(template.HeaderRowIndex));
        }

        return new AvailabilityImportResult
        {
            Dates = dateColumns.Values.Order().Distinct().ToList(),
            Rows = rows,
            Warnings = warnings,
        };
    }

    private static void RecordMatchOutcome(NameMatch match, int rowIndex, List<RosterWarning> warnings)
    {
        switch (match.Outcome)
        {
            case NameMatchOutcome.Unmatched:
                warnings.Add(ImportWarnings.NameUnmatched(rowIndex));
                break;

            case NameMatchOutcome.Ambiguous:
                warnings.Add(ImportWarnings.NameAmbiguous(rowIndex, match.Candidates.Count));
                break;

            case NameMatchOutcome.Fuzzy:
                warnings.Add(ImportWarnings.NameMatchedLoosely(rowIndex));
                break;

            case NameMatchOutcome.Exact:
            default:
                break;
        }
    }

    private static Dictionary<int, DateOnly> ReadDateColumns(
        RawSheet sheet,
        ImportTemplate template,
        List<RosterWarning> warnings)
    {
        var header = sheet.Rows.FirstOrDefault(row => row.RowIndex == template.HeaderRowIndex);

        if (header is null)
        {
            return [];
        }

        var dates = new Dictionary<int, DateOnly>();

        foreach (var cell in header.Cells
            .Where(cell => cell.ColumnIndex >= template.FirstDateColumnIndex)
            .OrderBy(cell => cell.ColumnIndex))
        {
            // A date Excel already holds as a date beats anything parsed from text, which is
            // at the mercy of whichever locale the sheet was typed in.
            if (cell.DateValue is { } stored)
            {
                dates[cell.ColumnIndex] = stored;
                continue;
            }

            if (string.IsNullOrWhiteSpace(cell.Text))
            {
                continue;
            }

            if (template.DateFormat is { Length: > 0 } format
                && DateOnly.TryParseExact(
                    cell.Text.Trim(),
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                dates[cell.ColumnIndex] = parsed;
                continue;
            }

            // Something is written there and it is not a date. Reported rather than guessed
            // at: inventing a date silently shifts a whole column of people by a day.
            warnings.Add(ImportWarnings.DateHeaderUnreadable(cell.ColumnIndex));
        }

        return dates;
    }

    private static CellReading ReadCell(RawCell? cell, ImportTemplate template)
    {
        if (cell is null || cell.IsEmpty)
        {
            return new CellReading(template.EmptyCellStatus, null);
        }

        // Text before colour, always. Text survives being copied between workbooks, emailed
        // and re-saved; colour frequently does not.
        if (!string.IsNullOrWhiteSpace(cell.Text))
        {
            foreach (var rule in template.StatusRules.Where(rule => rule.Kind == CellMatchKind.Text))
            {
                if (string.Equals(rule.Value.Trim(), cell.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return new CellReading(rule.Status, null);
                }
            }
        }

        if (cell.FillColourHex is { } colour)
        {
            foreach (var rule in template.StatusRules.Where(rule => rule.Kind == CellMatchKind.FillColour))
            {
                if (ColoursMatch(rule.Value, colour))
                {
                    return new CellReading(rule.Status, null);
                }
            }
        }

        if (!template.ReportUnrecognisedCells)
        {
            return new CellReading(template.EmptyCellStatus, null);
        }

        return new CellReading(
            template.EmptyCellStatus,
            cell.FillUnreadable ? WarningCode.ImportCellColourUnreadable : WarningCode.ImportCellUnrecognised);
    }

    /// <summary>
    /// Compares colours by their red, green and blue components, ignoring any leading hash
    /// and any alpha channel. Excel writes the same colour as both six and eight digits
    /// depending on how it was set.
    /// </summary>
    private static bool ColoursMatch(string left, string right) =>
        string.Equals(NormaliseColour(left), NormaliseColour(right), StringComparison.Ordinal);

    private static string NormaliseColour(string colour)
    {
        var trimmed = colour.TrimStart('#').Trim().ToUpperInvariant();

        return trimmed.Length == 8 ? trimmed[2..] : trimmed;
    }

    private static RawCell? CellAt(RawRow row, int columnIndex) =>
        row.Cells.FirstOrDefault(cell => cell.ColumnIndex == columnIndex);

    private readonly record struct CellReading(AvailabilityStatus Status, WarningCode? Problem);
}
