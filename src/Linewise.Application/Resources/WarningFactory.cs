using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Resources;

/// <summary>
/// Builds a warning with its text resolved from the resource file. One place, so the engine,
/// the validator and the importer cannot drift apart on how a warning is put together.
/// </summary>
internal static class WarningFactory
{
    public static RosterWarning Create(
        WarningCode code,
        WarningSeverity severity,
        object?[] messageArguments,
        Guid? lineId = null,
        Guid? employeeId = null,
        DateOnly? date = null) =>
        new()
        {
            Severity = severity,
            Code = code,
            Message = WarningMessages.Format(code, messageArguments),
            LineId = lineId,
            EmployeeId = employeeId,
            Date = date,
        };
}
