using Linewise.Domain.Enums;

namespace Linewise.Domain.Entities;

/// <summary>One shift on one date. A roster is generated per shift.</summary>
public sealed record Shift
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The calendar date the shift belongs to. A date, not an instant: the factory floor
    /// works in local days and a roster is never converted between time zones.
    /// </summary>
    public required DateOnly Date { get; init; }

    public required ShiftName Name { get; init; }
}
