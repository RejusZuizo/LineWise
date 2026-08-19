using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Rostering;

/// <inheritdoc cref="IEmployeeDirectory"/>
public sealed class EmployeeDirectory : IEmployeeDirectory
{
    private readonly IConfigurationRepository _configuration;
    private readonly IAuditLog _auditLog;

    public EmployeeDirectory(IConfigurationRepository configuration, IAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(auditLog);

        _configuration = configuration;
        _auditLog = auditLog;
    }

    public async Task<Employee> AddAsync(
        string fullName,
        bool isTemporary,
        CancellationToken cancellationToken = default)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            IsActive = true,
            IsTemporary = isTemporary,
        };

        await _configuration.SaveEmployeeAsync(employee, cancellationToken).ConfigureAwait(false);

        // Identifiers, never a name. The audit log is one of the places personal data would
        // otherwise accumulate quietly for years.
        await _auditLog.AppendAsync(
            AuditAction.EmployeeAdded,
            $"Added employee {employee.Id}{(isTemporary ? ", agency" : string.Empty)}.",
            "Added by hand.",
            cancellationToken).ConfigureAwait(false);

        return employee;
    }

    public Task DeactivateAsync(Guid employeeId, CancellationToken cancellationToken = default) =>
        SetActiveAsync(employeeId, active: false, cancellationToken);

    public Task ReactivateAsync(Guid employeeId, CancellationToken cancellationToken = default) =>
        SetActiveAsync(employeeId, active: true, cancellationToken);

    private async Task SetActiveAsync(Guid employeeId, bool active, CancellationToken cancellationToken)
    {
        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);
        var employee = configuration.Employees.FirstOrDefault(person => person.Id == employeeId);

        if (employee is null || employee.IsActive == active)
        {
            return;
        }

        // Rewritten rather than deleted. Everything else about them — their preferences,
        // their skills, the weeks they have already worked — stays exactly as it was.
        await _configuration
            .SaveEmployeeAsync(employee with { IsActive = active }, cancellationToken)
            .ConfigureAwait(false);

        await _auditLog.AppendAsync(
            active ? AuditAction.EmployeeAdded : AuditAction.EmployeeDeactivated,
            $"{(active ? "Reactivated" : "Deactivated")} employee {employeeId}.",
            active ? "Back on the roster." : "No longer on the roster.",
            cancellationToken).ConfigureAwait(false);
    }
}
