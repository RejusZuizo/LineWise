namespace Linewise.Domain.Enums;

/// <summary>What kind of change an audit entry records.</summary>
public enum AuditAction
{
    RosterGenerated = 1,
    RosterEdited = 2,
    RosterPublished = 3,
    EmployeeAdded = 4,
    EmployeeDeactivated = 5,
    ConfigurationChanged = 6,
    ImportCommitted = 7,
    BackupRestored = 8,
}
