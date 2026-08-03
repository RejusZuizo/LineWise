using Linewise.Application.Resources;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Rostering;

/// <summary>
/// Builds warnings. One place decides the severity of each code, so the engine and the
/// validator cannot drift apart on how serious something is.
/// </summary>
internal static class RosterWarnings
{
    // Raised while generating.

    public static RosterWarning LineUnderHeadcount(ProductionLine line, int actual, DateOnly date) =>
        Build(
            WarningCode.LineUnderHeadcount,
            WarningSeverity.Error,
            [line.Name, actual, line.RequiredHeadcount],
            lineId: line.Id,
            date: date);

    public static RosterWarning LineHasNoLeader(ProductionLine line, DateOnly date) =>
        Build(WarningCode.LineHasNoLeader, WarningSeverity.Error, [line.Name], lineId: line.Id, date: date);

    public static RosterWarning EmployeeUnassigned(Guid employeeId, DateOnly date) =>
        Build(WarningCode.EmployeeUnassigned, WarningSeverity.Notice, [], employeeId: employeeId, date: date);

    public static RosterWarning MandatoryPreferenceNotHonoured(ProductionLine line, Guid employeeId, DateOnly date) =>
        Build(
            WarningCode.MandatoryPreferenceNotHonoured,
            WarningSeverity.Error,
            [line.Name],
            lineId: line.Id,
            employeeId: employeeId,
            date: date);

    public static RosterWarning MandatoryPreferenceBrokenForOvertime(ProductionLine line, Guid employeeId, DateOnly date) =>
        Build(
            WarningCode.MandatoryPreferenceBrokenForOvertime,
            WarningSeverity.Notice,
            [line.Name],
            lineId: line.Id,
            employeeId: employeeId,
            date: date);

    public static RosterWarning LockedAssignmentConflictsWithAvailability(ProductionLine line, Guid employeeId, DateOnly date) =>
        Build(
            WarningCode.LockedAssignmentConflictsWithAvailability,
            WarningSeverity.Notice,
            [line.Name],
            lineId: line.Id,
            employeeId: employeeId,
            date: date);

    public static RosterWarning LockedAssignmentViolatesEligibility(ProductionLine line, Guid employeeId, DateOnly date) =>
        Build(
            WarningCode.LockedAssignmentViolatesEligibility,
            WarningSeverity.Notice,
            [line.Name],
            lineId: line.Id,
            employeeId: employeeId,
            date: date);

    // Raised by the validator, before generation.

    public static RosterWarning TooManyMandatoryPreferencesForLine(ProductionLine line, int mandatoryCount) =>
        Build(
            WarningCode.TooManyMandatoryPreferencesForLine,
            WarningSeverity.Error,
            [line.Name, mandatoryCount, line.RequiredHeadcount],
            lineId: line.Id);

    public static RosterWarning EmployeeBlockedFromEveryLine(Guid employeeId) =>
        Build(WarningCode.EmployeeBlockedFromEveryLine, WarningSeverity.Error, [], employeeId: employeeId);

    public static RosterWarning LineRequiresSkillNobodyHolds(ProductionLine line, string skillName) =>
        Build(WarningCode.LineRequiresSkillNobodyHolds, WarningSeverity.Error, [line.Name, skillName], lineId: line.Id);

    public static RosterWarning LineHasNoEligibleLeader(ProductionLine line) =>
        Build(WarningCode.LineHasNoEligibleLeader, WarningSeverity.Error, [line.Name], lineId: line.Id);

    public static RosterWarning ContradictoryPreference(ProductionLine line, Guid employeeId) =>
        Build(
            WarningCode.ContradictoryPreference,
            WarningSeverity.Error,
            [line.Name],
            lineId: line.Id,
            employeeId: employeeId);

    public static RosterWarning MandatoryPreferenceWithoutRequiredSkills(ProductionLine line, Guid employeeId, string skillName) =>
        Build(
            WarningCode.MandatoryPreferenceWithoutRequiredSkills,
            WarningSeverity.Error,
            [line.Name, skillName],
            lineId: line.Id,
            employeeId: employeeId);

    public static RosterWarning DuplicatePreferenceRank(Guid employeeId, int rank) =>
        Build(WarningCode.DuplicatePreferenceRank, WarningSeverity.Notice, [rank], employeeId: employeeId);

    public static RosterWarning LineHasNoPlaces(ProductionLine line) =>
        Build(WarningCode.LineHasNoPlaces, WarningSeverity.Notice, [line.Name], lineId: line.Id);

    public static RosterWarning EmployeeMandatoryOnMultipleLines(Guid employeeId, ProductionLine chosenLine) =>
        Build(
            WarningCode.EmployeeMandatoryOnMultipleLines,
            WarningSeverity.Notice,
            [chosenLine.Name],
            lineId: chosenLine.Id,
            employeeId: employeeId);

    private static RosterWarning Build(
        WarningCode code,
        WarningSeverity severity,
        object?[] messageArguments,
        Guid? lineId = null,
        Guid? employeeId = null,
        DateOnly? date = null) =>
        WarningFactory.Create(code, severity, messageArguments, lineId, employeeId, date);
}
