using Linewise.Application.Import;
using Linewise.Domain.Entities;
using Xunit;

namespace Linewise.Tests.Import;

/// <summary>
/// Punctuation in surnames pulls in two directions, and both directions turn up on a real
/// sheet. These exist so a future tidy-up of the normaliser cannot quietly break one while
/// fixing the other.
/// </summary>
public sealed class HyphenatedNameTests
{
    private static readonly Guid EmployeeId = new("10000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("Sean O'Invented", "Sean OInvented")]
    [InlineData("Sean OInvented", "Sean O'Invented")]
    [InlineData("Sean O'Invented", "OInvented, Sean")]
    public void AnElidedApostropheStillMatches(string onFile, string onSheet)
    {
        Assert.Equal(NameMatchOutcome.Exact, MatchOf(onFile, onSheet).Outcome);
    }

    [Theory]
    [InlineData("Ada Smith-Fabricated", "Ada Smith Fabricated")]
    [InlineData("Ada Smith Fabricated", "Ada Smith-Fabricated")]
    public void AHyphenBehavesLikeASpace(string onFile, string onSheet)
    {
        Assert.Equal(NameMatchOutcome.Exact, MatchOf(onFile, onSheet).Outcome);
    }

    [Fact]
    public void AHyphenatedNameIsNotConfusedWithADifferentPerson()
    {
        Assert.Equal(
            NameMatchOutcome.Unmatched,
            MatchOf("Ada Smith-Fabricated", "Ada Fabricated-Notreal").Outcome);
    }

    private static NameMatch MatchOf(string onFile, string onSheet) =>
        new EmployeeNameMatcher([new Employee { Id = EmployeeId, FullName = onFile }])
            .Match(onSheet);
}
