using System.Diagnostics.CodeAnalysis;
using Linewise.Domain.Entities;
using Serilog.Core;
using Serilog.Events;

namespace Linewise.Infrastructure.Logging;

/// <summary>
/// Keeps employee names out of log files. Threat T6.
/// </summary>
/// <remarks>
/// <para>
/// Logging <c>{@Employee}</c> would otherwise write a name, every alias that person is
/// known by on the factory's spreadsheet, and their skills, into a file that is not
/// encrypted and outlives the session. That is personal data leaving the database for a
/// place nobody thinks of as holding any.
/// </para>
/// <para>
/// The identifier is kept, because an audit trail that cannot say who it is talking about
/// is not an audit trail. A Guid is only personal data to somebody who already has the
/// database, and somebody who has the database does not need the log.
/// </para>
/// <para>
/// <b>What this does not do.</b> It intercepts structured destructuring only. A name
/// interpolated into a message string — <c>$"Assigned {employee.FullName}"</c> — arrives as
/// an opaque string and no policy can pull it back out. That is a rule about how logging
/// calls are written, and this class cannot enforce it.
/// </para>
/// </remarks>
public sealed class PersonalDataDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        if (value is not Employee employee)
        {
            result = null;
            return false;
        }

        result = new StructureValue(
            [
                new LogEventProperty(nameof(Employee.Id), new ScalarValue(employee.Id)),
                new LogEventProperty(nameof(Employee.FullName), new ScalarValue(Redacted)),
                new LogEventProperty(nameof(Employee.IsActive), new ScalarValue(employee.IsActive)),
                new LogEventProperty(nameof(Employee.IsTemporary), new ScalarValue(employee.IsTemporary)),
            ],
            nameof(Employee));

        return true;
    }

    /// <summary>
    /// Says a value was removed rather than that it was absent. A log claiming an employee
    /// has no name reads as a bug and gets investigated as one.
    /// </summary>
    public const string Redacted = "[redacted]";
}
