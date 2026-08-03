using Linewise.Application.Abstractions;
using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <inheritdoc cref="IAvailabilityImportService"/>
public sealed class AvailabilityImportService : IAvailabilityImportService
{
    private readonly IAvailabilitySheetReader _reader;
    private readonly IAvailabilityImportBuilder _builder;
    private readonly IImportRepository _imports;
    private readonly IConfigurationRepository _configuration;
    private readonly IAvailabilityRepository _availability;
    private readonly IAuditLog _auditLog;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AvailabilityImportService(
        IAvailabilitySheetReader reader,
        IAvailabilityImportBuilder builder,
        IImportRepository imports,
        IConfigurationRepository configuration,
        IAvailabilityRepository availability,
        IAuditLog auditLog,
        IClock clock,
        ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(imports);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        _reader = reader;
        _builder = builder;
        _imports = imports;
        _configuration = configuration;
        _availability = availability;
        _auditLog = auditLog;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<AvailabilityImportResult> ParseAsync(
        byte[] file,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var template = await _imports.GetTemplateAsync(templateId, cancellationToken).ConfigureAwait(false);

        if (template is null)
        {
            return new AvailabilityImportResult { Warnings = [ImportWarnings.NotAWorkbook()] };
        }

        var read = await _reader
            .ReadAsync(file, template.WorksheetName, cancellationToken)
            .ConfigureAwait(false);

        if (read.Sheet is null)
        {
            return new AvailabilityImportResult { Warnings = read.Warnings };
        }

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);
        var result = _builder.Build(read.Sheet, template, configuration.Employees);

        // Nothing is written. This is the whole point of parsing as its own step.
        return result with { Warnings = [.. read.Warnings, .. result.Warnings] };
    }

    public async Task<ImportCommitResult> CommitAsync(
        ImportCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<RosterWarning>();
        var resolutions = request.Resolutions.ToDictionary(resolution => resolution.RowIndex);
        var temporariesAdded = 0;

        var rows = new List<ImportedRow>();

        foreach (var row in request.Parsed.Rows)
        {
            if (!resolutions.TryGetValue(row.RowIndex, out var resolution))
            {
                rows.Add(row);
                continue;
            }

            if (resolution.AddAsTemporary)
            {
                var temporary = new Employee
                {
                    Id = Guid.NewGuid(),
                    FullName = row.SheetName,
                    IsActive = true,
                    IsTemporary = true,
                };

                await _configuration.SaveEmployeeAsync(temporary, cancellationToken).ConfigureAwait(false);
                temporariesAdded++;

                rows.Add(row with { Match = new NameMatch(NameMatchOutcome.Exact, temporary.Id, [temporary.Id], 0) });
                continue;
            }

            if (resolution.EmployeeId is { } employeeId)
            {
                rows.Add(row with { Match = new NameMatch(NameMatchOutcome.Exact, employeeId, [employeeId], 0) });
                continue;
            }

            rows.Add(row);
        }

        var resolved = request.Parsed with { Rows = rows };
        var availabilities = resolved.ToAvailabilities();
        var unresolved = resolved.NeedingAttention.Count();

        // Pointing an unfamiliar name at somebody already on the sheet is a reasonable thing
        // for an operator to do, and it puts one person on two rows. The first row wins and
        // this says so, rather than the write failing on a duplicate key.
        foreach (var employeeId in resolved.EmployeesOnMoreThanOneRow)
        {
            warnings.Add(ImportWarnings.EmployeeOnMoreThanOneRow(employeeId));
        }

        if (unresolved > 0)
        {
            // Committing anyway is deliberate. A manager who cannot get four fifths of a week
            // onto the wall because one agency name is unfamiliar will go back to the
            // spreadsheet and never come back.
            warnings.Add(ImportWarnings.RowsLeftUnresolved(unresolved));
        }

        var from = resolved.Dates.Min();
        var toExclusive = resolved.Dates.Max().AddDays(1);

        await _availability
            .ReplaceAsync(from, toExclusive, availabilities, cancellationToken)
            .ConfigureAwait(false);

        var import = new CommittedImport
        {
            Id = Guid.NewGuid(),
            ImportTemplateId = request.TemplateId,
            FileName = request.FileName,
            ImportedAtUtc = _clock.UtcNow,
            ImportedBy = _currentUser.Name,
            FirstDate = from,
            EndDateExclusive = toExclusive,
            RowsImported = resolved.Matched.Count(),
            RowsResolvedManually = request.Resolutions.Count,
        };

        await _imports.RecordAsync(import, request.FileContent, cancellationToken).ConfigureAwait(false);

        await _auditLog.AppendAsync(
            AuditAction.ImportCommitted,
            $"Imported {import.RowsImported} rows covering {from:yyyy-MM-dd} to {toExclusive:yyyy-MM-dd}.",
            request.Reason,
            cancellationToken).ConfigureAwait(false);

        return new ImportCommitResult(
            import.Id,
            availabilities.Count,
            temporariesAdded,
            unresolved,
            warnings);
    }
}
