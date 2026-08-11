using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Linewise.Desktop.ViewModels;

/// <summary>Value converters used by the views.</summary>
public static class Converters
{
    /// <summary>
    /// Bold when true, normal otherwise. One of the three ways a line leader is marked,
    /// alongside the filled block and the word itself.
    /// </summary>
    public static readonly IValueConverter BoldWhenTrue = new FuncValueConverter<bool, FontWeight>(
        value => value ? FontWeight.Bold : FontWeight.Normal);

    /// <summary>
    /// Formats the count of cells carrying a mark nothing recognised. Through a resource,
    /// like every other string, rather than composed in the view.
    /// </summary>
    public static readonly IValueConverter UnrecognisedCells = new FuncValueConverter<int, string>(
        count => Resources.Strings.ImportUnrecognised(count));

    /// <summary>Excel's own row number, so the operator can go and look at the row.</summary>
    public static readonly IValueConverter RowLabel = new FuncValueConverter<int, string>(
        row => Resources.Strings.ImportRow(row));
}
