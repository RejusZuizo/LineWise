namespace Linewise.Domain.Enums;

/// <summary>Paper the roster is printed on. Configurable because sites differ.</summary>
public enum PaperSize
{
    A4 = 0,

    /// <summary>Bigger paper, for a factory with more lines than fit legibly on A4.</summary>
    A3 = 1,

    Letter = 2,
}

/// <summary>Which way round the page goes.</summary>
public enum PageOrientation
{
    /// <summary>Suits a per line sheet, which is a short list.</summary>
    Portrait = 0,

    /// <summary>Suits the full sheet, which is wide.</summary>
    Landscape = 1,
}
