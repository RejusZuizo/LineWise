namespace Linewise.Domain.Enums;

/// <summary>
/// Stable identifier for a warning. Tests and the user interface key off the code, never
/// off the message text, which is translated.
/// </summary>
public enum WarningCode
{
    // Raised while generating a roster.

    /// <summary>A line finished below its required headcount.</summary>
    LineUnderHeadcount = 1,

    /// <summary>A line finished with nobody leading it.</summary>
    LineHasNoLeader = 2,

    /// <summary>An employee was available and was left on no line at all.</summary>
    EmployeeUnassigned = 3,

    /// <summary>A mandatory preference could not be honoured and the employee is not on overtime.</summary>
    MandatoryPreferenceNotHonoured = 4,

    /// <summary>A mandatory preference was set aside, which the employee's overtime status permits.</summary>
    MandatoryPreferenceBrokenForOvertime = 5,

    /// <summary>A locked assignment kept an employee who is not available that day.</summary>
    LockedAssignmentConflictsWithAvailability = 6,

    /// <summary>A locked assignment kept an employee who is blocked from the line or lacks a required skill.</summary>
    LockedAssignmentViolatesEligibility = 7,

    // Raised by the rule validator, before generation is attempted.

    /// <summary>More employees are mandatory on a line than the line has places.</summary>
    TooManyMandatoryPreferencesForLine = 100,

    /// <summary>An employee is blocked from every line and can never be rostered.</summary>
    EmployeeBlockedFromEveryLine = 101,

    /// <summary>A line requires a skill that no active employee holds.</summary>
    LineRequiresSkillNobodyHolds = 102,

    /// <summary>A line has no employee eligible to lead it.</summary>
    LineHasNoEligibleLeader = 103,

    /// <summary>An employee is both mandatory on and blocked from the same line.</summary>
    ContradictoryPreference = 104,

    /// <summary>An employee is mandatory on a line whose required skills they do not hold.</summary>
    MandatoryPreferenceWithoutRequiredSkills = 105,

    /// <summary>An employee holds more than one preference at the same rank.</summary>
    DuplicatePreferenceRank = 106,

    /// <summary>A line has a required headcount of zero or less and can never be filled.</summary>
    LineHasNoPlaces = 107,

    /// <summary>
    /// An employee holds a mandatory preference on more than one line. The engine uses the
    /// highest ranked of them.
    /// </summary>
    EmployeeMandatoryOnMultipleLines = 108,

    // Raised while importing an availability sheet. Every one of these is a warning rather
    // than an exception: a sheet the parser dislikes must still produce a review screen.

    /// <summary>The workbook holds no worksheet by the name the template expects.</summary>
    ImportWorksheetMissing = 200,

    /// <summary>No date could be read from the header row, so there is nothing to import.</summary>
    ImportNoDateColumns = 201,

    /// <summary>A header cell holds something, but nothing that reads as a date.</summary>
    ImportDateHeaderUnreadable = 202,

    /// <summary>A name on the sheet matched nobody. Addable during review as a temporary worker.</summary>
    ImportNameUnmatched = 203,

    /// <summary>A name on the sheet fitted more than one employee equally well.</summary>
    ImportNameAmbiguous = 204,

    /// <summary>A name matched only after forgiving a typo, and is worth a second look.</summary>
    ImportNameMatchedLoosely = 205,

    /// <summary>A cell holds a mark that no rule in the template recognises.</summary>
    ImportCellUnrecognised = 206,

    /// <summary>A cell has a fill whose colour could not be resolved.</summary>
    ImportCellColourUnreadable = 207,

    /// <summary>The file is larger, or holds more entries, than an availability sheet should.</summary>
    ImportFileTooLarge = 208,

    /// <summary>The file is not a spreadsheet, or not one this can read.</summary>
    ImportFileNotAWorkbook = 209,

    /// <summary>The worksheet holds no rows below the header.</summary>
    ImportNoRows = 210,

    /// <summary>
    /// An import was committed with rows nobody tied to an employee. Not a failure: those
    /// people simply have no availability recorded, and the rest of the week went in.
    /// </summary>
    ImportRowsLeftUnresolved = 211,

    /// <summary>
    /// One employee ended up on more than one row. The first row on the sheet was used.
    /// </summary>
    ImportEmployeeOnMoreThanOneRow = 212,
}
