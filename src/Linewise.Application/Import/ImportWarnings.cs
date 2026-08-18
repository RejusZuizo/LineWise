using Linewise.Application.Resources;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <summary>
/// Warnings raised while reading a sheet. Every one is a warning and never an exception: a
/// file the parser dislikes still has to produce a review screen the operator can act on.
/// </summary>
/// <remarks>
/// Messages refer to rows and columns, never to the name written in them. The name is
/// carried on the row for the review screen to show, so nothing that might reach a log
/// holds personal data.
/// </remarks>
public static class ImportWarnings
{
    public static RosterWarning WorksheetMissing(string worksheetName) =>
        WarningFactory.Create(WarningCode.ImportWorksheetMissing, WarningSeverity.Error, [worksheetName]);

    public static RosterWarning NoDateColumns(int headerRowIndex) =>
        WarningFactory.Create(WarningCode.ImportNoDateColumns, WarningSeverity.Error, [headerRowIndex]);

    public static RosterWarning DateHeaderUnreadable(int columnIndex) =>
        WarningFactory.Create(WarningCode.ImportDateHeaderUnreadable, WarningSeverity.Notice, [columnIndex]);

    public static RosterWarning NameUnmatched(int rowIndex) =>
        WarningFactory.Create(WarningCode.ImportNameUnmatched, WarningSeverity.Error, [rowIndex]);

    public static RosterWarning NameAmbiguous(int rowIndex, int candidates) =>
        WarningFactory.Create(WarningCode.ImportNameAmbiguous, WarningSeverity.Error, [rowIndex, candidates]);

    public static RosterWarning NameMatchedLoosely(int rowIndex) =>
        WarningFactory.Create(WarningCode.ImportNameMatchedLoosely, WarningSeverity.Notice, [rowIndex]);

    public static RosterWarning CellUnrecognised(int rowIndex, int columnIndex) =>
        WarningFactory.Create(WarningCode.ImportCellUnrecognised, WarningSeverity.Notice, [rowIndex, columnIndex]);

    public static RosterWarning CellColourUnreadable(int rowIndex, int columnIndex) =>
        WarningFactory.Create(WarningCode.ImportCellColourUnreadable, WarningSeverity.Notice, [rowIndex, columnIndex]);

    public static RosterWarning FileTooLarge() =>
        WarningFactory.Create(WarningCode.ImportFileTooLarge, WarningSeverity.Error, []);

    public static RosterWarning NotAWorkbook() =>
        WarningFactory.Create(WarningCode.ImportFileNotAWorkbook, WarningSeverity.Error, []);

    public static RosterWarning NoRows(int headerRowIndex) =>
        WarningFactory.Create(WarningCode.ImportNoRows, WarningSeverity.Error, [headerRowIndex]);

    public static RosterWarning RowsLeftUnresolved(int rowCount) =>
        WarningFactory.Create(WarningCode.ImportRowsLeftUnresolved, WarningSeverity.Notice, [rowCount]);

    /// <summary>
    /// A record the manager set by hand that this import left alone. One per record rather
    /// than a count, so the review screen can name the person and the day.
    /// </summary>
    public static RosterWarning ManualAvailabilityKept(Guid employeeId, DateOnly date) =>
        WarningFactory.Create(
            WarningCode.ImportManualAvailabilityKept,
            WarningSeverity.Notice,
            [],
            employeeId: employeeId,
            date: date);

    /// <summary>
    /// The layout was read off the sheet rather than taken from the template. Says what was
    /// assumed, in the terms the operator would use to check it.
    /// </summary>
    public static RosterWarning LayoutDetected(int headerRow, int nameColumn, int dateColumns) =>
        WarningFactory.Create(
            WarningCode.ImportLayoutDetected,
            WarningSeverity.Notice,
            [headerRow, nameColumn, dateColumns]);

    /// <summary>
    /// The detected layout was written back onto the template. Said out loud, because it
    /// changes what the next import will do and a silent configuration change is the kind
    /// nobody can account for later.
    /// </summary>
    public static RosterWarning LayoutLearned(int headerRow, int nameColumn) =>
        WarningFactory.Create(
            WarningCode.ImportLayoutLearned,
            WarningSeverity.Notice,
            [headerRow, nameColumn]);

    /// <summary>
    /// Spellings kept against the people they were pointed at, so next week's sheet matches
    /// them without asking.
    /// </summary>
    public static RosterWarning NamesLearned(int count) =>
        WarningFactory.Create(
            WarningCode.ImportNamesLearned,
            WarningSeverity.Notice,
            [count]);

    public static RosterWarning EmployeeOnMoreThanOneRow(Guid employeeId) =>
        WarningFactory.Create(
            WarningCode.ImportEmployeeOnMoreThanOneRow,
            WarningSeverity.Notice,
            [],
            employeeId: employeeId);
}
