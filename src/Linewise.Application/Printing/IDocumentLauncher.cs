namespace Linewise.Application.Printing;

/// <summary>
/// Puts a generated document in front of the operator.
/// </summary>
/// <remarks>
/// The printer produces bytes and knows nothing about files (ADR notes on
/// <see cref="IRosterPrinter"/>). Somebody has to write those bytes down and hand them to
/// whatever opens a PDF, and doing it behind an interface keeps that decision out of the
/// view model and lets a test assert what was asked for without a printer existing.
/// </remarks>
public interface IDocumentLauncher
{
    /// <summary>
    /// Writes the document somewhere temporary and opens it for preview.
    /// </summary>
    /// <returns>The path written, so it can be shown to the operator or logged.</returns>
    Task<string> PreviewAsync(byte[] document, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the document and asks the operating system to print it.
    /// </summary>
    /// <remarks>
    /// Deliberately not Avalonia's printing. A PDF handed to the platform prints the same
    /// way from every application on that machine, which is what the operator already knows
    /// how to drive, and it keeps paper size and duplex out of this codebase entirely.
    /// </remarks>
    Task<string> PrintAsync(byte[] document, string fileName, CancellationToken cancellationToken = default);
}
