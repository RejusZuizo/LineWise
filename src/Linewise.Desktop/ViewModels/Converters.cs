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
}
