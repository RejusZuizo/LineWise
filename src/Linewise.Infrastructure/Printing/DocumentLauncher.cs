using System.Diagnostics;
using Linewise.Application.Printing;
using Linewise.Infrastructure.Logging;
using Serilog;

namespace Linewise.Infrastructure.Printing;

/// <inheritdoc cref="IDocumentLauncher"/>
public sealed class DocumentLauncher : IDocumentLauncher
{
    /// <summary>
    /// Under the user's own temporary folder, in a subfolder of ours.
    /// </summary>
    /// <remarks>
    /// Threat T10: an exported roster left in a shared location is a list of employees
    /// somebody did not mean to publish. A temporary folder scoped to this account is the
    /// least surprising default, and the user manual documents where it is.
    /// </remarks>
    public static string OutputDirectory => Path.Combine(Path.GetTempPath(), "Linewise");

    public Task<string> PreviewAsync(
        byte[] document,
        string fileName,
        CancellationToken cancellationToken = default) =>
        LaunchAsync(document, fileName, verb: null, cancellationToken);

    public Task<string> PrintAsync(
        byte[] document,
        string fileName,
        CancellationToken cancellationToken = default) =>
        // The print verb exists on Windows and nowhere else. On a development machine the
        // file opens in a viewer instead, which is the honest behaviour: this product is
        // not printed from Linux.
        LaunchAsync(document, fileName, OperatingSystem.IsWindows() ? "print" : null, cancellationToken);

    private static async Task<string> LaunchAsync(
        byte[] document,
        string fileName,
        string? verb,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        Directory.CreateDirectory(OutputDirectory);

        var path = Path.Combine(OutputDirectory, Safe(fileName));

        await File.WriteAllBytesAsync(path, document, cancellationToken).ConfigureAwait(false);

        // The path is logged through the redaction helper: a temporary folder sits under a
        // home directory, and a home directory is named after a person.
        Log.Information("Wrote {Bytes} bytes to {Path}.", document.Length, SafePath.ForLog(path));

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Verb = verb ?? string.Empty,
        })?.Dispose();

        return path;
    }

    /// <summary>
    /// Keeps a caller's filename from escaping the output folder. Nothing currently passes
    /// anything but a generated name, which is exactly when a control like this is cheap to
    /// add and easy to forget.
    /// </summary>
    private static string Safe(string fileName)
    {
        var name = Path.GetFileName(fileName);

        return string.IsNullOrWhiteSpace(name)
            ? "roster.pdf"
            : string.Concat(name.Split(Path.GetInvalidFileNameChars()));
    }
}
