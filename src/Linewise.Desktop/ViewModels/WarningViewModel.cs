using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// One warning, with its identifiers resolved to names for display.
/// </summary>
/// <remarks>
/// The engine deliberately carries identifiers rather than names, so a warning reaching a
/// log file carries no personal data (ADR 0004). Resolution happens here, at the last
/// possible moment, and goes no further than the screen.
/// </remarks>
public sealed class WarningViewModel
{
    public WarningViewModel(RosterWarning warning, string? lineName, string? employeeName)
    {
        ArgumentNullException.ThrowIfNull(warning);

        Severity = warning.Severity;
        Code = warning.Code;
        Message = warning.Message;
        Date = warning.Date;
        LineName = lineName;
        EmployeeName = employeeName;
    }

    public WarningSeverity Severity { get; }

    public WarningCode Code { get; }

    public string Message { get; }

    public DateOnly? Date { get; }

    public string? LineName { get; }

    public string? EmployeeName { get; }

    public bool IsError => Severity == WarningSeverity.Error;

    /// <summary>
    /// The word, not a colour and not an icon alone. Roughly eight percent of men have a
    /// colour vision deficiency, and a warnings strip that distinguishes severity by red
    /// and grey tells some readers nothing at all.
    /// </summary>
    public string SeverityLabel => IsError ? "Error" : "Notice";

    /// <summary>
    /// Where it applies, when that is known. A configuration warning concerns no
    /// particular day and says so by omission rather than by showing today's date.
    /// </summary>
    public string Context
    {
        get
        {
            var parts = new List<string>(2);

            if (Date is { } date)
            {
                parts.Add(date.ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture));
            }

            if (!string.IsNullOrEmpty(LineName))
            {
                parts.Add(LineName);
            }

            return string.Join(" · ", parts);
        }
    }
}
