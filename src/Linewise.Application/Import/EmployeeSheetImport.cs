using Linewise.Domain.Entities;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <summary>Where the names live on a simple list of employees.</summary>
/// <param name="WorksheetName">Null takes the first sheet.</param>
/// <param name="HeaderRowIndex">Rows above and including this are ignored.</param>
/// <param name="NameColumnIndex">Column holding the full name.</param>
/// <param name="AliasColumnIndex">
/// Optional column of other spellings, separated by semicolons. Populating this up front
/// saves the same names being resolved by hand every week.
/// </param>
public sealed record EmployeeSheetTemplate(
    string? WorksheetName,
    int HeaderRowIndex,
    int NameColumnIndex,
    int? AliasColumnIndex);

/// <param name="RowIndex">Row on the sheet.</param>
/// <param name="FullName">The name as written.</param>
/// <param name="Aliases">Other spellings, if the sheet supplied any.</param>
/// <param name="AlreadyOnFile">
/// The existing employee this row matches, when it matches one. Those are skipped rather
/// than creating a second record for somebody who is already there.
/// </param>
public sealed record ProposedEmployee(
    int RowIndex,
    string FullName,
    IReadOnlyList<string> Aliases,
    Guid? AlreadyOnFile);

public sealed record EmployeeImportResult
{
    public IReadOnlyList<ProposedEmployee> Proposed { get; init; } = [];

    public IReadOnlyList<RosterWarning> Warnings { get; init; } = [];

    public IEnumerable<ProposedEmployee> NewPeople =>
        Proposed.Where(candidate => candidate.AlreadyOnFile is null);

    public IEnumerable<ProposedEmployee> AlreadyKnown =>
        Proposed.Where(candidate => candidate.AlreadyOnFile is not null);
}

/// <summary>
/// Reads a plain list of names so a new installation does not begin with somebody typing a
/// hundred and fifty people into a form.
/// </summary>
public interface IEmployeeSheetImporter
{
    Task<EmployeeImportResult> ParseAsync(
        byte[] file,
        EmployeeSheetTemplate template,
        CancellationToken cancellationToken = default);

    /// <summary>Adds everybody not already on file. Returns how many were added.</summary>
    Task<int> CommitAsync(
        EmployeeImportResult parsed,
        CancellationToken cancellationToken = default);
}
