using Linewise.Infrastructure.Logging;
using Xunit;

namespace Linewise.Tests.Logging;

/// <summary>
/// Threat T6, the other half: a home directory is named after a person.
/// </summary>
public sealed class SafePathTests
{
    [Fact]
    public void A_path_inside_the_user_profile_loses_the_profile()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(profile, "Downloads", "week 32.xlsx");

        var safe = SafePath.ForLog(path);

        Assert.DoesNotContain(profile, safe, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(SafePath.ProfilePlaceholder, safe, StringComparison.Ordinal);
    }

    /// <summary>
    /// Which sheet failed to parse is the question a log exists to answer, and a sheet is
    /// named after a week rather than a person.
    /// </summary>
    [Fact]
    public void The_filename_is_kept()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "week 32.xlsx");

        Assert.Contains("week 32.xlsx", SafePath.ForLog(path), StringComparison.Ordinal);
    }

    /// <summary>
    /// A shared drive is operational detail rather than personal data, and blanking it
    /// would make an import failure harder to diagnose for no gain.
    /// </summary>
    [Fact]
    public void A_path_outside_any_profile_is_left_alone()
    {
        const string path = "/mnt/production/rosters/week 32.xlsx";

        Assert.Equal(path, SafePath.ForLog(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_in_gives_nothing_out(string? path)
    {
        Assert.Equal(string.Empty, SafePath.ForLog(path));
    }
}
