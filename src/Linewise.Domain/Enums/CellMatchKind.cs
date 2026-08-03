namespace Linewise.Domain.Enums;

/// <summary>What part of a cell a status rule looks at.</summary>
public enum CellMatchKind
{
    /// <summary>
    /// The text written in the cell, compared case insensitively and ignoring surrounding
    /// whitespace. Checked before colour, because text survives being copied, re-saved and
    /// emailed, and colour frequently does not.
    /// </summary>
    Text = 0,

    /// <summary>The cell's fill colour, as a hex string.</summary>
    FillColour = 1,
}
