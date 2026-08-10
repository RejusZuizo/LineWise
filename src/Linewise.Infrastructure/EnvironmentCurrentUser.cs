using Linewise.Application.Abstractions;

namespace Linewise.Infrastructure;

/// <inheritdoc cref="ICurrentUser"/>
/// <remarks>
/// Was <c>WindowsCurrentUser</c>, which claimed a platform dependency it never had:
/// <see cref="Environment.UserName"/> returns the logged in account on every platform .NET
/// runs on. Naming it after Windows meant the audit trail appeared to need a Windows
/// specific implementation, and the first attempt at running on Linux duly wrote a second
/// class that was identical.
/// <para>
/// The key provider genuinely is platform specific. This is not, and pretending otherwise
/// costs a class and misleads whoever reads the registration.
/// </para>
/// </remarks>
public sealed class EnvironmentCurrentUser : ICurrentUser
{
    public string Name => Environment.UserName;
}
