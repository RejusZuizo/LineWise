using Linewise.Domain.Enums;
using Xunit;

namespace Linewise.Tests;

/// <summary>
/// Every warning code is its own number.
/// </summary>
/// <remarks>
/// Not pedantry. The message for a warning is looked up by <c>code.ToString()</c>, and an
/// enum member sharing a value with another returns the name of whichever was declared
/// first. A duplicate therefore fetches somebody else's message, and if that message takes
/// more arguments than the caller supplies it throws a FormatException from inside the
/// validator.
/// <para>
/// That is not hypothetical: it shipped. LineHasNoEligibleOperatingAssistant was added as
/// 105, which MandatoryPreferenceWithoutRequiredSkills already held, and the validator
/// threw whenever a line asked for operating assistants nobody was permitted to be.
/// </para>
/// </remarks>
public sealed class WarningCodeTests
{
    [Fact]
    public void No_two_codes_share_a_number()
    {
        var duplicates = Enum.GetValues<WarningCode>()
            .GroupBy(code => (int)code)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(code => code.ToString()))}")
            .ToList();

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// The consequence of the above, asserted directly: a code must round trip to its own
    /// name, because that name is the resource key its message is stored under.
    /// </summary>
    [Fact]
    public void Every_code_round_trips_to_its_own_name()
    {
        foreach (var code in Enum.GetValues<WarningCode>())
        {
            Assert.Equal(code, Enum.Parse<WarningCode>(code.ToString()));
        }
    }
}
