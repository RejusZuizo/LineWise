using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <summary>
/// The three step import: parse, review, commit.
/// </summary>
/// <remarks>
/// Parsing writes nothing. The operator sees what the file says, resolves anything the
/// matcher could not, and only then is anything stored. An import that half succeeded
/// leaves a week that is neither the old one nor the new one, which is worse than one that
/// never ran.
/// </remarks>
public interface IAvailabilityImportService
{
    /// <summary>Step one and two. Reads the file and produces something to review.</summary>
    Task<AvailabilityImportResult> ParseAsync(
        byte[] file,
        Guid templateId,
        CancellationToken cancellationToken = default);

    /// <summary>Step three. Only ever called after the operator has confirmed.</summary>
    Task<ImportCommitResult> CommitAsync(
        ImportCommitRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>What the operator confirmed, and the file it came from.</summary>
public sealed record ImportCommitRequest
{
    public required Guid TemplateId { get; init; }

    public required string FileName { get; init; }

    /// <summary>Kept with the import so a mapping fix can be replayed against it.</summary>
    public required byte[] FileContent { get; init; }

    public required AvailabilityImportResult Parsed { get; init; }

    /// <summary>What the operator decided about the rows the matcher could not settle.</summary>
    public IReadOnlyList<ImportResolution> Resolutions { get; init; } = [];

    /// <summary>Written to the audit trail alongside the import.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// One decision made during review.
/// </summary>
/// <param name="RowIndex">The row on the sheet being resolved.</param>
/// <param name="EmployeeId">
/// The employee the operator picked, when the row matched several or none.
/// </param>
/// <param name="AddAsTemporary">
/// Add this row's name as a temporary employee and use it. Food production runs heavily on
/// agency staff, and a weekly hard stop on unknown names would kill adoption on its own.
/// </param>
public sealed record ImportResolution(int RowIndex, Guid? EmployeeId, bool AddAsTemporary);

/// <param name="ImportId">The recorded import.</param>
/// <param name="AvailabilityRecordsWritten">How many days of availability were stored.</param>
/// <param name="TemporaryEmployeesAdded">How many people were added during review.</param>
/// <param name="RowsLeftUnresolved">
/// Rows nobody settled. Not an error: a name the operator chose to ignore is a decision.
/// </param>
/// <param name="Warnings">Anything worth saying about what was committed.</param>
public sealed record ImportCommitResult(
    Guid ImportId,
    int AvailabilityRecordsWritten,
    int TemporaryEmployeesAdded,
    int RowsLeftUnresolved,
    IReadOnlyList<RosterWarning> Warnings);
