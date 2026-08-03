namespace Linewise.Domain.Enums;

/// <summary>Whether a roster is still being worked on or has been posted.</summary>
public enum RosterStatus
{
    /// <summary>Being edited. Autosaved continuously, and safe to regenerate.</summary>
    Draft = 0,

    /// <summary>
    /// Published. Somebody has printed this and pinned it to a wall, so a later change
    /// means a new version and a stale sheet to replace.
    /// </summary>
    Published = 1,
}
