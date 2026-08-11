using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Linewise.Desktop.Services;

/// <summary>Asks the operator for a file.</summary>
/// <remarks>
/// Behind an interface for the same reason the dialog service is: a view model that reaches
/// for <c>StorageProvider</c> cannot be tested without a window.
/// </remarks>
public interface IFilePicker
{
    /// <summary>Returns the chosen path, or null if the operator changed their mind.</summary>
    Task<string?> PickSpreadsheetAsync();
}

/// <inheritdoc cref="IFilePicker"/>
public sealed class FilePicker : IFilePicker
{
    public async Task<string?> PickSpreadsheetAsync()
    {
        if (Owner?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Resources.Strings.ChooseSheet,
            AllowMultiple = false,
            FileTypeFilter =
            [
                // .xlsx only. The reader is a ClosedXML reader, and offering .xls or .csv
                // here would be an invitation to a failure further in, where the message is
                // worse and the operator has already spent the effort of finding the file.
                new FilePickerFileType("Excel workbook")
                {
                    Patterns = ["*.xlsx"],
                    MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
                },
            ],
        }).ConfigureAwait(true);

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private static Avalonia.Controls.Window? Owner =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
}
