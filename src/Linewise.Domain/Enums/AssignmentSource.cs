namespace Linewise.Domain.Enums;

/// <summary>Who placed an assignment.</summary>
public enum AssignmentSource
{
    /// <summary>Placed by the assignment engine.</summary>
    Auto = 0,

    /// <summary>Placed or moved by the manager. Always locked.</summary>
    Manual = 1,
}
