using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Linewise.Infrastructure.Printing;

/// <summary>
/// The visual decisions, in one place so the three layouts cannot drift apart.
/// </summary>
/// <remarks>
/// The sheet is read from several feet away by somebody walking past, in a building where
/// the printer is probably monochrome. Everything here follows from that: large type, heavy
/// rules between blocks, and no meaning carried by colour on its own.
/// </remarks>
internal static class RosterDocumentStyle
{
    /// <summary>Near neutral. Saturated colour is reserved for line accents, if enabled at all.</summary>
    public static string Ink => Colors.Grey.Darken4;

    public static string Muted => Colors.Grey.Darken1;

    public static string Rule => Colors.Grey.Medium;

    public static string LeaderBackground => Colors.Grey.Lighten3;

    public static PageSize PageSizeFor(PrintSettings settings)
    {
        var size = settings.PaperSize switch
        {
            PaperSize.A3 => PageSizes.A3,
            PaperSize.Letter => PageSizes.Letter,
            _ => PageSizes.A4,
        };

        return settings.Orientation == PageOrientation.Landscape ? size.Landscape() : size.Portrait();
    }

    /// <summary>
    /// Everything scales from the base size, so making the sheet readable from further away
    /// is one number in settings rather than a redesign.
    /// </summary>
    public static float Title(PrintSettings settings) => settings.BaseFontPoints * 1.9f;

    public static float Heading(PrintSettings settings) => settings.BaseFontPoints * 1.3f;

    public static float Body(PrintSettings settings) => settings.BaseFontPoints;

    public static float Small(PrintSettings settings) => settings.BaseFontPoints * 0.8f;

    /// <summary>
    /// How a line leader is marked. A filled block, a bold name and the word itself, so the
    /// mark survives a monochrome printer, a photocopy, and a reader who cannot distinguish
    /// the accent colours.
    /// </summary>
    public const string LeaderMark = "■";

    public const string LeaderLabel = "LEADER";
}
