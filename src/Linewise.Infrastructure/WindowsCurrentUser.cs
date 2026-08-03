using Linewise.Application.Abstractions;

namespace Linewise.Infrastructure;

/// <inheritdoc cref="ICurrentUser"/>
public sealed class WindowsCurrentUser : ICurrentUser
{
    public string Name => Environment.UserName;
}
