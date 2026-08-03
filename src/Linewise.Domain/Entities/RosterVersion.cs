using Linewise.Domain.Enums;

namespace Linewise.Domain.Entities;

/// <summary>
/// One saved state of one week's roster. Publishing is an explicit act that increments the
/// version, so a sheet on the wall can be checked against the current version number and
/// found stale at a glance.
/// </summary>
public sealed record RosterVersion
{
    public required Guid Id { get; init; }

    /// <summary>The Monday the week starts on.</summary>
    public required DateOnly WeekStart { get; init; }

    /// <summary>Increments on each publish of the same week. Printed on the sheet.</summary>
    public required int VersionNumber { get; init; }

    public required RosterStatus Status { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? PublishedAtUtc { get; init; }

    /// <summary>The Windows account that published it. Not an authorisation check; a record.</summary>
    public string? PublishedBy { get; init; }
}
