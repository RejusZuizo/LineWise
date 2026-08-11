using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Validation;

/// <inheritdoc cref="IRosterRuleValidator"/>
public sealed class RosterRuleValidator : IRosterRuleValidator
{
    public IReadOnlyList<RosterWarning> Validate(RosterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var warnings = new List<RosterWarning>();

        var lines = configuration.Lines
            .OrderBy(line => line.DisplayOrder)
            .ThenBy(line => line.Id)
            .ToList();

        var employees = configuration.Employees
            .Where(employee => employee.IsActive)
            .OrderBy(employee => employee.Id)
            .ToList();

        var skillNames = configuration.Skills.ToDictionary(skill => skill.Id, skill => skill.Name);
        var blockedFrom = BuildBlocked(configuration);

        CheckLinesHavePlaces(lines, warnings);
        CheckLinesAreStaffable(lines, employees, skillNames, warnings);
        CheckLinesHaveALeader(lines, employees, configuration, blockedFrom, warnings);
        CheckMandatoryPreferencesFit(lines, employees, configuration, warnings);
        CheckEmployeePreferences(lines, employees, configuration, skillNames, blockedFrom, warnings);

        return warnings;
    }

    private static void CheckLinesHavePlaces(IEnumerable<ProductionLine> lines, List<RosterWarning> warnings)
    {
        foreach (var line in lines.Where(line => line.RequiredHeadcount <= 0))
        {
            warnings.Add(RosterWarnings.LineHasNoPlaces(line));
        }
    }

    private static void CheckLinesAreStaffable(
        IEnumerable<ProductionLine> lines,
        IReadOnlyCollection<Employee> employees,
        IReadOnlyDictionary<Guid, string> skillNames,
        List<RosterWarning> warnings)
    {
        foreach (var line in lines)
        {
            foreach (var skillId in line.RequiredSkillIds.OrderBy(id => id))
            {
                if (!employees.Any(employee => employee.SkillIds.Contains(skillId)))
                {
                    warnings.Add(RosterWarnings.LineRequiresSkillNobodyHolds(line, SkillName(skillNames, skillId)));
                }
            }
        }
    }

    private static void CheckLinesHaveALeader(
        IEnumerable<ProductionLine> lines,
        IReadOnlyCollection<Employee> employees,
        RosterConfiguration configuration,
        IReadOnlySet<(Guid EmployeeId, Guid LineId)> blockedFrom,
        List<RosterWarning> warnings)
    {
        var eligibility = configuration.LeaderEligibilities
            .Select(entry => (entry.EmployeeId, entry.LineId))
            .ToHashSet();

        foreach (var line in lines)
        {
            var hasLeader = employees.Any(employee =>
                eligibility.Contains((employee.Id, line.Id))
                && !blockedFrom.Contains((employee.Id, line.Id))
                && Eligibility.HasRequiredSkills(employee, line));

            if (!hasLeader)
            {
                warnings.Add(RosterWarnings.LineHasNoEligibleLeader(line));
            }

            // A line asking for a second in charge that nobody may fill is the same class of
            // fault, and just as invisible until a week is generated.
            if (line.RequiredOperatingAssistants > 0
                && !configuration.OperatingAssistantEligibilities.Any(e => e.LineId == line.Id))
            {
                warnings.Add(RosterWarnings.LineHasNoEligibleOperatingAssistant(line));
            }
        }
    }

    private static void CheckMandatoryPreferencesFit(
        IEnumerable<ProductionLine> lines,
        IReadOnlyCollection<Employee> employees,
        RosterConfiguration configuration,
        List<RosterWarning> warnings)
    {
        var activeIds = employees.Select(employee => employee.Id).ToHashSet();

        foreach (var line in lines)
        {
            var mandatoryCount = configuration.Preferences.Count(preference =>
                preference.LineId == line.Id
                && preference.Type == PreferenceType.Mandatory
                && activeIds.Contains(preference.EmployeeId));

            if (mandatoryCount > line.RequiredHeadcount)
            {
                warnings.Add(RosterWarnings.TooManyMandatoryPreferencesForLine(line, mandatoryCount));
            }
        }
    }

    private static void CheckEmployeePreferences(
        IReadOnlyCollection<ProductionLine> lines,
        IEnumerable<Employee> employees,
        RosterConfiguration configuration,
        IReadOnlyDictionary<Guid, string> skillNames,
        IReadOnlySet<(Guid EmployeeId, Guid LineId)> blockedFrom,
        List<RosterWarning> warnings)
    {
        var linesById = lines.ToDictionary(line => line.Id);

        var preferencesByEmployee = configuration.Preferences
            .Where(preference => linesById.ContainsKey(preference.LineId))
            .GroupBy(preference => preference.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var employee in employees)
        {
            if (!preferencesByEmployee.TryGetValue(employee.Id, out var preferences))
            {
                continue;
            }

            if (lines.Count > 0 && lines.All(line => blockedFrom.Contains((employee.Id, line.Id))))
            {
                warnings.Add(RosterWarnings.EmployeeBlockedFromEveryLine(employee.Id));
            }

            var mandatory = preferences
                .Where(preference => preference.Type == PreferenceType.Mandatory)
                .OrderBy(preference => preference.Rank)
                .ThenBy(preference => linesById[preference.LineId].DisplayOrder)
                .ToList();

            if (mandatory.Count > 1)
            {
                warnings.Add(RosterWarnings.EmployeeMandatoryOnMultipleLines(
                    employee.Id,
                    linesById[mandatory[0].LineId]));
            }

            foreach (var preference in mandatory)
            {
                var line = linesById[preference.LineId];

                if (blockedFrom.Contains((employee.Id, line.Id)))
                {
                    warnings.Add(RosterWarnings.ContradictoryPreference(line, employee.Id));
                }

                foreach (var skillId in line.RequiredSkillIds.OrderBy(id => id))
                {
                    if (!employee.SkillIds.Contains(skillId))
                    {
                        warnings.Add(RosterWarnings.MandatoryPreferenceWithoutRequiredSkills(
                            line,
                            employee.Id,
                            SkillName(skillNames, skillId)));
                    }
                }
            }

            var duplicateRanks = preferences
                .Where(preference => preference.Type != PreferenceType.Blocked)
                .GroupBy(preference => preference.Rank)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(rank => rank);

            foreach (var rank in duplicateRanks)
            {
                warnings.Add(RosterWarnings.DuplicatePreferenceRank(employee.Id, rank));
            }
        }
    }

    private static HashSet<(Guid EmployeeId, Guid LineId)> BuildBlocked(RosterConfiguration configuration) =>
        configuration.Preferences
            .Where(preference => preference.Type == PreferenceType.Blocked)
            .Select(preference => (preference.EmployeeId, preference.LineId))
            .ToHashSet();

    private static string SkillName(IReadOnlyDictionary<Guid, string> skillNames, Guid skillId) =>
        skillNames.TryGetValue(skillId, out var name) ? name : skillId.ToString();
}
