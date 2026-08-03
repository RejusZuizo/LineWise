namespace Linewise.Application.Abstractions;

/// <summary>
/// Who is at the keyboard, for the audit trail. There is no authentication and no
/// authorisation in this product: this records the Windows account, it does not check it.
/// </summary>
public interface ICurrentUser
{
    string Name { get; }
}
