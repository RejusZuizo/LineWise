using Linewise.Domain.Rostering;

namespace Linewise.Application.Import;

/// <summary>
/// Reads a workbook into the plain shape the importer works with.
/// </summary>
/// <remarks>
/// The implementation lives in infrastructure because opening a spreadsheet needs a library
/// and a file. This is also the trust boundary: the file arrives from outside, and nothing
/// past this interface should have to think about that.
/// </remarks>
public interface IAvailabilitySheetReader
{
    /// <summary>
    /// Reads a worksheet, or explains why it could not. Never throws for a bad file: a
    /// stack trace is not something an operator can act on.
    /// </summary>
    /// <remarks>
    /// Takes the file as bytes rather than as a stream. Partly because this layer is not
    /// allowed to know the file system exists, and partly because it is honest: the archive
    /// has to be inspected before it is opened and kept afterwards so the import can be
    /// re-run, so the whole file is in memory either way.
    /// <para>
    /// Parsing is processor work rather than I/O. The method is asynchronous because callers
    /// must not run it on the interface thread, not because it waits on anything.
    /// </para>
    /// </remarks>
    Task<SheetReadResult> ReadAsync(
        byte[] workbook,
        string? worksheetName,
        CancellationToken cancellationToken = default);
}

/// <param name="Sheet">The worksheet, or null when it could not be read.</param>
/// <param name="Warnings">What went wrong, or what was odd but survivable.</param>
public sealed record SheetReadResult(RawSheet? Sheet, IReadOnlyList<RosterWarning> Warnings)
{
    public bool Succeeded => Sheet is not null;
}
