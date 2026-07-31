using Linewise.Domain.Enums;

namespace Linewise.Domain.Rostering;

/// <summary>
/// Something the manager should know about a roster or the configuration behind it.
/// Business rule failures arrive here, never as exceptions.
/// </summary>
/// <remarks>
/// <see cref="Message"/> is resolved from a resource file and is for display. Anything
/// deciding behaviour, tests included, keys off <see cref="Code"/>. Employee names are
/// deliberately absent: the identifier is carried instead and resolved for display, so a
/// warning that reaches a log file carries no personal data.
/// </remarks>
public sealed record RosterWarning
{
    public required WarningSeverity Severity { get; init; }

    public required WarningCode Code { get; init; }

    public required string Message { get; init; }

    public Guid? LineId { get; init; }

    public Guid? EmployeeId { get; init; }

    /// <summary>The date the warning concerns. Null for configuration warnings.</summary>
    public DateOnly? Date { get; init; }
}
