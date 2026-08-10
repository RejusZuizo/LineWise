using Xunit;

namespace Linewise.Tests.Security;

/// <summary>
/// A test that only means anything on a platform with Unix file modes.
/// </summary>
/// <remarks>
/// Skipped rather than absent, so that a run on Windows reports what it did not check
/// instead of quietly reporting a smaller suite. The count dropping by five with no
/// explanation is how a platform gap goes unnoticed.
/// </remarks>
public sealed class PosixFactAttribute : FactAttribute
{
    public PosixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Requires Unix file modes. The shipped provider uses DPAPI here.";
        }
    }
}
