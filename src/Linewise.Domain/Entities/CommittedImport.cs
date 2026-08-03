namespace Linewise.Domain.Entities;

/// <summary>
/// A record that a sheet was imported, and what it covered.
/// </summary>
/// <remarks>
/// The file itself is kept alongside this. When somebody discovers a fortnight later that a
/// colour was mapped to the wrong status, the fix is to correct the template and re-run the
/// original file, not to ask the office for a copy of a spreadsheet that has since been
/// overwritten.
/// </remarks>
public sealed record CommittedImport
{
    public required Guid Id { get; init; }

    public required Guid ImportTemplateId { get; init; }

    /// <summary>The file's name as supplied. Not a path: a path can carry a user's name.</summary>
    public required string FileName { get; init; }

    public required DateTime ImportedAtUtc { get; init; }

    /// <summary>The Windows account that ran it. A record, not a permission check.</summary>
    public required string ImportedBy { get; init; }

    public required DateOnly FirstDate { get; init; }

    /// <summary>Exclusive, so a five day week ends on the following Monday.</summary>
    public required DateOnly EndDateExclusive { get; init; }

    public required int RowsImported { get; init; }

    /// <summary>How many rows the operator had to resolve by hand.</summary>
    public int RowsResolvedManually { get; init; }
}
