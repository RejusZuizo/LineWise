using Linewise.Domain.Entities;

namespace Linewise.Application.Import;

/// <summary>How confidently a name on the sheet was tied to an employee.</summary>
public enum NameMatchOutcome
{
    /// <summary>Nobody plausible. The operator adds them or corrects the sheet.</summary>
    Unmatched = 0,

    /// <summary>One employee, after normalising and allowing for word order.</summary>
    Exact = 1,

    /// <summary>One employee, after forgiving a typo. Worth confirming before committing.</summary>
    Fuzzy = 2,

    /// <summary>More than one employee fits equally well. Only a person can choose.</summary>
    Ambiguous = 3,
}

/// <param name="Outcome">How the match went.</param>
/// <param name="EmployeeId">The employee, when exactly one fits.</param>
/// <param name="Candidates">Everyone who fitted, when more than one did.</param>
/// <param name="Distance">How many edits away the name was. Zero for an exact match.</param>
public sealed record NameMatch(
    NameMatchOutcome Outcome,
    Guid? EmployeeId,
    IReadOnlyList<Guid> Candidates,
    int Distance)
{
    public static NameMatch Unmatched { get; } = new(NameMatchOutcome.Unmatched, null, [], int.MaxValue);
}

/// <summary>
/// Ties a name written on a sheet to an employee, matching aliases as well as full names.
/// </summary>
public sealed class EmployeeNameMatcher
{
    private readonly Dictionary<string, HashSet<Guid>> _byNormalised = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Guid>> _byWordOrder = new(StringComparer.Ordinal);
    private readonly List<(string Sortable, Guid EmployeeId)> _everyForm = [];

    public EmployeeNameMatcher(IEnumerable<Employee> employees)
    {
        ArgumentNullException.ThrowIfNull(employees);

        foreach (var employee in employees.Where(employee => employee.IsActive))
        {
            // Aliases carry the same weight as the full name. They exist precisely because
            // the sheet calls somebody something the payroll system does not.
            foreach (var form in employee.Aliases.Prepend(employee.FullName))
            {
                var normalised = NameMatching.Normalise(form);

                if (normalised.Length == 0)
                {
                    continue;
                }

                var sortable = NameMatching.Sortable(form);
                var compact = NameMatching.SortableCompact(form);

                Index(_byNormalised, normalised, employee.Id);
                Index(_byWordOrder, sortable, employee.Id);
                Index(_byWordOrder, compact, employee.Id);
                _everyForm.Add((sortable, employee.Id));
            }
        }
    }

    public NameMatch Match(string? sheetName)
    {
        var normalised = NameMatching.Normalise(sheetName);

        if (normalised.Length == 0)
        {
            return NameMatch.Unmatched;
        }

        if (_byNormalised.TryGetValue(normalised, out var straight))
        {
            return Resolve(straight, NameMatchOutcome.Exact, 0);
        }

        // Surname first versus surname last, which is the single most common difference
        // between a payroll export and a sheet somebody maintains by hand.
        var sortable = NameMatching.Sortable(sheetName);

        if (_byWordOrder.TryGetValue(sortable, out var reordered))
        {
            return Resolve(reordered, NameMatchOutcome.Exact, 0);
        }

        // The same name with the punctuation elided rather than spaced out.
        if (_byWordOrder.TryGetValue(NameMatching.SortableCompact(sheetName), out var compacted))
        {
            return Resolve(compacted, NameMatchOutcome.Exact, 0);
        }

        var tolerance = NameMatching.ToleranceFor(sortable);

        if (tolerance == 0)
        {
            return NameMatch.Unmatched;
        }

        var best = int.MaxValue;
        var candidates = new HashSet<Guid>();

        foreach (var (candidate, employeeId) in _everyForm)
        {
            // Two strings differing in length by more than the tolerance cannot be within it,
            // and skipping them here avoids the distance calculation entirely.
            if (Math.Abs(candidate.Length - sortable.Length) > tolerance)
            {
                continue;
            }

            var distance = NameMatching.Distance(sortable, candidate);

            if (distance > tolerance)
            {
                continue;
            }

            if (distance < best)
            {
                best = distance;
                candidates.Clear();
            }

            if (distance == best)
            {
                candidates.Add(employeeId);
            }
        }

        return candidates.Count == 0
            ? NameMatch.Unmatched
            : Resolve(candidates, NameMatchOutcome.Fuzzy, best);
    }

    private static void Index(Dictionary<string, HashSet<Guid>> index, string key, Guid employeeId)
    {
        if (!index.TryGetValue(key, out var employees))
        {
            employees = [];
            index[key] = employees;
        }

        employees.Add(employeeId);
    }

    /// <summary>
    /// One candidate is a match. More than one is a question for the operator, even when the
    /// name matched perfectly: two people really can share a name, and picking the first
    /// would put somebody on the wrong line without anybody being asked.
    /// </summary>
    private static NameMatch Resolve(HashSet<Guid> candidates, NameMatchOutcome outcome, int distance)
    {
        var ordered = candidates.OrderBy(id => id).ToList();

        return ordered.Count == 1
            ? new NameMatch(outcome, ordered[0], ordered, distance)
            : new NameMatch(NameMatchOutcome.Ambiguous, null, ordered, distance);
    }
}
