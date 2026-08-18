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

    /// <summary>
    /// Whether the sheet said this or the manager did. Defaults to
    /// <see cref="AvailabilitySource.Imported"/>, so every record written before this
    /// existed reads as what it was.
    /// </summary>
    /// <remarks>
    /// The engine does not read this. Who is available is the same question whoever
    /// answered it; provenance decides only what an import is allowed to overwrite.
    /// </remarks>
    public AvailabilitySource Source { get; init; } = AvailabilitySource.Imported;
}
