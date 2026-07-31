using Linewise.Domain.Enums;

namespace Linewise.Domain.Entities;

/// <summary>How one employee relates to one production line.</summary>
public sealed record LinePreference
{
    public required Guid EmployeeId { get; init; }

    public required Guid LineId { get; init; }

    /// <summary>
    /// 1 is first choice. Only meaningful for <see cref="PreferenceType.Preferred"/> and
    /// <see cref="PreferenceType.Mandatory"/>; ignored for
    /// <see cref="PreferenceType.Blocked"/>.
    /// </summary>
    public required int Rank { get; init; }

    public required PreferenceType Type { get; init; }
}
