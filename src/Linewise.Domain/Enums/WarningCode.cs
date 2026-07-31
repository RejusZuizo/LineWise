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
}
