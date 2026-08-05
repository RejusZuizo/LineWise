using System.Globalization;
using Linewise.Application;
using Linewise.Application.Rostering;
using Linewise.Application.Validation;
using Linewise.ConsoleHarness;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Microsoft.Extensions.DependencyInjection;

// Throwaway harness. It exists so the engine can be watched working before there is a
// window to look at, and it goes away in phase 5. Nothing but wiring and printing here.

var services = new ServiceCollection()
    .AddLinewiseApplication()
    .BuildServiceProvider();

var engine = services.GetRequiredService<IAssignmentEngine>();
var validator = services.GetRequiredService<IRosterRuleValidator>();

var request = FakeFactory.Build();
var employeeNames = request.Configuration.Employees.ToDictionary(e => e.Id, e => e.FullName);
var lineNames = request.Configuration.Lines.ToDictionary(l => l.Id, l => l.Name);

Console.WriteLine("Linewise engine harness");
Console.WriteLine(Format("Week beginning {0:dddd d MMMM yyyy}", request.WeekStart));
Console.WriteLine();

Console.WriteLine("Rule check");
var issues = validator.Validate(request.Configuration);

if (issues.Count == 0)
{
    Console.WriteLine("  nothing impossible");
}
else
{
    foreach (var issue in issues)
    {
        Console.WriteLine("  " + Describe(issue));
    }
}

Console.WriteLine();

var started = DateTime.UtcNow;
var roster = engine.Generate(request);
var elapsed = DateTime.UtcNow - started;

foreach (var day in roster.Days)
{
    Console.WriteLine(Format("{0:dddd d MMMM}", day.Date));

    foreach (var line in request.Configuration.Lines.OrderBy(l => l.DisplayOrder))
    {
        var onLine = day.Assignments
            .Where(assignment => assignment.LineId == line.Id)
            .OrderByDescending(assignment => assignment.Role == AssignmentRole.LineLeader)
            .Select(assignment => (assignment.Role == AssignmentRole.LineLeader ? "* " : "  ")
                + employeeNames[assignment.EmployeeId]
                + "  " + Because(assignment.Explanation))
            .ToList();

        Console.WriteLine(Format("  {0,-14} {1} of {2}", line.Name, onLine.Count, line.RequiredHeadcount));

        foreach (var entry in onLine)
        {
            Console.WriteLine("     " + entry);
        }
    }

    if (day.Warnings.Count > 0)
    {
        Console.WriteLine("  Warnings");

        foreach (var warning in day.Warnings)
        {
            Console.WriteLine("    " + Describe(warning));
        }
    }

    Console.WriteLine();
}

Console.WriteLine("* marks the line leader.");
Console.WriteLine(Format(
    "{0} assignments, {1} warnings, generated in {2:0} ms.",
    roster.AllAssignments.Count(),
    roster.AllWarnings.Count(),
    elapsed.TotalMilliseconds));

// The warning carries an identifier rather than a name, so the name is resolved here at
// the point of display. That is the whole point: nothing upstream holds personal data.
string Describe(RosterWarning warning)
{
    var who = warning.EmployeeId is { } employeeId && employeeNames.TryGetValue(employeeId, out var name)
        ? name + ": "
        : string.Empty;

    return Format("[{0}] {1}{2}", warning.Severity, who, warning.Message);
}

static string Because(AssignmentExplanation explanation) => explanation.Rule switch
{
    PlacementRule.ManualOverride => "(placed by hand)",
    PlacementRule.MandatoryPreference => "(required on this line)",
    PlacementRule.LeaderSelection => "(due to lead)",
    PlacementRule.PreferenceRank => Format("(choice {0})", explanation.PreferenceRank),
    PlacementRule.Backfill => "(filling a gap)",
    _ => string.Empty,
};

static string Format(string template, params object?[] arguments) =>
    string.Format(CultureInfo.CurrentCulture, template, arguments);
