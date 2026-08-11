using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Import;
using Linewise.Desktop.Resources;
using Linewise.Desktop.Services;
using Linewise.Infrastructure.Logging;
using Serilog;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The three step import: choose a file, review what it says, commit.
/// </summary>
/// <remarks>
/// Parsing writes nothing. The operator sees what the sheet says, decides about the names
/// the matcher could not settle, and only then is anything stored. An import that half
/// succeeded leaves a week that is neither the old one nor the new one, which is worse than
/// one that never ran.
/// </remarks>
public sealed partial class ImportViewModel : ObservableObject
{
    private readonly IAvailabilityImportService? _import;
    private readonly IImportRepository? _templates;
    private readonly IFilePicker? _picker;

    private byte[]? _fileContent;
    private Guid _templateId;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private AvailabilityImportResult? _parsed;

    public ImportViewModel(
        IAvailabilityImportService import,
        IImportRepository templates,
        IFilePicker picker)
    {
        _import = import;
        _templates = templates;
        _picker = picker;
    }

    /// <summary>For the Avalonia designer, which cannot resolve from the container.</summary>
    public ImportViewModel()
    {
    }

    /// <summary>
    /// Rows the matcher could not settle: nobody matched, or several did. Never dropped
    /// silently, because a dropped row is somebody who does not appear on the wall sheet.
    /// </summary>
    public ObservableCollection<UnresolvedRowViewModel> NeedingAttention { get; } = [];

    public bool HasParsed => Parsed is not null;

    public bool CanCommit => Parsed is { HasAnythingToCommit: true } && !IsBusy;

    public int MatchedCount => Parsed?.Matched.Count() ?? 0;

    public int DateCount => Parsed?.Dates.Count ?? 0;

    public int UnrecognisedCellCount =>
        Parsed?.Rows.SelectMany(row => row.Cells).Count(cell => !cell.Recognised) ?? 0;

    public string Summary => Parsed is null
        ? string.Empty
        : Strings.ImportSummary(MatchedCount, NeedingAttention.Count, DateCount);

    [RelayCommand]
    private async Task ChooseFileAsync(CancellationToken cancellationToken)
    {
        if (_picker is null || _import is null || _templates is null)
        {
            return;
        }

        var path = await _picker.PickSpreadsheetAsync().ConfigureAwait(true);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        IsBusy = true;

        try
        {
            FileName = Path.GetFileName(path);
            _fileContent = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(true);
            _templateId = await EnsureTemplateAsync(cancellationToken).ConfigureAwait(true);

            // The path goes through the redaction helper: a spreadsheet lives under a home
            // directory, and a home directory is named after a person.
            Log.Information(
                "Parsing {Path}, {Bytes} bytes.",
                SafePath.ForLog(path),
                _fileContent.Length);

            Parsed = await _import
                .ParseAsync(_fileContent, _templateId, cancellationToken)
                .ConfigureAwait(true);

            NeedingAttention.Clear();

            foreach (var row in Parsed.NeedingAttention)
            {
                NeedingAttention.Add(new UnresolvedRowViewModel(row));
            }

            Status = Parsed.HasAnythingToCommit
                ? Strings.ImportReadyToReview
                : Strings.ImportNothingUsable;

            Log.Information(
                "Parsed {Rows} rows over {Dates} dates: {Matched} matched, {Unresolved} needing attention.",
                Parsed.Rows.Count,
                Parsed.Dates.Count,
                MatchedCount,
                NeedingAttention.Count);
        }
        catch (Exception exception)
        {
            // A malformed workbook is an ordinary thing for a person to hand over, and it
            // gets a sentence rather than a stack trace.
            Log.Error(exception, "Could not read the sheet.");
            Status = Strings.ImportCouldNotRead;
            Parsed = null;
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    [RelayCommand]
    private async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_import is null || Parsed is null || _fileContent is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _import.CommitAsync(
                new ImportCommitRequest
                {
                    TemplateId = _templateId,
                    FileName = FileName,
                    FileContent = _fileContent,
                    Parsed = Parsed,
                    Resolutions =
                    [
                        .. NeedingAttention
                            .Where(row => row.AddAsTemporary)
                            .Select(row => new ImportResolution(row.RowIndex, null, AddAsTemporary: true)),
                    ],
                    Reason = Strings.ImportReason(FileName),
                },
                cancellationToken).ConfigureAwait(true);

            Committed = true;

            Status = Strings.ImportCommitted(
                result.AvailabilityRecordsWritten,
                result.TemporaryEmployeesAdded);

            Log.Information(
                "Imported {Records} availability records, added {Temporary} temporary employees, "
                + "{Unresolved} rows left unresolved.",
                result.AvailabilityRecordsWritten,
                result.TemporaryEmployeesAdded,
                result.RowsLeftUnresolved);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not commit the import.");
            Status = Strings.ImportCouldNotCommit;
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    /// <summary>Whether anything was written, so the window behind knows to redraw.</summary>
    public bool Committed { get; private set; }

    /// <summary>
    /// Adds every unresolved row as a temporary employee in one go.
    /// </summary>
    /// <remarks>
    /// Food production runs heavily on agency staff, and a first import has nobody on file
    /// at all, so the common case is every row unmatched. Ticking sixty boxes by hand to
    /// get started is the sort of thing that decides whether a tool is used twice.
    /// </remarks>
    [RelayCommand]
    private void AddAllAsTemporary()
    {
        foreach (var row in NeedingAttention)
        {
            row.AddAsTemporary = true;
        }
    }

    private async Task<Guid> EnsureTemplateAsync(CancellationToken cancellationToken)
    {
        var existing = await _templates!.GetTemplatesAsync(cancellationToken).ConfigureAwait(true);

        if (existing.Count > 0)
        {
            return existing[0].Id;
        }

        // No screen builds templates until phase 7. Without one there is nothing to parse
        // against, so a first import creates the default rather than refusing.
        var template = ImportTemplateDefaults.Create();
        await _templates.SaveTemplateAsync(template, cancellationToken).ConfigureAwait(true);

        Log.Information("Created the default import template {TemplateId}.", template.Id);

        return template.Id;
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasParsed));
        OnPropertyChanged(nameof(CanCommit));
        OnPropertyChanged(nameof(MatchedCount));
        OnPropertyChanged(nameof(DateCount));
        OnPropertyChanged(nameof(UnrecognisedCellCount));
        OnPropertyChanged(nameof(Summary));
        CommitCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>A row the matcher could not settle, and what the operator decided about it.</summary>
public sealed partial class UnresolvedRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _addAsTemporary;

    public UnresolvedRowViewModel(ImportedRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        RowIndex = row.RowIndex;
        SheetName = row.SheetName;
        Outcome = row.Match.Outcome.ToString();
    }

    /// <summary>Excel's own row number, so the operator can go and look at it.</summary>
    public int RowIndex { get; }

    /// <summary>
    /// The name as written on the sheet. Personal data, held only as long as this window is
    /// open, and never written to a log.
    /// </summary>
    public string SheetName { get; }

    public string Outcome { get; }
}
