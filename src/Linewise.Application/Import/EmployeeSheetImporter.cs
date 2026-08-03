using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <inheritdoc cref="IEmployeeSheetImporter"/>
public sealed class EmployeeSheetImporter : IEmployeeSheetImporter
{
    private readonly IAvailabilitySheetReader _reader;
    private readonly IConfigurationRepository _configuration;
    private readonly IAuditLog _auditLog;

    public EmployeeSheetImporter(
        IAvailabilitySheetReader reader,
        IConfigurationRepository configuration,
        IAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(auditLog);

        _reader = reader;
        _configuration = configuration;
        _auditLog = auditLog;
    }

    public async Task<EmployeeImportResult> ParseAsync(
        byte[] file,
        EmployeeSheetTemplate template,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(template);

        var read = await _reader
            .ReadAsync(file, template.WorksheetName, cancellationToken)
            .ConfigureAwait(false);

        if (read.Sheet is null)
        {
            return new EmployeeImportResult { Warnings = read.Warnings };
        }

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);
        var matcher = new EmployeeNameMatcher(configuration.Employees);
        var proposed = new List<ProposedEmployee>();
        var warnings = new List<RosterWarning>(read.Warnings);

        foreach (var row in read.Sheet.Rows
            .Where(row => row.RowIndex > template.HeaderRowIndex)
            .OrderBy(row => row.RowIndex))
        {
            var name = row.Cells.FirstOrDefault(cell => cell.ColumnIndex == template.NameColumnIndex)?.Text;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var aliases = ReadAliases(row, template);
            var match = matcher.Match(name);

            // Somebody already on file is listed and skipped rather than duplicated. Two
            // records for one person is how a roster ends up double booking them.
            proposed.Add(new ProposedEmployee(
                row.RowIndex,
                name.Trim(),
                aliases,
                match.Outcome is NameMatchOutcome.Exact ? match.EmployeeId : null));
        }

        if (proposed.Count == 0)
        {
            warnings.Add(ImportWarnings.NoRows(template.HeaderRowIndex));
        }

        return new EmployeeImportResult { Proposed = proposed, Warnings = warnings };
    }

    public async Task<int> CommitAsync(
        EmployeeImportResult parsed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parsed);

        var added = 0;

        foreach (var candidate in parsed.NewPeople)
        {
            await _configuration.SaveEmployeeAsync(
                new Employee
                {
                    Id = Guid.NewGuid(),
                    FullName = candidate.FullName,
                    Aliases = candidate.Aliases,
                    IsActive = true,
                },
                cancellationToken).ConfigureAwait(false);

            added++;
        }

        if (added > 0)
        {
            await _auditLog.AppendAsync(
                AuditAction.EmployeeAdded,
                $"{added} employees added from a list.",
                string.Empty,
                cancellationToken).ConfigureAwait(false);
        }

        return added;
    }

    private static IReadOnlyList<string> ReadAliases(RawRow row, EmployeeSheetTemplate template)
    {
        if (template.AliasColumnIndex is not { } column)
        {
            return [];
        }

        var text = row.Cells.FirstOrDefault(cell => cell.ColumnIndex == column)?.Text;

        return string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
