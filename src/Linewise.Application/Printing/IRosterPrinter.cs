using Linewise.Domain.Entities;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Printing;

/// <summary>
/// Produces the sheets that go on the wall.
/// </summary>
/// <remarks>
/// Returns bytes rather than writing files. This layer is not allowed to know the file
/// system exists, and the caller decides whether the result is saved, previewed or sent
/// straight to a printer.
/// </remarks>
public interface IRosterPrinter
{
    /// <summary>One page per date and shift, showing every line. The main wall sheet.</summary>
    Task<byte[]> PrintFullSheetAsync(PrintRequest request, CancellationToken cancellationToken = default);

    /// <summary>One page per line, for pinning at that line rather than in the office.</summary>
    Task<byte[]> PrintPerLineSheetsAsync(PrintRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// A compact page showing only what changed, for pinning beside a roster that is already
    /// on the wall. Reprinting the lot every time somebody rings in sick is how a wall ends
    /// up with four versions of Tuesday on it.
    /// </summary>
    Task<byte[]> PrintAmendmentSlipAsync(
        AmendmentRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Everything a layout needs. Identifiers are resolved to names here, not deeper.</summary>
public sealed record PrintRequest
{
    public required RosterWeek Roster { get; init; }

    /// <summary>Printed in the header, so a stale sheet on the wall is spottable.</summary>
    public required RosterVersion Version { get; init; }

    public required IReadOnlyList<ProductionLine> Lines { get; init; }

    public required IReadOnlyList<Employee> Employees { get; init; }

    public required IReadOnlyList<Shift> Shifts { get; init; }

    public required PrintSettings Settings { get; init; }
}

/// <param name="Current">The roster as it now stands.</param>
/// <param name="Previous">What was printed last time, to compare against.</param>
public sealed record AmendmentRequest(PrintRequest Current, RosterWeek Previous);
