using Linewise.Desktop.ViewModels;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>
/// One warning as the strip shows it.
/// </summary>
/// <remarks>
/// ADR 0004: the engine carries identifiers rather than names so that a warning reaching a
/// log file carries no personal data. Resolution happens in this view model, at the last
/// possible moment, and goes no further than the screen.
/// </remarks>
public sealed class WarningViewModelTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);

    /// <summary>
    /// The word, not a colour. A strip distinguishing severity by red and grey tells some
    /// readers nothing, and the source spreadsheet already proves this factory's data is
    /// full of red and green.
    /// </summary>
    [Theory]
    [InlineData(WarningSeverity.Error, "Error")]
    [InlineData(WarningSeverity.Notice, "Notice")]
    public void Severity_is_carried_by_a_word(WarningSeverity severity, string expected)
    {
        var warning = View(new RosterWarning
        {
            Severity = severity,
            Code = WarningCode.LineUnderHeadcount,
            Message = "A line is short.",
        });

        Assert.Equal(expected, warning.SeverityLabel);
    }

    [Fact]
    public void A_warning_about_a_line_on_a_day_says_both()
    {
        var warning = View(
            new RosterWarning
            {
                Severity = WarningSeverity.Error,
                Code = WarningCode.LineUnderHeadcount,
                Message = "Ovens has 2 of the 3 people it needs.",
                LineId = Guid.NewGuid(),
                Date = Monday,
            },
            lineName: "Ovens");

        Assert.Contains("Ovens", warning.Context, StringComparison.Ordinal);
        Assert.Contains("Mon", warning.Context, StringComparison.Ordinal);
    }

    /// <summary>
    /// A configuration warning concerns no particular day. Showing today's date would be a
    /// claim the warning does not make.
    /// </summary>
    [Fact]
    public void A_warning_with_no_date_does_not_invent_one()
    {
        var warning = View(
            new RosterWarning
            {
                Severity = WarningSeverity.Error,
                Code = WarningCode.LineHasNoEligibleLeader,
                Message = "Nobody is eligible to lead Ovens.",
                LineId = Guid.NewGuid(),
            },
            lineName: "Ovens");

        Assert.Equal("Ovens", warning.Context);
    }

    [Fact]
    public void A_warning_about_nothing_in_particular_has_no_context()
    {
        var warning = View(new RosterWarning
        {
            Severity = WarningSeverity.Notice,
            Code = WarningCode.EmployeeUnassigned,
            Message = "Somebody was available and not placed.",
        });

        Assert.Equal(string.Empty, warning.Context);
    }

    private static WarningViewModel View(
        RosterWarning warning,
        string? lineName = null,
        string? employeeName = null) =>
        new(warning, lineName, employeeName);
}
