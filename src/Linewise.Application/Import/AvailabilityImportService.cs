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
    private readonly IImportLayoutDetector _detector;
    private readonly IImportRepository _imports;
    private readonly IConfigurationRepository _configuration;
    private readonly IAvailabilityRepository _availability;
    private readonly IAuditLog _auditLog;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AvailabilityImportService(
        IAvailabilitySheetReader reader,
        IAvailabilityImportBuilder builder,
        IImportLayoutDetector detector,
        IImportRepository imports,
        IConfigurationRepository configuration,
        IAvailabilityRepository availability,
        IAuditLog auditLog,
        IClock clock,
        ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(imports);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        _reader = reader;
        _builder = builder;
        _detector = detector;
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

        // The template is tried first and wins whenever it works, so a layout somebody
        // configured is never quietly overruled. Detection is the rescue for the sheet the
        // template does not fit, which on a first run is every sheet. ADR 0017.
        if (result.Rows.Count == 0)
        {
            var detected = _detector.Detect(read.Sheet);

            if (detected.Found)
            {
                var rescued = _builder.Build(read.Sheet, detected.ApplyTo(template), configuration.Employees);

                if (rescued.Rows.Count > 0)
                {
                    return rescued with
                    {
                        Warnings =
                        [
                            .. read.Warnings,
                            ImportWarnings.LayoutDetected(
                                detected.HeaderRowIndex,
                                detected.NameColumnIndex,
                                detected.DateColumnCount),
                            .. rescued.Warnings,
                        ],
                        DetectedLayout = detected,
                    };
                }
            }
        }

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
        var aliasesLearned = 0;

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
                // Remembered. Pointing an unfamiliar spelling at somebody is a fact about
                // that person, not about this week's sheet, and the matcher already reads
                // aliases — so the same spelling matches by itself next week rather than
                // being handed back to the operator every Monday.
                aliasesLearned += await RememberAliasAsync(employeeId, row.SheetName, cancellationToken)
                    .ConfigureAwait(false)
                    ? 1
                    : 0;

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

        // Anything the manager set by hand in this range survives, and is reported rather
        // than left for somebody to discover. A sheet corrected and re-imported on a
        // Tuesday afternoon must not quietly put back the four people who rang in sick
        // that morning.
        var kept = await _availability
            .ReplaceImportedAsync(from, toExclusive, availabilities, cancellationToken)
            .ConfigureAwait(false);

        foreach (var record in kept)
        {
            warnings.Add(ImportWarnings.ManualAvailabilityKept(record.EmployeeId, record.Date));
        }

        if (aliasesLearned > 0)
        {
            warnings.Add(ImportWarnings.NamesLearned(aliasesLearned));
        }

        var manual = kept.Select(record => (record.EmployeeId, record.Date)).ToHashSet();

        // What went in, not what was offered. Reporting the sheet's row count as records
        // written would overstate it by exactly the number the manager had already
        // corrected, which is the number they most want to be right.
        var written = availabilities.Count(
            availability => !manual.Contains((availability.EmployeeId, availability.Date)));

        // What was worked out by reading the sheet is written back onto the template, so the
        // next sheet of the same shape is parsed rather than detected. This is the whole of
        // "it adapts": the guess is made once and then it is configuration like any other,
        // visible and editable rather than repeated silently every week.
        if (request.Parsed.DetectedLayout is { Found: true } detected)
        {
            var template = await _imports
                .GetTemplateAsync(request.TemplateId, cancellationToken)
                .ConfigureAwait(false);

            if (template is not null)
            {
                await _imports
                    .SaveTemplateAsync(detected.ApplyTo(template), cancellationToken)
                    .ConfigureAwait(false);

                warnings.Add(ImportWarnings.LayoutLearned(
                    detected.HeaderRowIndex,
                    detected.NameColumnIndex));
            }
        }

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
            written,
            temporariesAdded,
            unresolved,
            warnings);
    }

    /// <summary>
    /// Adds the spelling from the sheet to that employee's aliases, unless they already have
    /// it or it is simply their name.
    /// </summary>
    /// <returns>Whether anything was actually learned.</returns>
    /// <remarks>
    /// The alias list is personal data like the name itself, and this only ever adds a
    /// spelling of a name the sheet already carried. Nothing new about the person is
    /// recorded — the same string is simply kept rather than thrown away and asked for again.
    /// </remarks>
    private async Task<bool> RememberAliasAsync(
        Guid employeeId,
        string sheetName,
        CancellationToken cancellationToken)
    {
        var spelling = sheetName.Trim();

        if (string.IsNullOrEmpty(spelling))
        {
            return false;
        }

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(false);
        var employee = configuration.Employees.FirstOrDefault(person => person.Id == employeeId);

        if (employee is null
            || string.Equals(employee.FullName, spelling, StringComparison.CurrentCultureIgnoreCase)
            || employee.Aliases.Any(alias =>
                string.Equals(alias, spelling, StringComparison.CurrentCultureIgnoreCase)))
        {
            return false;
        }

        await _configuration
            .SaveEmployeeAsync(employee with { Aliases = [.. employee.Aliases, spelling] }, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
