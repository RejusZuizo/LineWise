using Linewise.Domain.Entities;

namespace Linewise.Application.Import;

/// <summary>Stores import templates and what has been imported.</summary>
public interface IImportRepository
{
    Task<ImportTemplate?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task SaveTemplateAsync(ImportTemplate template, CancellationToken cancellationToken = default);

    /// <summary>Records a committed import and keeps the file that produced it.</summary>
    Task RecordAsync(
        CommittedImport import,
        byte[] fileContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The bytes of a previously imported file, so it can be run again after a mapping fix.
    /// Fetched separately because a spreadsheet has no business being loaded every time
    /// somebody lists what has been imported.
    /// </summary>
    Task<byte[]?> GetFileAsync(Guid importId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommittedImport>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default);
}
