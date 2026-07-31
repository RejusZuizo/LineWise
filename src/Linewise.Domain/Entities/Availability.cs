using Linewise.Domain.Enums;

namespace Linewise.Domain.Entities;

/// <summary>
/// Whether one employee is available on one date, as read from the imported sheet. An
/// employee with no record for a date is treated as <see cref="AvailabilityStatus.Off"/>.
/// </summary>
public sealed record Availability
{
    public required Guid EmployeeId { get; init; }

    public required DateOnly Date { get; init; }

    public required AvailabilityStatus Status { get; init; }
}
