using Linewise.Domain.Enums;

namespace Linewise.Domain.Entities;

/// <summary>
/// How the printed roster looks.
/// </summary>
/// <remarks>
/// Configurable rather than compiled in, because the sheet has to be legible on whatever
/// paper the site's printer holds and from wherever it ends up being pinned. A roster
/// nobody can read from where they stand is not a roster.
/// </remarks>
public sealed record PrintSettings
{
    public required Guid Id { get; init; }

    /// <summary>Printed in the header so a sheet found loose is identifiable.</summary>
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>Optional logo, as PNG bytes. Null prints the name alone.</summary>
    public byte[]? LogoPng { get; init; }

    public PaperSize PaperSize { get; init; } = PaperSize.A4;

    public PageOrientation Orientation { get; init; } = PageOrientation.Landscape;

    /// <summary>
    /// Base type size in points. Everything else scales from it, so making the sheet
    /// readable from further away is one number rather than a redesign.
    /// </summary>
    public int BaseFontPoints { get; init; } = 12;

    /// <summary>
    /// Whether to print each line's accent colour. Off by default: the sheet must work in
    /// monochrome, and a site with a black and white printer should not have to discover
    /// that the hard way.
    /// </summary>
    public bool UseAccentColours { get; init; }
}
