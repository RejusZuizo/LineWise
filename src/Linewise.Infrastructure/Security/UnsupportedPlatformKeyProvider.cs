using Linewise.Application.Abstractions;

namespace Linewise.Infrastructure.Security;

/// <summary>
/// Refuses to produce a key, with an explanation. Registered in a release build on a
/// platform this product does not ship to.
/// </summary>
/// <remarks>
/// The alternative was to register nothing, which fails with "unable to resolve service for
/// type IDatabaseKeyProvider" — true, and useless to whoever is reading it. This costs
/// fifteen lines and answers the question the failure raises.
/// <para>
/// It throws when the key is asked for rather than when it is constructed, so that
/// composing the container still works. Tests substitute their own provider before
/// infrastructure is registered, and a container that cannot be built at all would take
/// them down with it.
/// </para>
/// </remarks>
public sealed class UnsupportedPlatformKeyProvider : IDatabaseKeyProvider
{
    public string GetKey() =>
        throw new PlatformNotSupportedException(
            "Linewise ships to Windows, where the database key is held by DPAPI. This is a "
            + "release build running on "
            + (OperatingSystem.IsLinux() ? "Linux" : "a platform that is not Windows")
            + ", which has no equivalent, and a release build deliberately carries no "
            + "weaker substitute. Build in Debug to develop on this machine.");
}
